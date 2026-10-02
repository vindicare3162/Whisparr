using System;
using System.Collections.Generic;
using System.IO;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.MovieImport.Manual;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.MediaFiles.MovieImport.Manual
{
    [TestFixture]
    public class ManualImportServiceFixture : CoreTest<ManualImportService>
    {
        private Movie _movie;
        private MovieFile _movieFile;

        [SetUp]
        public void Setup()
        {
            _movie = Builder<Movie>.CreateNew()
                                   .With(m => m.Path = @"C:\Test\Movies\Movie Title".AsOsAgnostic())
                                   .Build();

            _movieFile = Builder<MovieFile>.CreateNew()
                                           .With(f => f.MovieId = _movie.Id)
                                           .With(f => f.RelativePath = "Movie Title.mkv")
                                           .Build();

            Mocker.GetMock<IMovieService>()
                  .Setup(s => s.GetMovie(_movie.Id))
                  .Returns(_movie);

            Mocker.GetMock<IMediaFileService>()
                  .Setup(s => s.GetFilesByMovie(_movie.Id))
                  .Returns(new List<MovieFile> { _movieFile });

            Mocker.GetMock<ICustomFormatCalculationService>()
                  .Setup(s => s.ParseCustomFormat(It.IsAny<MovieFile>(), It.IsAny<Movie>()))
                  .Returns(new List<CustomFormat>());

            // Mirror DiskProviderBase / Directory enumeration: a missing file or folder throws.
            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.GetFileSize(It.IsAny<string>()))
                  .Throws<FileNotFoundException>();

            Mocker.GetMock<IDiskScanService>()
                  .Setup(s => s.GetVideoFiles(It.IsAny<string>(), It.IsAny<bool>()))
                  .Throws<DirectoryNotFoundException>();
        }

        private void GivenFolderExists()
        {
            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.FolderExists(_movie.Path))
                  .Returns(true);

            Mocker.GetMock<IDiskScanService>()
                  .Setup(s => s.GetVideoFiles(_movie.Path, It.IsAny<bool>()))
                  .Returns(Array.Empty<string>());

            Mocker.GetMock<IDiskScanService>()
                  .Setup(s => s.FilterPaths(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<bool>()))
                  .Returns(new List<string>());
        }

        [Test]
        public void should_not_throw_when_movie_folder_is_missing()
        {
            var result = Subject.GetMediaFiles(_movie.Id);

            result.Should().HaveCount(1);
            result[0].MovieFileId.Should().Be(_movieFile.Id);
            result[0].Size.Should().Be(0);

            Mocker.GetMock<IDiskScanService>()
                  .Verify(v => v.GetVideoFiles(It.IsAny<string>(), It.IsAny<bool>()), Times.Never());
        }

        [Test]
        public void should_report_zero_size_when_file_is_missing_but_folder_exists()
        {
            GivenFolderExists();

            var result = Subject.GetMediaFiles(_movie.Id);

            result.Should().HaveCount(1);
            result[0].Size.Should().Be(0);
        }

        [Test]
        public void should_use_file_size_when_file_exists()
        {
            GivenFolderExists();

            var path = Path.Combine(_movie.Path, _movieFile.RelativePath);

            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.FileExists(path))
                  .Returns(true);

            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.GetFileSize(path))
                  .Returns(1234);

            var result = Subject.GetMediaFiles(_movie.Id);

            result.Should().HaveCount(1);
            result[0].Size.Should().Be(1234);
        }
    }
}
