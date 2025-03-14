using FluentAssertions;
using HydroGarden.Foundation.Abstractions.Interfaces;
using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Foundation.Abstractions.Interfaces.Events.Routing;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.Abstractions.Interfaces.Services;
using HydroGarden.Foundation.Common.Events.Pipeline;
using HydroGarden.Logger.Abstractions;
using Moq;
using Xunit;

namespace HydroGarden.Foundation.Tests.Unit.EventBus
{
    public class EventBusInitializationTests : EventBusBaseTests
    {
        [Fact]
        public void EventBus_ShouldInitializeWithAllDependencies()
        {
            // Arrange & Act
            using var eventBus = new Common.Events.EventBus(
                MockLogger.Object,
                MockEventRouter.Object,
                MockTopologyService.Object,
                MockStore.Object,
                MockRetryPolicy.Object,
                MockTransformer.Object);

            // Assert
            eventBus.Should().NotBeNull();
            eventBus.GetTopologyService().Should().Be(MockTopologyService.Object);
        }

        [Fact]
        public void EventBus_ShouldThrowWithNullLogger()
        {
            // Arrange & Act & Assert
            Action act = () => new Common.Events.EventBus(
                null!,
                MockEventRouter.Object,
                MockTopologyService.Object,
                MockStore.Object,
                MockRetryPolicy.Object,
                MockTransformer.Object);

            act.Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("logger");
        }

        [Fact]
        public void EventBus_ShouldThrowWithNullRouter()
        {
            // Arrange & Act & Assert
            Action act = () => new Common.Events.EventBus(
                MockLogger.Object,
                null!,
                MockTopologyService.Object,
                MockStore.Object,
                MockRetryPolicy.Object,
                MockTransformer.Object);

            act.Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("router");
        }

        [Fact]
        public void EventBus_ShouldAllowNullOptionalDependencies()
        {
            // Arrange & Act
            using var eventBus = new Common.Events.EventBus(
                MockLogger.Object,
                MockEventRouter.Object);

            // Assert
            eventBus.Should().NotBeNull();
            eventBus.GetTopologyService().Should().BeNull();
        }
    }

