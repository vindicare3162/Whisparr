using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.IndexerSearch;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Movies.AlternativeTitles;
using NzbDrone.Core.Movies.Credits;
using NzbDrone.Core.Movies.Studios;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.IndexerSearchTests
{
        public class ReleaseSearchServiceFixture : CoreTest<ReleaseSearchService>
    {
        private Mock<IIndexer> _mockIndexer;
        private Movie _movie;

        [SetUp]
        public void SetUp()
        {
            _mockIndexer = Mocker.GetMock<IIndexer>();
            _mockIndexer.SetupGet(s => s.Definition).Returns(new IndexerDefinition { Id = 1 });
            _mockIndexer.SetupGet(s => s.SupportsSearch).Returns(true);

            Mocker.GetMock<IIndexerFactory>()
                  .Setup(s => s.AutomaticSearchEnabled(true))
                  .Returns(new List<IIndexer> { _mockIndexer.Object });

            Mocker.GetMock<IMakeDownloadDecision>()
                .Setup(s => s.GetSearchDecision(It.IsAny<List<Parser.Model.ReleaseInfo>>(), It.IsAny<SearchCriteriaBase>()))
                .Returns(new List<DownloadDecision>());

            _movie = Builder<Movie>.CreateNew()
                .With(v => v.Monitored = true)
                .Build();

            Mocker.GetMock<IMovieService>()
                .Setup(v => v.GetMovie(_movie.Id))
                .Returns(_movie);
        }

        private List<SearchCriteriaBase> WatchForSearchCriteria()
        {
            var result = new List<SearchCriteriaBase>();

            _mockIndexer.Setup(v => v.Fetch(It.IsAny<MovieSearchCriteria>()))
                .Callback<MovieSearchCriteria>(s => result.Add(s))
                .Returns(Task.FromResult<IList<Parser.Model.ReleaseInfo>>(new List<Parser.Model.ReleaseInfo>()));

            return result;
        }

        [Test]
        public async Task Tags_IndexerTags_MovieNoTags_IndexerNotIncluded()
        {
            _mockIndexer.SetupGet(s => s.Definition).Returns(new IndexerDefinition
            {
                Id = 1,
                Tags = new HashSet<int> { 3 }
            });

            var allCriteria = WatchForSearchCriteria();

            await Subject.MovieSearch(_movie, true, false);

            var criteria = allCriteria.OfType<MovieSearchCriteria>().ToList();

            criteria.Count.Should().Be(0);
        }

        [Test]
        public async Task Tags_IndexerNoTags_MovieTags_IndexerIncluded()
        {
            _mockIndexer.SetupGet(s => s.Definition).Returns(new IndexerDefinition
            {
                Id = 1
            });

            _movie = Builder<Movie>.CreateNew()
                .With(v => v.Monitored = true)
                .With(v => v.Tags = new HashSet<int> { 3 })
                .Build();

            Mocker.GetMock<IMovieService>()
                .Setup(v => v.GetMovie(_movie.Id))
                .Returns(_movie);

            var allCriteria = WatchForSearchCriteria();

            await Subject.MovieSearch(_movie, true, false);

            var criteria = allCriteria.OfType<MovieSearchCriteria>().ToList();

            criteria.Count.Should().Be(1);
        }

        [Test]
        public async Task Tags_IndexerAndMovieTagsMatch_IndexerIncluded()
        {
            _mockIndexer.SetupGet(s => s.Definition).Returns(new IndexerDefinition
            {
                Id = 1,
                Tags = new HashSet<int> { 1, 2, 3 }
            });

            _movie = Builder<Movie>.CreateNew()
                .With(v => v.Monitored = true)
                .With(v => v.Tags = new HashSet<int> { 3, 4, 5 })
                .Build();

            Mocker.GetMock<IMovieService>()
                .Setup(v => v.GetMovie(_movie.Id))
                .Returns(_movie);

            var allCriteria = WatchForSearchCriteria();

            await Subject.MovieSearch(_movie, true, false);

            var criteria = allCriteria.OfType<MovieSearchCriteria>().ToList();

            criteria.Count.Should().Be(1);
        }

        [Test]
        public async Task Tags_IndexerAndMovieTagsMismatch_IndexerNotIncluded()
        {
            _mockIndexer.SetupGet(s => s.Definition).Returns(new IndexerDefinition
            {
                Id = 1,
                Tags = new HashSet<int> { 1, 2, 3 }
            });

            _movie = Builder<Movie>.CreateNew()
                .With(v => v.Monitored = true)
                .With(v => v.Tags = new HashSet<int> { 4, 5, 6 })
                .Build();

            Mocker.GetMock<IMovieService>()
                .Setup(v => v.GetMovie(_movie.Id))
                .Returns(_movie);

            var allCriteria = WatchForSearchCriteria();

            await Subject.MovieSearch(_movie, true, false);

            var criteria = allCriteria.OfType<MovieSearchCriteria>().ToList();

            criteria.Count.Should().Be(0);
        }

        private Movie GivenScene()
        {
            var scene = Builder<Movie>.CreateNew()
                .With(v => v.Monitored = true)
                .With(v => v.Title = "Some Scene Title")
                .With(v => v.MovieMetadata.Value.ItemType = ItemType.Scene)
                .With(v => v.MovieMetadata.Value.Title = "Some Scene Title")
                .With(v => v.MovieMetadata.Value.AlternativeTitles = new List<AlternativeTitle>())
                .With(v => v.MovieMetadata.Value.ReleaseDateUtc = DateTime.UtcNow)
                .With(v => v.MovieMetadata.Value.StudioTitle = "Some Studio")
                .With(v => v.MovieMetadata.Value.Code = "SCN-123")
                .With(v => v.MovieMetadata.Value.Credits = new List<Credit>
                {
                    new Credit { Performer = new CreditPerformer { Name = "Performer One" } }
                })
                .Build();

            Mocker.GetMock<IMovieService>()
                .Setup(v => v.GetMovie(scene.Id))
                .Returns(scene);

            Mocker.GetMock<IStudioService>()
                .Setup(v => v.FindAllByTitle(It.IsAny<string>()))
                .Returns(new List<Studio>());

            return scene;
        }

        private List<SceneSearchCriteria> WatchForSceneSearchCriteria()
        {
            var result = new List<SceneSearchCriteria>();

            _mockIndexer.Setup(v => v.Fetch(It.IsAny<SceneSearchCriteria>()))
                .Callback<SceneSearchCriteria>(s => result.Add(s))
                .Returns(Task.FromResult<IList<ReleaseInfo>>(new List<ReleaseInfo>()));

            return result;
        }

        [Test]
        public async Task SceneSearch_PrimaryTierAccepted_FallbackNotDispatched()
        {
            var scene = GivenScene();

            Mocker.GetMock<IMakeDownloadDecision>()
                .Setup(s => s.GetSearchDecision(It.IsAny<List<ReleaseInfo>>(), It.IsAny<SearchCriteriaBase>()))
                .Returns(new List<DownloadDecision> { new DownloadDecision(new RemoteMovie { Movie = scene, Release = new ReleaseInfo() }) });

            var allCriteria = WatchForSceneSearchCriteria();

            await Subject.MovieSearch(scene, true, false);

            allCriteria.Count.Should().Be(1);
        }

        [Test]
        public async Task SceneSearch_PrimaryTierEmpty_FallbackDispatchedWithBareTitleCodeAndPerformer()
        {
            var scene = GivenScene();

            Mocker.GetMock<IMakeDownloadDecision>()
                .Setup(s => s.GetSearchDecision(It.IsAny<List<ReleaseInfo>>(), It.IsAny<SearchCriteriaBase>()))
                .Returns(new List<DownloadDecision>());

            var allCriteria = WatchForSceneSearchCriteria();

            await Subject.MovieSearch(scene, true, false);

            allCriteria.Count.Should().Be(2);

            var fallbackTitles = allCriteria[1].SceneTitles;

            fallbackTitles.Should().Contain(scene.Title);
            fallbackTitles.Should().Contain(scene.MovieMetadata.Value.Code);
            fallbackTitles.Should().Contain("Performer One Some Scene Title");
        }
    }
}
