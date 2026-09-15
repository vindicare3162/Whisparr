using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.CustomFormats
{
    [TestFixture]
    public class SceneReleaseTitleFixture : CoreTest
    {
        // Parser.ParseMovieTitle's release-group extraction step replaces whatever a matching
        // regex captured as the "title" group with a generic "A.Movie"/"A Movie" placeholder in
        // SimpleReleaseTitle, to keep movie-title words out of release-group detection. For scene
        // releases this can swallow format-relevant tokens (e.g. "iMAGESET") that happen to fall
        // inside that captured span. ReleaseTitleSpecification must fall back to the raw,
        // unredacted ReleaseTitle so custom formats can still match those tokens.
        [TestCase("SweetSophieMoone.com 08.05.30.Sophie.Moone.Presents.For.Sophie.Part.7.XXX.iMAGESET-P4L", "imageset")]
        [TestCase("Intimates_.Intimates.2020.11.20.AJ.Applegate.Come.Home.To.A.J", "come.home.to.a.j")]
        public void should_match_scene_release_title_even_when_simple_release_title_is_redacted(string title, string pattern)
        {
            var parsed = Parser.Parser.ParseMovieTitle(title);
            parsed.IsScene.Should().BeTrue();

            var spec = new ReleaseTitleSpecification
            {
                Name = "test",
                Value = pattern
            };

            var input = new CustomFormatInput { MovieInfo = parsed };

            spec.IsSatisfiedBy(input).Should().BeTrue();
        }
    }
}
