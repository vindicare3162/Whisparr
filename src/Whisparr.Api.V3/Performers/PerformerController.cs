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
using NzbDrone.Core.Movies.Performers;
using NzbDrone.Core.Movies.Performers.Events;
using NzbDrone.Core.MovieStats;
using NzbDrone.SignalR;
using Whisparr.Api.V3.Shared;
using Whisparr.Http;
using Whisparr.Http.REST.Attributes;

namespace Whisparr.Api.V3.Performers
{
    /// <summary>Controller for managing performers in Whisparr</summary>
    [V3ApiController]
    public class PerformerController : RestControllerWithResourceCache<PerformerResource, Performer>,
        IHandle<PerformerUpdatedEvent>,
        IHandle<MoviesDeletedEvent>,
        IHandle<MoviesImportedEvent>,
        IHandle<MovieFileAddedEvent>,
        IHandle<MovieFileDeletedEvent>,
        IHandle<MovieFileUpdatedEvent>
    {
        private readonly IPerformerService _performerService;
        private readonly IAddPerformerService _addPerformerService;
        private readonly IMapCoversToLocal _coverMapper;
        private readonly IMovieService _moviesService;
        private readonly IMovieStatisticsService _movieStatisticsService;
        private readonly IImportListExclusionService _exclusionService;
        private readonly IConfigService _configService;
        private readonly bool _useCache;

        public PerformerController(IPerformerService performerService,
                                   IAddPerformerService addPerformerService,
                                   IMapCoversToLocal coverMapper,
                                   IMovieService moviesService,
                                   IMovieStatisticsService movieStatisticsService,
                                   IImportListExclusionService exclusionService,
                                   ICacheManager cacheManager,
                                   IConfigService configService,
                                   Logger logger,
                                   IBroadcastSignalRMessage signalRBroadcaster)
        : base(cacheManager.GetCache<PerformerResource>(typeof(PerformerResource), "performerResources"), logger, signalRBroadcaster)
        {
            _performerService = performerService;
            _addPerformerService = addPerformerService;
            _configService = configService;
            _coverMapper = coverMapper;
            _moviesService = moviesService;
            _movieStatisticsService = movieStatisticsService;
            _exclusionService = exclusionService;
            _useCache = _configService.WhisparrCachePerformerAPI;

            if (configService.WhisparrMovieMetadataSource == MovieMetadataType.TMDB)
            {
                SharedValidator.RuleFor(s => s.MoviesMonitored)
                    .Must((s, monitored) => !monitored || s.TmdbId > 0)
                    .WithMessage("Requires a TMDB link to be added to the Performer within StashDB");
            }

            if (configService.WhisparrMovieMetadataSource == MovieMetadataType.TPDB)
            {
                SharedValidator.RuleFor(s => s.MoviesMonitored)
                    .Must((s, monitored) => !monitored || !string.IsNullOrWhiteSpace(s.TpdbId))
                    .WithMessage("Requires a TPDB link to be added to the Performer within StashDB");
            }
        }

        /// <summary>Retrieves a performer by their Whisparr (local)internal ID</summary>
        /// <param name="id">The internal ID of the performer</param>
        /// <returns>Performer details with associated movies and local cover URLs</returns>
        /// <response code="200">Performer found and returned</response>
        /// <response code="404">Performer with the specified ID not found</response>
        [HttpGet("{id:int}")]
        [Produces("application/json")]
        protected override PerformerResource GetResourceById(int id)
        {
            var resource = _performerService.GetById(id).ToResource();

            _coverMapper.ConvertToLocalPerformerUrls(resource.Id, resource.Images);

            FetchAndLinkMovies(resource);

            return resource;
        }

        /// <summary>Retrieves full list of performers, or a single performer by their external foreign ID (e.g., from StashDb)</summary>
        /// <param name="stashId">The external foreign ID (StashDb ID) of the performer</param>
        /// <returns>Performer details with associated movies and local cover URLs</returns>
        /// <response code="200">Performer found and returned</response>
        /// <response code="404">Performer with the specified foreign ID not found</response>
        [HttpGet]
        [Produces("application/json")]
        public List<PerformerResource> GetPerformers(string stashId)
        {
            var performerResources = new List<PerformerResource>();

            if (_useCache)
            {
                if (stashId.IsNotNullOrWhiteSpace())
                {
                    var resource = GetCachedResource(stashId);

                    if (resource != null)
                    {
                        performerResources.Add(resource);
                    }
                }
                else
                {
                    performerResources = GetCachedResources(AllResourceForeignIds());
                }

                return performerResources;
            }
            else
            {
                if (stashId.IsNotNullOrWhiteSpace())
                {
                    var performer = _performerService.FindByForeignId(stashId);

                    if (performer != null)
                    {
                        performerResources.Add(performer.ToResource());
                    }
                }
                else
                {
                    performerResources = _performerService.GetAllPerformers().ToResource();
                }
            }

            var coverFileInfos = _coverMapper.GetPerformerCoverFileInfos();

            _coverMapper.ConvertToLocalPerformerUrls(performerResources.Select(x => Tuple.Create(x.Id, x.Images.AsEnumerable())), coverFileInfos);

            LinkMovies(performerResources);

            return performerResources;
        }

        /// <summary>Adds a new performer to Whisparr</summary>
        /// <param name="performerResource">The performer details to add</param>
        /// <returns>The newly added performer details</returns>
        /// <response code="201">Performer successfully added</response>
        /// <response code="400">Invalid performer details provided</response>
        [RestPostById]
        [Consumes("application/json")]
        [Produces("application/json")]
        public ActionResult<PerformerResource> AddPerformer([FromBody] PerformerResource performerResource)
        {
            var performer = _addPerformerService.AddPerformer(performerResource.ToModel());

            // Clear any negative cache entry left by requests made before the performer existed.
            InvalidateCachedResource(performer.ForeignId);

            return Created(performer.Id);
        }

        /// <summary>Updates an existing performer in Whisparr</summary>
        /// <param name="resource">The performer details to update</param>
        /// <returns>The updated performer details</returns>
        /// <response code="202">Performer successfully updated</response>
        /// <response code="400">Invalid performer details provided</response>
        /// <response code="404">Performer with the specified ID not found</response>
        [RestPutById]
        [Consumes("application/json")]
        [Produces("application/json")]
        public ActionResult<PerformerResource> Update([FromBody] PerformerResource resource)
        {
            var performer = _performerService.GetById(resource.Id);

            var updatedPerformer = _performerService.Update(resource.ToModel(performer));

            InvalidateCachedResource(updatedPerformer.ForeignId);
            BroadcastResourceChange(ModelAction.Updated, updatedPerformer.ToResource());

            return Accepted(updatedPerformer);
        }

        /// <summary>Deletes a performer and their associated movies/scenes from Whisparr</summary>
        /// <param name="id">The internal ID of the performer to delete</param>
        /// <param name="deleteFiles">If true, associated movie/scene files will also be deleted from disk</param>
        /// <param name="addImportExclusion">If true, an import exclusion will be added to prevent re-adding the performer in future imports</param>
        [RestDeleteById]
        public void DeletePerformer(int id, bool deleteFiles = false, bool addImportExclusion = false)
        {
            var performer = _performerService.GetById(id);

            if (performer == null)
            {
                return;
            }

            // Get the scenes for the performer
            var scenes = _moviesService.GetByPerformerForeignId(performer.ForeignId);
            var sceneIds = scenes.Select(x => x.Id).ToList();
            _moviesService.DeleteMovies(sceneIds, deleteFiles);

            if (addImportExclusion)
            {
                var exclusion = new ImportListExclusion();
                exclusion.ForeignId = performer.ForeignId;
                exclusion.MovieTitle = performer.Name;
                exclusion.Type = ImportExclusionType.Performer;

                _exclusionService.AddExclusion(exclusion);
            }

            // Remove the performer now that the associated scenes have been removed
            _performerService.RemovePerformer(performer);

            InvalidateCachedResource(performer.ForeignId);
        }

        /// <summary>Handles performer updated events to update the performer cache and broadcast changes via SignalR</summary>
        [NonAction]
        public void Handle(PerformerUpdatedEvent message)
        {
            var resource = message.Performer.ToResource();

            FetchAndLinkMovies(resource);
            InvalidateCachedResource(resource.ForeignId);
            BroadcastResourceChange(ModelAction.Updated, resource);
        }

        /// <summary>
        /// Movie-library changes (deletions, imports, file add/delete/update) affect cached
        /// performer counts and SizeOnDisk. Clear the whole cache: refilling is cheap (a handful
        /// of batched queries) and mapping one movie to the many performers it links to via
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

        private void FetchAndLinkMovies(PerformerResource resource)
        {
            var movies = _moviesService.GetByPerformerForeignId(resource.ForeignId);
            var movieStatsByMovieId = _movieStatisticsService.MovieStatistics(movies.Select(x => x.Id).ToList()).ToDictionary(x => x.MovieId);

            LinkMovies(resource, movies, movieStatsByMovieId);
        }

        // Batch performer movie/stats linkage into a single aggregation query instead of
        // loading every referenced movie into memory. The per-performer version caused
        // ~2x N queries (N being thousands in a typical library).
        protected override void LinkMovies(List<PerformerResource> resources)
        {
            if (resources.Count == 0)
            {
                return;
            }

            var performerForeignIds = resources.Select(x => x.ForeignId).ToList();
            var counts = _moviesService.GetPerformerMovieCounts(performerForeignIds);

            var countsByPerformer = counts
                .GroupBy(x => x.PerformerForeignId)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var resource in resources)
            {
                if (countsByPerformer.TryGetValue(resource.ForeignId, out var performerCounts))
                {
                    LinkMovies(resource, performerCounts);
                }
            }
        }

