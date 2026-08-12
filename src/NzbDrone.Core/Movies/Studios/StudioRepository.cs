using System.Collections.Generic;
using System.Linq;
using Dapper;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Parser;

namespace NzbDrone.Core.Movies.Studios
{
    public interface IStudioRepository : IBasicRepository<Studio>
    {
        Studio FindByForeignId(string foreignId);
        List<Studio> FindByForeignIds(List<string> foreignIds);
        Studio FindByTitle(string title);
        List<Studio> SearchStudios(string cleanTitle, string foreignId);
        List<Studio> FindAllByTitle(string title);
        List<Studio> FindAllByTitleFuzzy(string title);
        List<string> AllStudioForeignIds();
    }

    public class StudioRepository : BasicRepository<Studio>, IStudioRepository
    {
        public StudioRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public Studio FindByTitle(string title)
        {
            return FindAllByTitle(title).FirstOrDefault();
        }

        public List<Studio> SearchStudios(string cleanTitle, string foreignId)
        {
            return Query(x => x.CleanTitle.Contains(cleanTitle) || x.ForeignId == foreignId).ToList();
        }

        public List<Studio> FindAllByTitle(string title)
        {
            return All().Where(x => x.CleanTitle == title || x.CleanSearchTitle == title || (x.Aliases != null && x.Aliases.Where(x => x.CleanStudioTitle()?.ToLower() == title).Any())).ToList();
        }

        public List<Studio> FindAllByTitleFuzzy(string title)
        {
            // Require a minimum length before doing substring matching, otherwise short/abbreviated
            // titles (e.g. "ps") would match against a large number of unrelated studios.
            if (title.IsNullOrWhiteSpace() || title.Length < 4)
            {
                return new List<Studio>();
            }

            bool StudioTitleMatches(string candidate)
            {
                if (candidate.IsNullOrWhiteSpace())
                {
                    return false;
                }

                return candidate.Contains(title) || title.Contains(candidate);
            }

            return All().Where(x => StudioTitleMatches(x.CleanTitle) ||
                                     StudioTitleMatches(x.CleanSearchTitle) ||
                                     (x.Aliases != null && x.Aliases.Any(a => StudioTitleMatches(a.CleanStudioTitle()?.ToLower()))))
                         .ToList();
        }

        public Studio FindByForeignId(string foreignId)
        {
            return Query(x => x.ForeignId == foreignId).FirstOrDefault();
        }

        public List<Studio> FindByForeignIds(List<string> foreignIds)
        {
            return Query(x => foreignIds.Contains(x.ForeignId)).ToList();
        }

        public List<string> AllStudioForeignIds()
        {
            using (var conn = _database.OpenConnection())
            {
                return conn.Query<string>("SELECT \"ForeignId\" FROM \"Studios\"").ToList();
            }
        }
    }
}