    public class EventBusPipelineTests : EventBusBaseTests
    {
        [Fact]
        public async Task EventBus_ShouldUsePipelineForProcessingIfConfigured()
        {
            // Arrange
            var sourceId = Guid.NewGuid();
            var eventId = Guid.NewGuid();

            // Set up a successful pipeline result
            var mockPipelineResult = new Mock<IEventProcessingResult>();
            mockPipelineResult.Setup(r => r.IsSuccess).Returns(true);

            // Set up the pipeline to return success
            MockPipeline
                .Setup(p => p.ProcessEventAsync(
                    It.IsAny<object>(),
                    It.IsAny<IEvent>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockPipelineResult.Object);

            // Create the event bus and set the pipeline
            using var eventBus = CreateTestEventBus();
            eventBus.SetEventProcessingPipeline(MockPipeline.Object);

            // Create a handler that should NOT be called if pipeline succeeds
            var mockHandler = new Mock<IEventHandler>();
            mockHandler.Setup(h => h.HandleEventAsync(
                It.IsAny<object>(),
                It.IsAny<IEvent>(),
                It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Subscribe the handler
            eventBus.Subscribe(mockHandler.Object);

            // Create a test event
            var testEvent = new Mock<IEvent>();
            testEvent.Setup(e => e.EventId).Returns(eventId);
            testEvent.Setup(e => e.EventType).Returns(EventType.Command);
            testEvent.Setup(e => e.SourceId).Returns(sourceId);
            testEvent.Setup(e => e.Timestamp).Returns(DateTime.UtcNow);

            // Act
            var result = await eventBus.PublishAsync(this, testEvent.Object, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result!.EventId.Should().Be(eventId);
            result.SuccessCount.Should().Be(1); // Pipeline counts as a handler

            // Verify pipeline was used
            MockPipeline.Verify(p => p.ProcessEventAsync(
                It.IsAny<object>(),
                It.Is<IEvent>(e => e.EventId == eventId),
                It.IsAny<CancellationToken>()),
                Times.Once);

            // Verify regular handler was not called
            mockHandler.Verify(h => h.HandleEventAsync(
                It.IsAny<object>(),
                It.IsAny<IEvent>(),
                It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task EventBus_ShouldFallbackToRegularHandlersWhenPipelineFails()
        {
            // Arrange
            var sourceId = Guid.NewGuid();
            var eventId = Guid.NewGuid();

            // Set up a failed pipeline result
            var mockPipelineResult = new Mock<IEventProcessingResult>();
            mockPipelineResult.Setup(r => r.IsSuccess).Returns(false);
            mockPipelineResult.Setup(r => r.Exception).Returns(new InvalidOperationException("Pipeline failed"));

            // Set up the pipeline to return failure
            MockPipeline
                .Setup(p => p.ProcessEventAsync(
                    It.IsAny<object>(),
                    It.IsAny<IEvent>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockPipelineResult.Object);

            // Create the event bus and set the pipeline
            using var eventBus = CreateTestEventBus();
            eventBus.SetEventProcessingPipeline(MockPipeline.Object);

            // Create a handler that SHOULD be called when pipeline fails
            var mockHandler = new Mock<IEventHandler>();
            mockHandler.Setup(h => h.HandleEventAsync(
                It.IsAny<object>(),
                It.IsAny<IEvent>(),
                It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Set up router to include our handler
            MockEventRouter
                .Setup(r => r.GetMatchingSubscriptionsAsync(
                    It.IsAny<IEvent>(),
                    It.IsAny<IEnumerable<IEventSubscription>>(),
                    It.IsAny<CancellationToken>()))
                .Returns<IEvent, IEnumerable<IEventSubscription>, CancellationToken>((_, subs, _) =>
                    Task.FromResult<IReadOnlyList<IEventSubscription>>(subs.ToList()));

            // Subscribe the handler
            eventBus.Subscribe(mockHandler.Object);

            // Create a test event
            var testEvent = new Mock<IEvent>();
            testEvent.Setup(e => e.EventId).Returns(eventId);
            testEvent.Setup(e => e.EventType).Returns(EventType.Command);
            testEvent.Setup(e => e.SourceId).Returns(sourceId);
            testEvent.Setup(e => e.Timestamp).Returns(DateTime.UtcNow);

            // Act
            var result = await eventBus.PublishAsync(this, testEvent.Object, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result!.EventId.Should().Be(eventId);
            result.HandlerCount.Should().Be(1); // Regular handler count
            result.SuccessCount.Should().Be(1); // Regular handler was successful

            // Verify pipeline was used
            MockPipeline.Verify(p => p.ProcessEventAsync(
                It.IsAny<object>(),
                It.Is<IEvent>(e => e.EventId == eventId),
                It.IsAny<CancellationToken>()),
                Times.Once);

            // Verify regular handler was called
            mockHandler.Verify(h => h.HandleEventAsync(
                It.IsAny<object>(),
                It.IsAny<IEvent>(),
                It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }

    public abstract class EventBusBaseTests
    {
        protected readonly Mock<ILogger> MockLogger;
        protected readonly Mock<IEventStore> MockStore;
        protected readonly Mock<IEventRetryPolicy> MockRetryPolicy;
        protected readonly Mock<IEventTransformer> MockTransformer;
        protected readonly Mock<ITopologyService> MockTopologyService;
        protected readonly Mock<IErrorMonitor> MockErrorMonitor;
        protected readonly Mock<IEventRouter> MockEventRouter;
        protected readonly Mock<IEventProcessingPipeline> MockPipeline;

        protected EventBusBaseTests()
        {
            MockLogger = new Mock<ILogger>();
            MockStore = new Mock<IEventStore>();
            MockRetryPolicy = new Mock<IEventRetryPolicy>();
            MockTransformer = new Mock<IEventTransformer>();
            MockTopologyService = new Mock<ITopologyService>();
            MockErrorMonitor = new Mock<IErrorMonitor>();
            MockEventRouter = new Mock<IEventRouter>();
            MockPipeline = new Mock<IEventProcessingPipeline>();

            MockTransformer.Setup(t => t.Transform(It.IsAny<IEvent>()))
                .Returns<IEvent>(e => e);

            // Set up default behavior for the router to pass through all subscriptions
            MockEventRouter.Setup(r => r.GetMatchingSubscriptionsAsync(
                It.IsAny<IEvent>(), 
                It.IsAny<IEnumerable<IEventSubscription>>(), 
                It.IsAny<CancellationToken>()))
                .Returns<IEvent, IEnumerable<IEventSubscription>, CancellationToken>((_, subs, _) => 
                    Task.FromResult<IReadOnlyList<IEventSubscription>>(subs.ToList()));
        }

        protected Common.Events.EventBus CreateTestEventBus(
            bool withStore = false,
            bool withRetryPolicy = false,
            bool withTransformer = false,
            bool withTopologyService = true)
        {
            var eventBus = new Common.Events.EventBus(
                MockLogger.Object,
                MockEventRouter.Object,
                withTopologyService ? MockTopologyService.Object : null,
                withStore ? MockStore.Object : null,
                withRetryPolicy ? MockRetryPolicy.Object : null,
                withTransformer ? MockTransformer.Object : null);
            
            return eventBus;
        }
    }
}