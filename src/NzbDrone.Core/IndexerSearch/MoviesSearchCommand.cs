using System.Collections.Generic;
using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.IndexerSearch
{
    public class MoviesSearchCommand : Command
    {
        public List<int> MovieIds { get; set; }

        // Server-side filter search (issue #37): when MovieIds is empty and
        // ItemType is set, the service searches all matching scenes via a
        // server-side paged query instead of requiring the full catalog
        // client-side.
        public string ItemType { get; set; }
        public bool? Monitored { get; set; }
        public bool? HasFile { get; set; }

        public override bool SendUpdatesToClient => true;
    }
}
