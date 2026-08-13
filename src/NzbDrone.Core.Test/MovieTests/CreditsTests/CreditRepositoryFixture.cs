using System.Collections.Generic;
using System.Linq;
using FizzWare.NBuilder;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Movies.Credits;
using NzbDrone.Core.Movies.Performers;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MovieTests.CreditsTests
{
    [TestFixture]
    public class CreditRepositoryFixture : DbTest<CreditRepository, Credit>
    {
        [Test]
        public void find_by_performer_foreign_ids_should_only_return_credits_for_requested_performers()
        {
            var metadata = Builder<MovieMetadata>.CreateNew().With(m => m.TmdbId = 555).BuildNew();
            Db.Insert(metadata);

            var creditForPerformerA = Builder<Credit>.CreateNew()
                .With(c => c.MovieMetadataId = metadata.Id)
                .With(c => c.PerformerForeignId = "performer-a")
                .BuildNew();

            var creditForPerformerB = Builder<Credit>.CreateNew()
                .With(c => c.MovieMetadataId = metadata.Id)
                .With(c => c.PerformerForeignId = "performer-b")
                .BuildNew();

            var creditForPerformerC = Builder<Credit>.CreateNew()
                .With(c => c.MovieMetadataId = metadata.Id)
                .With(c => c.PerformerForeignId = "performer-c")
                .BuildNew();

            Subject.Insert(creditForPerformerA);
            Subject.Insert(creditForPerformerB);
            Subject.Insert(creditForPerformerC);

            var result = Subject.FindByPerformerForeignIds(new List<string> { "performer-a", "performer-c" });

            result.Select(x => x.PerformerForeignId).Should().BeEquivalentTo(new[] { "performer-a", "performer-c" });
        }

        [Test]
        public void find_by_movie_metadata_ids_should_only_return_credits_for_requested_movies_and_populate_performer()
        {
            var metadata1 = Builder<MovieMetadata>.CreateNew().With(m => m.TmdbId = 111).With(m => m.ForeignId = "foreign-1").BuildNew();
            var metadata2 = Builder<MovieMetadata>.CreateNew().With(m => m.TmdbId = 222).With(m => m.ForeignId = "foreign-2").BuildNew();
            Db.Insert(metadata1);
            Db.Insert(metadata2);

            var performer = Builder<Performer>.CreateNew()
                .With(p => p.ForeignId = "performer-1")
                .With(p => p.Name = "Test Performer")
                .BuildNew();
            Db.Insert(performer);

            var creditForMovie1 = Builder<Credit>.CreateNew()
                .With(c => c.MovieMetadataId = metadata1.Id)
                .With(c => c.PerformerForeignId = "performer-1")
                .BuildNew();

            var creditForMovie2 = Builder<Credit>.CreateNew()
                .With(c => c.MovieMetadataId = metadata2.Id)
                .With(c => c.PerformerForeignId = "performer-1")
                .BuildNew();

            Subject.Insert(creditForMovie1);
            Subject.Insert(creditForMovie2);

            var result = Subject.FindByMovieMetadataIds(new List<int> { metadata1.Id });

            result.Should().HaveCount(1);
            result.Single().MovieMetadataId.Should().Be(metadata1.Id);
            result.Single().Performer.Should().NotBeNull();
            result.Single().Performer.ForeignId.Should().Be("performer-1");
            result.Single().Performer.Name.Should().Be("Test Performer");
        }
    }
}
