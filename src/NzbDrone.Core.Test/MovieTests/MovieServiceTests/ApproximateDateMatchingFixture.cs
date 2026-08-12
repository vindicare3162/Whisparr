using System;
using System.Collections.Generic;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.MetadataSource.SkyHook.Resource;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Movies.Studios;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MovieTests.MovieServiceTests
{
    [TestFixture]
    public class ApproximateDateMatchingFixture : CoreTest<MovieService>
    {
        private const string StudioForeignId = "ApproxStudio";

        private Movie _onlyCandidate;

        [SetUp]
        public void Setup()
        {
            var studioResource = new StudioResource { Title = StudioForeignId };

            _onlyCandidate = Builder<Movie>.CreateNew()
                .With(x => x.Id = 101)
                .With(x => x.Title = "Some Unrelated Title")
                .With(x => x.MovieMetadata.Value.Studio = studioResource)
                .With(x => x.MovieMetadata.Value.ReleaseDate = "2025-09-10")
                .With(x => x.MovieMetadata.Value.ReleaseDateUtc = new DateTime(2025, 9, 10))
                .Build();

            var farCandidate = Builder<Movie>.CreateNew()
                .With(x => x.Id = 102)
                .With(x => x.Title = "Some Other Far Away Title")
                .With(x => x.MovieMetadata.Value.Studio = studioResource)
                .With(x => x.MovieMetadata.Value.ReleaseDate = "2024-01-01")
                .With(x => x.MovieMetadata.Value.ReleaseDateUtc = new DateTime(2024, 1, 1))
                .Build();

            var studios = new List<Studio> { new Studio { ForeignId = StudioForeignId } };

            Mocker.GetMock<IStudioService>()
                .Setup(s => s.FindAllByTitle(It.Is<string>(t => t == StudioForeignId)))
                .Returns(studios);

            // No exact-date match, forcing the studio-only fallback path.
            Mocker.GetMock<IMovieRepository>()
                .Setup(s => s.FindByStudioAndDate(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(new List<Movie>());

            Mocker.GetMock<IMovieRepository>()
                .Setup(s => s.GetByStudioForeignId(It.Is<string>(t => t == StudioForeignId)))
                .Returns(new List<Movie> { _onlyCandidate, farCandidate });
        }

        private static ParsedMovieInfo GivenParsedInfo(string releaseDate, string unmatchedTitle)
        {
            return new ParsedMovieInfo
            {
                StudioTitle = StudioForeignId,
                ReleaseDate = releaseDate,
                ReleaseTokens = unmatchedTitle,
            };
        }

        [Test]
        public void should_not_match_approximately_when_feature_disabled()
        {
            Mocker.GetMock<IConfigService>()
                .Setup(s => s.SearchApproximateDateMatching)
                .Returns(false);

            var parsedInfo = GivenParsedInfo("2025-09-11", "Completely Unmatched Text");

            var movie = Subject.FindScene(parsedInfo, false, null);

            movie.Should().BeNull();
        }

        [Test]
        public void should_match_approximately_when_single_candidate_within_window()
        {
            Mocker.GetMock<IConfigService>()
                .Setup(s => s.SearchApproximateDateMatching)
                .Returns(true);

            // One day off from _onlyCandidate's actual release date, and far from farCandidate.
            var parsedInfo = GivenParsedInfo("2025-09-11", "Completely Unmatched Text");

            var movie = Subject.FindScene(parsedInfo, false, null);

            movie.Should().NotBeNull();
            movie.Id.Should().Be(_onlyCandidate.Id);
        }

        [Test]
        public void should_not_match_approximately_when_outside_window()
        {
            Mocker.GetMock<IConfigService>()
                .Setup(s => s.SearchApproximateDateMatching)
                .Returns(true);

            // Too far from either candidate's release date.
            var parsedInfo = GivenParsedInfo("2025-06-01", "Completely Unmatched Text");

            var movie = Subject.FindScene(parsedInfo, false, null);

            movie.Should().BeNull();
        }
    }
}
