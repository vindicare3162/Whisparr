using System;
using System.IO;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Download;
using NzbDrone.Core.Movies.Studios;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.MediaFiles.MovieImport.Specifications
{
    public class MatchesFolderSpecification : IImportDecisionEngineSpecification
    {
        private readonly IStudioService _studioService;
        private readonly Logger _logger;

        public MatchesFolderSpecification(IStudioService studioService, Logger logger)
        {
            _studioService = studioService;
            _logger = logger;
        }

        public ImportSpecDecision IsSatisfiedBy(LocalMovie localMovie, DownloadClientItem downloadClientItem)
        {
            if (localMovie.ExistingFile)
            {
                return ImportSpecDecision.Accept();
            }

            var dirInfo = new FileInfo(localMovie.Path).Directory;

            if (dirInfo == null)
            {
                return ImportSpecDecision.Accept();
            }

            var folderInfo = localMovie.FolderMovieInfo;

            if (folderInfo == null)
            {
                return ImportSpecDecision.Accept();
            }

            var movieMetadata = localMovie.Movie?.MovieMetadata?.Value;

            if (movieMetadata == null)
            {
                return ImportSpecDecision.Accept();
            }

            // Only reject on unambiguous identifier mismatches (stash ID, code, resolved studio) --
            // folder naming conventions vary too widely to safely reject on fuzzy title differences,
            // and a false-positive rejection here is far more disruptive than the protection this
            // check provides.
            if (folderInfo.StashId.IsNotNullOrWhiteSpace() && movieMetadata.ForeignId.IsNotNullOrWhiteSpace() &&
                !folderInfo.StashId.Equals(movieMetadata.ForeignId, StringComparison.OrdinalIgnoreCase))
            {
                _logger.Debug("Folder StashId '{0}' does not match movie StashId '{1}' for {2}", folderInfo.StashId, movieMetadata.ForeignId, localMovie.Movie);

                return ImportSpecDecision.Reject(ImportRejectionReason.MovieNotFoundInFolder, "Folder ID '{0}' does not match movie '{1}'", folderInfo.StashId, localMovie.Movie);
            }

            if (folderInfo.Code.IsNotNullOrWhiteSpace() && movieMetadata.Code.IsNotNullOrWhiteSpace() &&
                !folderInfo.Code.Equals(movieMetadata.Code, StringComparison.OrdinalIgnoreCase))
            {
                _logger.Debug("Folder code '{0}' does not match movie code '{1}' for {2}", folderInfo.Code, movieMetadata.Code, localMovie.Movie);

                return ImportSpecDecision.Reject(ImportRejectionReason.MovieNotFoundInFolder, "Folder code '{0}' does not match movie '{1}'", folderInfo.Code, localMovie.Movie);
            }

            if (folderInfo.IsScene && folderInfo.StudioTitle.IsNotNullOrWhiteSpace() && movieMetadata.StudioForeignId.IsNotNullOrWhiteSpace())
            {
                var folderStudios = _studioService.FindAllByTitle(folderInfo.StudioTitle);

                if (folderStudios.Any() && folderStudios.All(s => s.ForeignId != movieMetadata.StudioForeignId))
                {
                    _logger.Debug("Folder studio '{0}' does not match movie studio for {1}", folderInfo.StudioTitle, localMovie.Movie);

                    return ImportSpecDecision.Reject(ImportRejectionReason.MovieNotFoundInFolder, "Folder studio '{0}' does not match movie '{1}'", folderInfo.StudioTitle, localMovie.Movie);
                }
            }

            return ImportSpecDecision.Accept();
        }
    }
}
