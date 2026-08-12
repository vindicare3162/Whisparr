using System.Collections.Generic;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.MediaFiles.MovieImport.Specifications;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Movies.Studios;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.MediaFiles.MovieImport.Specifications
{
    [TestFixture]
    public class MatchesFolderSpecificationFixture : CoreTest<MatchesFolderSpecification>
    {
        private Movie _movie;
        private LocalMovie _localMovie;

        [SetUp]
        public void Setup()
        {
            _movie = Builder<Movie>.CreateNew()
                .With(m => m.Id = 1)
                .With(m => m.MovieMetadata.Value.ForeignId = "movie-stash-id")
                .With(m => m.MovieMetadata.Value.Code = "MOVIE-CODE")
                .With(m => m.MovieMetadata.Value.StudioForeignId = "movie-studio-id")
                .Build();

            _localMovie = Builder<LocalMovie>.CreateNew()
                .With(l => l.Path = @"C:\Test\Unsorted\Some.Folder\file.mkv".AsOsAgnostic())
                .With(l => l.Movie = _movie)
                .With(l => l.ExistingFile = false)
                .With(l => l.FolderMovieInfo = null)
                .Build();

            Mocker.GetMock<IStudioService>()
                .Setup(s => s.FindAllByTitle(It.IsAny<string>()))
                .Returns(new List<Studio>());
        }

        [Test]
        public void should_be_accepted_for_existing_file()
        {
            _localMovie.ExistingFile = true;

            Subject.IsSatisfiedBy(_localMovie, null).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_be_accepted_if_folder_could_not_be_parsed()
        {
            _localMovie.FolderMovieInfo = null;

            Subject.IsSatisfiedBy(_localMovie, null).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_be_accepted_if_folder_has_no_identifying_info()
        {
            _localMovie.FolderMovieInfo = new ParsedMovieInfo();

            Subject.IsSatisfiedBy(_localMovie, null).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_be_accepted_if_folder_stash_id_matches()
        {
            _localMovie.FolderMovieInfo = new ParsedMovieInfo { StashId = "movie-stash-id" };

            Subject.IsSatisfiedBy(_localMovie, null).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_be_rejected_if_folder_stash_id_does_not_match()
        {
            _localMovie.FolderMovieInfo = new ParsedMovieInfo { StashId = "some-other-stash-id" };

            Subject.IsSatisfiedBy(_localMovie, null).Accepted.Should().BeFalse();
        }

        [Test]
        public void should_be_accepted_if_folder_code_matches()
        {
            _localMovie.FolderMovieInfo = new ParsedMovieInfo { Code = "MOVIE-CODE" };

            Subject.IsSatisfiedBy(_localMovie, null).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_be_rejected_if_folder_code_does_not_match()
        {
            _localMovie.FolderMovieInfo = new ParsedMovieInfo { Code = "OTHER-CODE" };

            Subject.IsSatisfiedBy(_localMovie, null).Accepted.Should().BeFalse();
        }

        [Test]
        public void should_be_accepted_if_folder_studio_cannot_be_resolved()
        {
            // Studio lookup returns no matches -- ambiguous, don't block the import.
            // ReleaseDate is set purely so ParsedMovieInfo.IsScene is true, exercising the studio check.
            _localMovie.FolderMovieInfo = new ParsedMovieInfo { StudioTitle = "Some Unknown Studio", ReleaseDate = "2024-01-01" };

            Subject.IsSatisfiedBy(_localMovie, null).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_be_rejected_if_folder_studio_resolves_to_a_different_studio()
        {
            _localMovie.FolderMovieInfo = new ParsedMovieInfo { StudioTitle = "Other Studio", ReleaseDate = "2024-01-01" };

            Mocker.GetMock<IStudioService>()
                .Setup(s => s.FindAllByTitle("Other Studio"))
                .Returns(new List<Studio> { new Studio { ForeignId = "some-other-studio-id" } });

            Subject.IsSatisfiedBy(_localMovie, null).Accepted.Should().BeFalse();
        }

        [Test]
        public void should_be_accepted_if_folder_studio_resolves_to_the_same_studio()
        {
            _localMovie.FolderMovieInfo = new ParsedMovieInfo { StudioTitle = "Movie Studio", ReleaseDate = "2024-01-01" };

            Mocker.GetMock<IStudioService>()
                .Setup(s => s.FindAllByTitle("Movie Studio"))
                .Returns(new List<Studio> { new Studio { ForeignId = "movie-studio-id" } });

            Subject.IsSatisfiedBy(_localMovie, null).Accepted.Should().BeTrue();
        }
    }
}
