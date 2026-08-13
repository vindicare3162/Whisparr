using System;
using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Jobs;
using NzbDrone.Core.Lifecycle;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Update.Commands;

namespace NzbDrone.Core.Test.Jobs
{
    [TestFixture]
    public class TaskManagerFixture : CoreTest<TaskManager>
    {
        [SetUp]
        public void Setup()
        {
            Mocker.GetMock<IScheduledTaskRepository>()
                .Setup(s => s.All())
                .Returns(new List<ScheduledTask>());
        }

        [Test]
        public void should_not_schedule_the_application_check_update_task()
        {
            Subject.Handle(new ApplicationStartedEvent());

            Subject.GetAll().Should().NotContain(t => t.TypeName == typeof(ApplicationCheckUpdateCommand).FullName);
        }

        [Test]
        public void should_remove_an_existing_application_check_update_task_from_the_database()
        {
            var staleTask = new ScheduledTask
            {
                Id = 42,
                TypeName = typeof(ApplicationCheckUpdateCommand).FullName,
                Interval = 360,
                LastExecution = DateTime.UtcNow
            };

            Mocker.GetMock<IScheduledTaskRepository>()
                .Setup(s => s.All())
                .Returns(new List<ScheduledTask> { staleTask });

            Subject.Handle(new ApplicationStartedEvent());

            Mocker.GetMock<IScheduledTaskRepository>()
                .Verify(s => s.Delete(staleTask.Id), Times.Once());
        }
    }
}
