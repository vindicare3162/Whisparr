using System;
using System.Collections.Generic;
using System.Linq;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using NLog;
using NzbDrone.Common.Cache;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Datastore.Events;
using NzbDrone.Core.ImportLists.ImportExclusions;
using NzbDrone.Core.MediaCover;
using NzbDrone.Core.MediaFiles.Events;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Movies.Events;
using NzbDrone.Core.Movies.Studios;
using NzbDrone.Core.Movies.Studios.Events;
using NzbDrone.Core.MovieStats;
using NzbDrone.SignalR;
using Whisparr.Api.V3.Shared;
using Whisparr.Http;
using Whisparr.Http.REST.Attributes;

namespace Whisparr.Api.V3.Studios
{
    [V3ApiController]
    public class StudioController : RestControllerWithResourceCache<StudioResource, Studio>,
        IHandle<StudioUpdatedEvent>,
        IHandle<MoviesDeletedEvent>,
        IHandle<MoviesImportedEvent>,
        IHandle<MovieFileAddedEvent>,
        IHandle<MovieFileDeletedEvent>,
        IHandle<MovieFileUpdatedEvent>
    {
        private readonly IStudioService _studioService;
        private readonly IAddStudioService _addStudioService;
        private readonly IMapCoversToLocal _coverMapper;
        private readonly IMovieService _moviesService;
        private readonly IMovieStatisticsService _movieStatisticsService;
        private readonly IImportListExclusionService _exclusionService;
        private readonly bool _useCache;

        public StudioController(IStudioService studioService,
                                IAddStudioService addStudioService,
                                IMapCoversToLocal coverMapper,
                                IMovieService moviesService,
                                IMovieStatisticsService movieStatisticsService,
                                IImportListExclusionService exclusionService,
                                ICacheManager cacheManager,
                                IConfigService configService,
                                Logger logger,
                                IBroadcastSignalRMessage signalRBroadcaster)
        : base(cacheManager.GetCache<StudioResource>(typeof(StudioResource), "studioResources"), logger, signalRBroadcaster)
        {
            _studioService = studioService;
            _addStudioService = addStudioService;
            _coverMapper = coverMapper;
            _moviesService = moviesService;
            _movieStatisticsService = movieStatisticsService;
            _exclusionService = exclusionService;
            _useCache = configService.WhisparrCacheStudioAPI;

            if (configService.WhisparrMovieMetadataSource == MovieMetadataType.TMDB)
            {
                SharedValidator.RuleFor(s => s.MoviesMonitored)
                    .Must((s, monitored) => !monitored || s.TmdbId > 0)
                    .WithMessage("Requires a TMDB link to be added to the Studio within StashDB");
            }

            if (configService.WhisparrMovieMetadataSource == MovieMetadataType.TPDB)
            {
                SharedValidator.RuleFor(s => s.MoviesMonitored)
                    .Must((s, monitored) => !monitored || !string.IsNullOrWhiteSpace(s.TpdbId))
                    .WithMessage("Requires a TPDB link to be added to the Studio within StashDB");
            }
        }

        protected override StudioResource GetResourceById(int id)
        {
            var resource = _studioService.GetById(id).ToResource();

            _coverMapper.ConvertToLocalStudioUrls(resource.Id, resource.Images);

            FetchAndLinkMovies(resource);

            return resource;
        }

        [HttpGet]
        public List<StudioResource> GetStudios(string stashId)
        {
            var studioResources = new List<StudioResource>();

            if (_useCache)
            {
                if (stashId.IsNotNullOrWhiteSpace())
                {
                    var resource = GetCachedResource(stashId);

                    if (resource != null)
                    {
                        studioResources.Add(resource);
                    }
                }
                else
                {
                    studioResources = GetCachedResources(AllResourceForeignIds());
                }
            }
            else
            {
                if (stashId.IsNotNullOrWhiteSpace())
                {
                    var studio = _studioService.FindByForeignId(stashId);

                    if (studio != null)
                    {
                        studioResources.Add(studio.ToResource());
                    }
                }
                else
                {
                    studioResources = _studioService.GetAllStudios().ToResource();
                }

                var coverFileInfos = _coverMapper.GetStudioCoverFileInfos();

                _coverMapper.ConvertToLocalStudioUrls(studioResources.Select(x => Tuple.Create(x.Id, x.Images.AsEnumerable())), coverFileInfos);

                LinkMovies(studioResources);
            }

            return studioResources;
        }

        [RestPostById]
        [Consumes("application/json")]
        [Produces("application/json")]
        public ActionResult<StudioResource> AddStudio([FromBody] StudioResource studioResource)
        {
            var studio = _addStudioService.AddStudio(studioResource.ToModel());

            // Clear any negative cache entry left by requests made before the studio existed.
            InvalidateCachedResource(studio.ForeignId);

            return Created(studio.Id);
        }

        [RestPutById]
        [Consumes("application/json")]
        [Produces("application/json")]
        public ActionResult<StudioResource> Update([FromBody] StudioResource resource)
        {
            var studio = _studioService.GetById(resource.Id);

            var updatedStudio = _studioService.Update(resource.ToModel(studio));

            InvalidateCachedResource(updatedStudio.ForeignId);
            BroadcastResourceChange(ModelAction.Updated, updatedStudio.ToResource());

            return Accepted(updatedStudio);
        }

        [RestDeleteById]
        public void DeleteStudio(int id, bool deleteFiles = false, bool addImportExclusion = false)
        {
            var studio = _studioService.GetById(id);

            if (studio == null)
            {
                return;
            }

            // Get the scenes for the studio
            var scenes = _moviesService.GetByStudioForeignId(studio.ForeignId);
            var sceneIds = scenes.Select(x => x.Id).ToList();
            _moviesService.DeleteMovies(sceneIds, deleteFiles);

            if (addImportExclusion)
            {
                var exclusion = new ImportListExclusion();
                exclusion.ForeignId = studio.ForeignId;
                exclusion.MovieTitle = studio.Title;
                exclusion.Type = ImportExclusionType.Studio;

                _exclusionService.AddExclusion(exclusion);
            }

            // Remove the studio now that the associated scenes have been removed
            _studioService.RemoveStudio(studio);

            InvalidateCachedResource(studio.ForeignId);
        }

        [NonAction]
        public void Handle(StudioUpdatedEvent message)
        {
            var resource = message.Studio.ToResource();

            InvalidateCachedResource(resource.ForeignId);
            FetchAndLinkMovies(resource);
            BroadcastResourceChange(ModelAction.Updated, message.Studio.ToResource());
        }

        /// <summary>
        /// Movie-library changes (deletions, imports, file add/delete/update) affect cached
        /// studio counts and SizeOnDisk. Clear the whole cache: refilling is cheap (a handful
        /// of batched queries) and mapping one movie to the many studios it links to via
        /// credits would cost more than the refill.
        /// </summary>
        [NonAction]
        public void Handle(MoviesDeletedEvent message) => InvalidateAllCachedResources();

        [NonAction]
        public void Handle(MoviesImportedEvent message) => InvalidateAllCachedResources();

        [NonAction]
        public void Handle(MovieFileAddedEvent message) => InvalidateAllCachedResources();

        [NonAction]
        public void Handle(MovieFileDeletedEvent message) => InvalidateAllCachedResources();

        [NonAction]
        public void Handle(MovieFileUpdatedEvent message) => InvalidateAllCachedResources();

        private void FetchAndLinkMovies(StudioResource resource)
        {
            LinkMovies(new List<StudioResource> { resource });
        }

        // Batch studio movie/stats linkage into a single aggregation query instead of loading every
        // referenced movie into memory per studio (~3 queries per studio, run while holding the
        // studio resource cache lock during cache fill). Mirrors the performer-side batch in
        // PerformerController.LinkMovies.
        protected override void LinkMovies(List<StudioResource> resources)
        {
            if (resources.Count == 0)
            {
                return;
            }

            var studioForeignIds = resources.Select(x => x.ForeignId).ToList();
            var counts = _moviesService.GetStudioMovieCounts(studioForeignIds);

            var countsByStudio = counts
                .GroupBy(x => x.StudioForeignId)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var resource in resources)
            {
                if (countsByStudio.TryGetValue(resource.ForeignId, out var studioCounts))
                {
                    LinkMovies(resource, studioCounts);
                }
                else
                {
                    // No movies for this studio - zero out the counts so cached data stays consistent
                    LinkMovies(resource, new List<StudioMovieCount>());
                }
            }
        }

