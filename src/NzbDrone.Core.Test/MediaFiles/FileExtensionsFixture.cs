using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MediaFiles
{
    [TestFixture]
    public class FileExtensionsFixture : CoreTest
    {
        [Test]
        public void should_return_null_when_title_is_null()
        {
            FileExtensions.RemoveFileExtension(null).Should().BeNull();
        }

        [Test]
        public void should_return_empty_when_title_is_empty()
        {
            FileExtensions.RemoveFileExtension(string.Empty).Should().BeEmpty();
        }

        [Test]
        public void should_remove_known_media_extension()
        {
            FileExtensions.RemoveFileExtension("Some.Release.Title.mkv").Should().Be("Some.Release.Title");
        }

        [Test]
        public void should_not_remove_unknown_extension()
        {
            FileExtensions.RemoveFileExtension("Some.Release.Title.xyz").Should().Be("Some.Release.Title.xyz");
        }
    }
}
