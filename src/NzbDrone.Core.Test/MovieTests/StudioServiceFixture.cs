using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Movies.Studios;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MovieTests
{
    [TestFixture]
    public class StudioServiceFixture : CoreTest<StudioService>
    {
        [Test]
        public void FindAllByTitle_should_return_exact_match_without_calling_fuzzy_lookup()
        {
            var studio = new Studio { Title = "Property Sex" };

            Mocker.GetMock<IStudioRepository>()
                .Setup(s => s.FindAllByTitle(It.IsAny<string>()))
                .Returns(new List<Studio> { studio });

            var result = Subject.FindAllByTitle("Property Sex");

            result.Should().ContainSingle().Which.Should().Be(studio);

            Mocker.GetMock<IStudioRepository>()
                .Verify(s => s.FindAllByTitleFuzzy(It.IsAny<string>()), Times.Never());
        }

        [Test]
        public void FindAllByTitle_should_fall_back_to_fuzzy_match_when_exact_match_is_empty_and_unambiguous()
        {
            var studio = new Studio { Title = "Property Sex Originals" };

            Mocker.GetMock<IStudioRepository>()
                .Setup(s => s.FindAllByTitle(It.IsAny<string>()))
                .Returns(new List<Studio>());

            Mocker.GetMock<IStudioRepository>()
                .Setup(s => s.FindAllByTitleFuzzy(It.IsAny<string>()))
                .Returns(new List<Studio> { studio });

            var result = Subject.FindAllByTitle("Property Sex");

            result.Should().ContainSingle().Which.Should().Be(studio);
        }

        [Test]
        public void FindAllByTitle_should_not_use_fuzzy_match_when_ambiguous()
        {
            var studios = new List<Studio>
            {
                new Studio { Title = "Property Sex Originals" },
                new Studio { Title = "Property Sex Classics" }
            };

            Mocker.GetMock<IStudioRepository>()
                .Setup(s => s.FindAllByTitle(It.IsAny<string>()))
                .Returns(new List<Studio>());

            Mocker.GetMock<IStudioRepository>()
                .Setup(s => s.FindAllByTitleFuzzy(It.IsAny<string>()))
                .Returns(studios);

            var result = Subject.FindAllByTitle("Property Sex");

            result.Should().BeEmpty();
        }
    }
}
