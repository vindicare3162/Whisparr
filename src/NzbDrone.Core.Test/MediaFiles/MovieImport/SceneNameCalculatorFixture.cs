using FizzWare.NBuilder;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.MediaFiles.MovieImport;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.MediaFiles.MovieImport
{
    [TestFixture]
    public class SceneNameCalculatorFixture : CoreTest
    {
        private LocalMovie _localMovie;

        [SetUp]
        public void Setup()
        {
            _localMovie = Builder<LocalMovie>.CreateNew()
                .With(l => l.Path = @"C:\Test\Unsorted\movie.mkv".AsOsAgnostic())
                .With(l => l.OtherVideoFiles = false)
                .Build();
        }

        [Test]
        public void should_use_download_client_release_title_when_present()
        {
            _localMovie.DownloadClientMovieInfo = new ParsedMovieInfo { ReleaseTitle = "Some.Release.Title.mkv" };

            SceneNameCalculator.GetSceneName(_localMovie).Should().Be("Some.Release.Title");
        }

        [Test]
        public void should_not_throw_when_download_client_release_title_is_null()
        {
            // Simulates a download matched by external ID or recreated from history, where the
            // parsed release info exists but has no ReleaseTitle set.
            _localMovie.DownloadClientMovieInfo = new ParsedMovieInfo { ReleaseTitle = null };

            Assert.DoesNotThrow(() => SceneNameCalculator.GetSceneName(_localMovie));
        }

        [Test]
        public void should_fall_back_when_download_client_release_title_is_null()
        {
            _localMovie.DownloadClientMovieInfo = new ParsedMovieInfo { ReleaseTitle = null };

            SceneNameCalculator.GetSceneName(_localMovie).Should().BeNull();
        }
    }
}
