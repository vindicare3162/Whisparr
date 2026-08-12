using System;
using System.Collections.Generic;
using FizzWare.NBuilder;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.MetadataSource;
using NzbDrone.Core.Movies.Performers;
using NzbDrone.Core.Movies.Performers.Commands;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.MovieTests
{
    [TestFixture]
    public class RefreshPerformerServiceFixture : CoreTest<RefreshPerformerService>
    {
        private Performer _failingPerformer;
        private Performer _succeedingPerformer;

        [SetUp]
        public void Setup()
        {
            _failingPerformer = Builder<Performer>.CreateNew()
                .With(p => p.Id = 101)
                .With(p => p.ForeignId = "failing-performer")
                .With(p => p.LastInfoSync = DateTime.UtcNow.AddDays(-30))
                .With(p => p.Monitored = false)
                .With(p => p.MoviesMonitored = false)
                .Build();

            _succeedingPerformer = Builder<Performer>.CreateNew()
                .With(p => p.Id = 102)
                .With(p => p.ForeignId = "succeeding-performer")
                .With(p => p.LastInfoSync = DateTime.UtcNow.AddDays(-30))
                .With(p => p.Monitored = false)
                .With(p => p.MoviesMonitored = false)
                .Build();

            Mocker.GetMock<IPerformerService>()
                .Setup(s => s.GetAllPerformers())
                .Returns(new List<Performer> { _failingPerformer, _succeedingPerformer });

            Mocker.GetMock<IPerformerService>()
                .Setup(s => s.GetById(_failingPerformer.Id))
                .Returns(_failingPerformer);

            Mocker.GetMock<IPerformerService>()
                .Setup(s => s.GetById(_succeedingPerformer.Id))
                .Returns(_succeedingPerformer);

            // Simulates a transient failure (network timeout, malformed response, etc.)
            // that is not a MovieNotFoundException.
            Mocker.GetMock<IProvideMovieInfo>()
                .Setup(s => s.GetPerformerInfo(_failingPerformer.ForeignId))
                .Throws(new InvalidOperationException("boom"));

            Mocker.GetMock<IProvideMovieInfo>()
                .Setup(s => s.GetPerformerInfo(_succeedingPerformer.ForeignId))
                .Returns(new Performer
                {
                    ForeignId = _succeedingPerformer.ForeignId,
                    Name = "Updated Name"
                });
        }

        [Test]
        public void should_continue_refreshing_remaining_performers_after_a_non_MovieNotFoundException_error()
        {
            Subject.Execute(new RefreshPerformersCommand());

            // The failing performer's refresh error is expected -- that's what this test is verifying is handled gracefully.
            ExceptionVerification.IgnoreErrors();

            Mocker.GetMock<IProvideMovieInfo>()
                .Verify(s => s.GetPerformerInfo(_failingPerformer.ForeignId), Times.Once());

            Mocker.GetMock<IProvideMovieInfo>()
                .Verify(s => s.GetPerformerInfo(_succeedingPerformer.ForeignId), Times.Once());

            Mocker.GetMock<IPerformerService>()
                .Verify(s => s.Update(It.Is<Performer>(p => p.ForeignId == _succeedingPerformer.ForeignId)), Times.Once());
        }
    }
}
