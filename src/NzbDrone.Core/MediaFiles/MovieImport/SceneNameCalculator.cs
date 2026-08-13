using System.IO;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.MediaFiles.MovieImport
{
    public static class SceneNameCalculator
    {
        public static string GetSceneName(LocalMovie localMovie)
        {
            var otherVideoFiles = localMovie.OtherVideoFiles;
            var downloadClientInfo = localMovie.DownloadClientMovieInfo;

            // The release title can be missing when the download was matched by external ID or
            // recreated from history, in which case fall back to the file/folder name below.
            if (!otherVideoFiles && downloadClientInfo != null && downloadClientInfo.ReleaseTitle.IsNotNullOrWhiteSpace())
            {
                return FileExtensions.RemoveFileExtension(downloadClientInfo.ReleaseTitle);
            }

            var fileName = Path.GetFileNameWithoutExtension(localMovie.Path.CleanFilePath());

            if (SceneChecker.IsSceneTitle(fileName))
            {
                return fileName;
            }

            var folderTitle = localMovie.FolderMovieInfo?.ReleaseTitle;

            if (!otherVideoFiles &&
                folderTitle.IsNotNullOrWhiteSpace() &&
                SceneChecker.IsSceneTitle(folderTitle))
            {
                return folderTitle;
            }

            return null;
        }
    }
}