        private void LinkMovies(PerformerResource resource, List<PerformerMovieCount> counts)
        {
            var scenes = counts.Where(x => x.ItemType == (int)ItemType.Scene).ToList();
            var movies = counts.Where(x => x.ItemType == (int)ItemType.Movie).ToList();

            resource.HasScenes = scenes.Any();
            resource.HasMovies = movies.Any();

            resource.MovieCount = movies.Sum(x => x.HasFileCount);
            resource.TotalMovieCount = movies.Sum(x => x.TotalCount);
            resource.SceneCount = scenes.Sum(x => x.HasFileCount);
            resource.TotalSceneCount = scenes.Sum(x => x.TotalCount);

            // SizeOnDisk is the same across all ItemType groups for a performer, so take it once.
            resource.SizeOnDisk = counts.Select(x => x.SizeOnDisk).FirstOrDefault();
        }

        private void LinkMovies(PerformerResource resource, List<Movie> movies, Dictionary<int, MovieStatistics> movieStatsByMovieId)
        {
            var scenes = movies.Where(x => x.MovieMetadata.Value.ItemType == ItemType.Scene);
            resource.HasScenes = scenes.Any();
            resource.HasMovies = movies.Where(x => x.MovieMetadata.Value.ItemType == ItemType.Movie).Any();

            resource.MovieCount = movies.Where(x => x.HasFile && x.MovieMetadata.Value.ItemType == ItemType.Movie).Count();
            resource.TotalMovieCount = movies.Where(x => x.MovieMetadata.Value.ItemType == ItemType.Movie).Count();
            resource.SceneCount = movies.Where(x => x.HasFile && x.MovieMetadata.Value.ItemType == ItemType.Scene).Count();
            resource.TotalSceneCount = movies.Where(x => x.MovieMetadata.Value.ItemType == ItemType.Scene).Count();

            resource.SizeOnDisk = movies.Sum(x => movieStatsByMovieId.TryGetValue(x.Id, out var stats) ? stats.SizeOnDisk : 0);
        }

        protected override List<string> AllResourceForeignIds()
        {
            return _performerService.AllPerformerForeignIds();
        }

        protected override List<PerformerResource> BuildResources(List<string> foreignIds)
        {
            return _performerService.FindByForeignIds(foreignIds)
                .Where(x => x != null)
                .Select(x => x.ToResource())
                .ToList();
        }

        protected override void ConvertToLocalUrls(List<PerformerResource> newResources)
        {
            var coverFileInfos = _coverMapper.GetPerformerCoverFileInfos();

            _coverMapper.ConvertToLocalPerformerUrls(newResources.Select(x => Tuple.Create(x.Id, x.Images.AsEnumerable())), coverFileInfos);
        }

        protected override string GetForeignId(PerformerResource resource)
        {
            return resource.ForeignId;
        }
    }
}
