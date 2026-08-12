using System;
using System.Collections.Generic;
using FizzWare.NBuilder;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.MetadataSource;
using NzbDrone.Core.Movies.Studios;
using NzbDrone.Core.Movies.Studios.Commands;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.MovieTests
{
    [TestFixture]
    public class RefreshStudioServiceFixture : CoreTest<RefreshStudioService>
    {
        private Studio _failingStudio;
        private Studio _succeedingStudio;

        [SetUp]
        public void Setup()
        {
            _failingStudio = Builder<Studio>.CreateNew()
                .With(s => s.Id = 101)
                .With(s => s.ForeignId = "failing-studio")
                .With(s => s.LastInfoSync = DateTime.UtcNow.AddDays(-30))
                .With(s => s.Monitored = false)
                .With(s => s.MoviesMonitored = false)
                .Build();

            _succeedingStudio = Builder<Studio>.CreateNew()
                .With(s => s.Id = 102)
                .With(s => s.ForeignId = "succeeding-studio")
                .With(s => s.LastInfoSync = DateTime.UtcNow.AddDays(-30))
                .With(s => s.Monitored = false)
                .With(s => s.MoviesMonitored = false)
                .Build();

            Mocker.GetMock<IStudioService>()
                .Setup(s => s.GetAllStudios())
                .Returns(new List<Studio> { _failingStudio, _succeedingStudio });

            Mocker.GetMock<IStudioService>()
                .Setup(s => s.GetById(_failingStudio.Id))
                .Returns(_failingStudio);

            Mocker.GetMock<IStudioService>()
                .Setup(s => s.GetById(_succeedingStudio.Id))
                .Returns(_succeedingStudio);

            // Simulates a transient failure (network timeout, malformed response, etc.)
            // that is not a MovieNotFoundException.
            Mocker.GetMock<IProvideMovieInfo>()
                .Setup(s => s.GetStudioInfo(_failingStudio.ForeignId))
                .Throws(new InvalidOperationException("boom"));

            Mocker.GetMock<IProvideMovieInfo>()
                .Setup(s => s.GetStudioInfo(_succeedingStudio.ForeignId))
                .Returns(new Studio
                {
                    ForeignId = _succeedingStudio.ForeignId,
                    Title = "Updated Title"
                });
        }

        [Test]
        public void should_continue_refreshing_remaining_studios_after_a_non_MovieNotFoundException_error()
        {
            Subject.Execute(new RefreshStudiosCommand());

            // The failing studio's refresh error is expected -- that's what this test is verifying is handled gracefully.
            ExceptionVerification.IgnoreErrors();

            Mocker.GetMock<IProvideMovieInfo>()
                .Verify(s => s.GetStudioInfo(_failingStudio.ForeignId), Times.Once());

            Mocker.GetMock<IProvideMovieInfo>()
                .Verify(s => s.GetStudioInfo(_succeedingStudio.ForeignId), Times.Once());

            Mocker.GetMock<IStudioService>()
                .Verify(s => s.Update(It.Is<Studio>(x => x.ForeignId == _succeedingStudio.ForeignId)), Times.Once());
        }
    }
}
