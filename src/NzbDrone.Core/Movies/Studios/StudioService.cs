using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Cache;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Movies.Studios.Events;
using NzbDrone.Core.Parser;

namespace NzbDrone.Core.Movies.Studios
{
    public interface IStudioService
    {
        Studio AddStudio(Studio studio);
        List<Studio> AddStudios(List<Studio> studios);
        List<Studio> GetStudios(IEnumerable<int> studioIds);
        Studio GetById(int id);
        Studio FindByForeignId(string foreignId);
        List<Studio> FindByForeignIds(List<string> foreignIds);
        List<Studio> SearchStudios(string query);
        List<Studio> GetAllStudios();
        List<string> AllStudioForeignIds();
        Studio Update(Studio performer);
        List<Studio> Update(List<Studio> studios);
        Studio FindByTitle(string title);
        List<Studio> FindAllByTitle(string title);
        void RemoveStudio(Studio studio);
    }

    public class StudioService : IStudioService
    {
        private readonly IStudioRepository _studioRepo;
        private readonly IEventAggregator _eventAggregator;
        private readonly ICacheManager _cacheManager;
        private readonly string _cacheName;
        private readonly Logger _logger;

        public StudioService(IStudioRepository studioRepo, IEventAggregator eventAggregator, ICacheManager cacheManager, Logger logger)
        {
            _studioRepo = studioRepo;
            _eventAggregator = eventAggregator;
            _cacheManager = cacheManager;
            _cacheName = "Whisparr.Api.V3.Studios.StudioResource_studioResources";
            _logger = logger;
        }

        public Studio AddStudio(Studio newStudio)
        {
            var studio = _studioRepo.Insert(newStudio);

            _eventAggregator.PublishEvent(new StudioAddedEvent(GetById(studio.Id)));

            return studio;
        }

        public List<Studio> AddStudios(List<Studio> studios)
        {
            _studioRepo.InsertMany(studios);

            _eventAggregator.PublishEvent(new StudiosAddedEvent(studios));

            return studios;
        }

        public Studio GetById(int id)
        {
            return _studioRepo.Get(id);
        }

        public List<Studio> GetStudios(IEnumerable<int> studioIds)
        {
            return _studioRepo.Get(studioIds).ToList();
        }

        public List<Studio> GetAllStudios()
        {
            return _studioRepo.All().ToList();
        }

        public Studio Update(Studio studio)
        {
            RemoveStudioResourcesCache(studio.ForeignId);

            return _studioRepo.Update(studio);
        }

        public List<Studio> Update(List<Studio> studios)
        {
            _studioRepo.UpdateMany(studios);

            foreach (var studio in studios)
            {
                RemoveStudioResourcesCache(studio.ForeignId);
            }

            return studios;
        }

        public void RemoveStudio(Studio studio)
        {
            _studioRepo.Delete(studio);

            RemoveStudioResourcesCache(studio.ForeignId);
        }

        public Studio FindByTitle(string title)
        {
            var cleanTitle = title.CleanStudioTitle();

            return _studioRepo.FindByTitle(cleanTitle);
        }

        public List<Studio> FindAllByTitle(string title)
        {
            var cleanTitle = title.CleanStudioTitle().ToLower();

            var matches = _studioRepo.FindAllByTitle(cleanTitle);

            if (matches.Any())
            {
                return matches;
            }

            // Exact match against title/search title/aliases failed. Fall back to substring matching,
            // but only trust it when it resolves to a single unambiguous studio - guessing wrong here
            // causes worse outcomes (wrong scene matched) than returning nothing.
            var fuzzyMatches = _studioRepo.FindAllByTitleFuzzy(cleanTitle);

            if (fuzzyMatches.Count == 1)
            {
                _logger.Debug("No exact studio match for '{0}', using fuzzy match '{1}'", title, fuzzyMatches[0].Title);

                return fuzzyMatches;
            }

            if (fuzzyMatches.Count > 1)
            {
                _logger.Debug("No exact studio match for '{0}', found {1} ambiguous fuzzy matches, skipping", title, fuzzyMatches.Count);
            }

            return matches;
        }

        public Studio FindByForeignId(string foreignId)
        {
            return _studioRepo.FindByForeignId(foreignId);
        }

        public List<Studio> FindByForeignIds(List<string> foreignIds)
        {
            return _studioRepo.FindByForeignIds(foreignIds);
        }

        public List<Studio> SearchStudios(string query)
        {
            var cleanTitle = query.CleanStudioTitle().ToLower();

            return _studioRepo.SearchStudios(cleanTitle, query);
        }

        public List<string> AllStudioForeignIds()
        {
            return _studioRepo.AllStudioForeignIds();
        }

        private void RemoveStudioResourcesCache(string foreignId)
        {
            var studioResourcesCache = _cacheManager.FindCache(_cacheName);
            if (studioResourcesCache != null)
            {
                studioResourcesCache.Remove(foreignId);
            }
        }
    }
}