        private void LinkMovies(StudioResource resource, List<StudioMovieCount> counts)
        {
            var scenes = counts.Where(x => x.ItemType == (int)ItemType.Scene).ToList();
            var movies = counts.Where(x => x.ItemType == (int)ItemType.Movie).ToList();

            resource.HasScenes = scenes.Any();
            resource.HasMovies = movies.Any();

            resource.MovieCount = movies.Sum(x => x.HasFileCount);
            resource.TotalMovieCount = movies.Sum(x => x.TotalCount);
            resource.SceneCount = scenes.Sum(x => x.HasFileCount);
            resource.TotalSceneCount = scenes.Sum(x => x.TotalCount);

            // SizeOnDisk is the same across all ItemType groups for a studio, so take it once.
            resource.SizeOnDisk = counts.Select(x => x.SizeOnDisk).FirstOrDefault();

            // Years is the same across all ItemType groups for a studio, so take it once.
            var years = counts.Select(x => x.Years).FirstOrDefault(x => x.IsNotNullOrWhiteSpace());

            resource.Years = years.IsNullOrWhiteSpace()
                ? new List<int>()
                : years.Split(',').Select(int.Parse).ToList();
        }

        protected override List<string> AllResourceForeignIds()
        {
            return _studioService.AllStudioForeignIds();
        }

        protected override List<StudioResource> BuildResources(List<string> foreignIds)
        {
            return _studioService.FindByForeignIds(foreignIds)
                .Where(x => x != null)
                .Select(x => x.ToResource())
                .ToList();
        }

        protected override void ConvertToLocalUrls(List<StudioResource> newResources)
        {
            var coverFileInfos = _coverMapper.GetStudioCoverFileInfos();

            _coverMapper.ConvertToLocalStudioUrls(newResources.Select(x => Tuple.Create(x.Id, x.Images.AsEnumerable())), coverFileInfos);
        }

        protected override string GetForeignId(StudioResource resource)
        {
            return resource.ForeignId;
        }
    }
}
