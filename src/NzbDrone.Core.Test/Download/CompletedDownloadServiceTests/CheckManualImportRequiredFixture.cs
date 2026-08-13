using System.Collections.Generic;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.History;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.Download.CompletedDownloadServiceTests
{
    [TestFixture]
    public class CheckManualImportRequiredFixture : CoreTest<CompletedDownloadService>
    {
        private TrackedDownload _trackedDownload;
        private Movie _movie;

        [SetUp]
        public void Setup()
        {
            _movie = Builder<Movie>.CreateNew().Build();

            var completed = Builder<DownloadClientItem>.CreateNew()
                .With(h => h.Status = DownloadItemStatus.Completed)
                .With(h => h.OutputPath = new OsPath(@"C:\DropFolder\MyDownload".AsOsAgnostic()))
                .With(h => h.Title = "Some.Unparseable.Title")
                .With(h => h.DownloadId = "abc123")
                .Build();

            _trackedDownload = Builder<TrackedDownload>.CreateNew()
                .With(c => c.State = TrackedDownloadState.Downloading)
                .With(c => c.DownloadItem = completed)
                .Build();

            Mocker.GetMock<IDownloadClient>()
                .SetupGet(c => c.Definition)
                .Returns(new DownloadClientDefinition { Id = 1, Name = "testClient" });

            Mocker.GetMock<IProvideDownloadClient>()
                .Setup(c => c.Get(It.IsAny<int>()))
                .Returns(Mocker.GetMock<IDownloadClient>().Object);

            Mocker.GetMock<IProvideImportItemService>()
                .Setup(c => c.ProvideImportItem(It.IsAny<DownloadClientItem>(), It.IsAny<DownloadClientItem>()))
                .Returns((DownloadClientItem item, DownloadClientItem previous) => item);

            // The download's own title can't be self-parsed, forcing the fallback to grab history.
            Mocker.GetMock<IParsingService>()
                .Setup(s => s.GetMovie(It.IsAny<string>(), false))
                .Returns((Movie)null);

            Mocker.GetMock<IMovieService>()
                .Setup(s => s.GetMovie(_movie.Id))
                .Returns(_movie);
        }

        private void GivenHistory(MovieMatchType matchType, ReleaseSourceType releaseSource)
        {
            var history = new MovieHistory
            {
                MovieId = _movie.Id,
                EventType = MovieHistoryEventType.Grabbed,
                Data = new Dictionary<string, string>
                {
                    { MovieHistory.MOVIE_MATCH_TYPE, matchType.ToString() },
                    { MovieHistory.RELEASE_SOURCE, releaseSource.ToString() },
                },
            };

            Mocker.GetMock<IHistoryService>()
                .Setup(s => s.FindByDownloadId(_trackedDownload.DownloadItem.DownloadId))
                .Returns(new List<MovieHistory> { history });
        }

        [TestCase(ReleaseSourceType.Rss)]
        [TestCase(ReleaseSourceType.Search)]
        [TestCase(ReleaseSourceType.UserInvokedSearch)]
        [TestCase(ReleaseSourceType.InteractiveSearch)]
        [TestCase(ReleaseSourceType.ReleasePush)]
        public void should_allow_import_when_id_matched_from_a_known_release_source(ReleaseSourceType releaseSource)
        {
            GivenHistory(MovieMatchType.Id, releaseSource);

            Subject.Check(_trackedDownload);

            _trackedDownload.State.Should().Be(TrackedDownloadState.ImportPending);
        }

        [Test]
        public void should_require_manual_import_when_id_matched_from_an_unknown_release_source()
        {
            GivenHistory(MovieMatchType.Id, ReleaseSourceType.Unknown);

            Subject.Check(_trackedDownload);

            _trackedDownload.State.Should().Be(TrackedDownloadState.ImportBlocked);
        }
    }
}
