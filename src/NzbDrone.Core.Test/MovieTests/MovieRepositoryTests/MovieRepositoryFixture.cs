using System.Collections.Generic;
using System.Linq;
using FizzWare.NBuilder;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.CustomFormats;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MovieTests.MovieRepositoryTests
{
    [TestFixture]

    public class MovieRepositoryFixture : DbTest<MovieRepository, Movie>
    {
        private IQualityProfileRepository _profileRepository;

        [SetUp]
        public void Setup()
        {
            _profileRepository = Mocker.Resolve<QualityProfileRepository>();
            Mocker.SetConstant<IQualityProfileRepository>(_profileRepository);

            Mocker.GetMock<ICustomFormatService>()
                .Setup(x => x.All())
                .Returns(new List<CustomFormat>());
        }

        [Test]
        public void should_load_quality_profile()
        {
            var profile = new QualityProfile
            {
                Items = Qualities.QualityFixture.GetDefaultQualities(Quality.Bluray1080p, Quality.DVD, Quality.HDTV720p),
                FormatItems = CustomFormatsTestHelpers.GetDefaultFormatItems(),
                MinFormatScore = 0,
                Cutoff = Quality.Bluray1080p.Id,
                Name = "TestProfile"
            };

            _profileRepository.Insert(profile);

            var movie = Builder<Movie>.CreateNew().BuildNew();
            movie.QualityProfileId = profile.Id;

            Subject.Insert(movie);

            Subject.All().Single().QualityProfile.Should().NotBeNull();
        }

        [Test]
        public void get_by_movie_metadata_ids_should_return_only_requested_movies_with_metadata()
        {
            var metadata1 = Builder<MovieMetadata>.CreateNew().With(m => m.TmdbId = 111).With(m => m.ForeignId = "foreign-1").BuildNew();
            var metadata2 = Builder<MovieMetadata>.CreateNew().With(m => m.TmdbId = 222).With(m => m.ForeignId = "foreign-2").BuildNew();
            var metadata3 = Builder<MovieMetadata>.CreateNew().With(m => m.TmdbId = 333).With(m => m.ForeignId = "foreign-3").BuildNew();

            Db.Insert(metadata1);
            Db.Insert(metadata2);
            Db.Insert(metadata3);

            var movie1 = Builder<Movie>.CreateNew().With(m => m.MovieMetadataId = metadata1.Id).BuildNew();
            var movie2 = Builder<Movie>.CreateNew().With(m => m.MovieMetadataId = metadata2.Id).BuildNew();
            var movie3 = Builder<Movie>.CreateNew().With(m => m.MovieMetadataId = metadata3.Id).BuildNew();

            Subject.Insert(movie1);
            Subject.Insert(movie2);
            Subject.Insert(movie3);

            var result = Subject.GetByMovieMetadataIds(new List<int> { metadata1.Id, metadata3.Id });

            result.Should().HaveCount(2);
            result.Select(x => x.MovieMetadataId).Should().BeEquivalentTo(new[] { metadata1.Id, metadata3.Id });
            result.Should().OnlyContain(x => x.MovieMetadata != null && x.MovieMetadata.Value != null);
        }
    }
}
