using FluentAssertions;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Foundation.Abstractions.Interfaces.Events.Routing;
using HydroGarden.Foundation.Common.Events.Adapters;
using HydroGarden.Foundation.ErrorHandling;
using HydroGarden.Foundation.ErrorHandling.Events;
using HydroGarden.Logger.Abstractions;
using Moq;
using Xunit;

namespace HydroGarden.Foundation.Tests.Integration.ErrorHandling
{
    /// <summary>
    /// Integration tests that verify the interaction between EventBus and error handling components.
    /// </summary>
    public class EventBusErrorHandlingIntegrationTests
    {
        private readonly Mock<ILogger> _mockLogger;
        private readonly Mock<IEventStore> _mockEventStore;
        private readonly Mock<IEventRouter> _mockEventRouter;
        private readonly Common.Events.EventBus _eventBus;
        private readonly ErrorMonitor _errorMonitor;
        private readonly ErrorEventTransformationService _transformationService;
        private readonly List<ErrorOccurredEvent> _publishedErrorEvents = new();
        
        public EventBusErrorHandlingIntegrationTests()
        {
            _mockLogger = new Mock<ILogger>();
            _mockEventStore = new Mock<IEventStore>();
            _mockEventRouter = new Mock<IEventRouter>();
            
            // Set up router to pass through all subscriptions
            _mockEventRouter.Setup(r => r.GetMatchingSubscriptionsAsync(
                It.IsAny<IEvent>(), 
                It.IsAny<IEnumerable<IEventSubscription>>(), 
                It.IsAny<CancellationToken>()))
                .Returns<IEvent, IEnumerable<IEventSubscription>, CancellationToken>((_, subs, _) => 
                    Task.FromResult<IReadOnlyList<IEventSubscription>>(subs.ToList()));
            
            // Set up event store to handle persist calls
            _mockEventStore
                .Setup(s => s.PersistEventAsync(It.IsAny<IEvent>()))
                .Returns(Task.CompletedTask);
            
            // Create the event bus - constructor is (ILogger, IEventRouter, IEventStore?, IEventTransformer?)
            _eventBus = new Common.Events.EventBus(
                _mockLogger.Object,
                _mockEventRouter.Object,
                _mockEventStore.Object,
                null);
                
            // Create error handling components
            _transformationService = new ErrorEventTransformationService(
                _eventBus,
                _mockLogger.Object);
                
            _errorMonitor = new ErrorMonitor(
                _mockLogger.Object,
                _transformationService);
        }

        [Fact]
        public async Task EventBus_ShouldPublishErrorEventsWhenHandlersFail()
        {
            // Arrange
            var eventId = Guid.NewGuid();
            var sourceId = Guid.NewGuid();

            // Create a test event
            var testEvent = new Mock<IEvent>();
            testEvent.Setup(e => e.EventId).Returns(eventId);
            testEvent.Setup(e => e.EventType).Returns(EventType.Command);
            testEvent.Setup(e => e.SourceId).Returns(sourceId);
            testEvent.Setup(e => e.Timestamp).Returns(DateTime.UtcNow);

            // Create a handler that will throw an exception
            var mockHandler = new Mock<IEventHandler>();
            mockHandler
                .Setup(h => h.HandleEventAsync(
                    It.IsAny<object>(),
                    It.IsAny<IEvent>(),
                    It.IsAny<CancellationToken>()))
                .Throws(new InvalidOperationException("Simulated handler failure"));

            // Subscribe the handler
            var adapter = new GenericEventHandlerAdapter<IEvent>(mockHandler.Object);
            _eventBus.Subscribe<IEvent>(adapter);

            // Set up event store to capture persisted events
            var persistedEvents = new List<IEvent>();
            _mockEventStore
                .Setup(s => s.PersistEventAsync(It.IsAny<IEvent>()))
                .Callback<IEvent>(evt => persistedEvents.Add(evt))
                .Returns(Task.CompletedTask);

            // Act
            var publishTask = _eventBus.PublishAsync(this, testEvent.Object);
            var result = await Task.WhenAny(publishTask, Task.Delay(5000)) == publishTask ? publishTask.Result : null;

            // Assert
            result.Should().NotBeNull();
            result!.EventId.Should().Be(eventId);
            result.HandlerCount.Should().Be(2);
            result.SuccessCount.Should().Be(0);
            result.HasErrors.Should().BeTrue();
            result.Errors.Should().ContainSingle(e => e is InvalidOperationException);

            // Event should be persisted for retry
            _mockEventStore.Verify(
                s => s.PersistEventAsync(
                    It.Is<IEvent>(e => e.EventId == eventId)),
                Times.Once);

            persistedEvents.Should().ContainSingle(e => e.EventId == eventId);
        }

        [Fact]
        public async Task ErrorCorrelation_ShouldLinkRelatedErrors()
        {
            // Arrange
            var sourceId = Guid.NewGuid();
            var correlationId = Guid.NewGuid();
            
            // Create an original error
            var originalError = new ComponentError(
                sourceId,
                "ORIGINAL_ERROR",
                "Original error message",
                ErrorSeverity.Error,
                ErrorSource.Device,
                correlationId: correlationId);
            
            // Track published events
            var publishedEvents = new List<IEvent>();
            var errorEvents = new List<ErrorOccurredEvent>();

            // Mock the event bus's PublishAsync to capture published events
            var mockEventBus = new Mock<IEventBus>();
            mockEventBus
                .Setup(b => b.PublishAsync(It.IsAny<object>(), It.IsAny<IEvent>(), It.IsAny<CancellationToken>()))
                .Callback<object, IEvent, CancellationToken>((_, evt, _) =>
                {
                    publishedEvents.Add(evt);
                    if (evt is ErrorOccurredEvent errorEvent)
                    {
                        errorEvents.Add(errorEvent);
                    }
                })
                .ReturnsAsync(new Common.Events.PublishResult { EventId = Guid.NewGuid(), SuccessCount = 1 });

            // Create a transformation service with our mocked event bus
            var transformationService = new ErrorEventTransformationService(
                mockEventBus.Object,
                _mockLogger.Object);
                
            // Create an error monitor with our mocked transformation service
            var errorMonitor = new ErrorMonitor(
                _mockLogger.Object,
                transformationService);
            
            // Act - Report the original error
            await errorMonitor.ReportErrorAsync(originalError);
            
            // Generate a related error with the same correlation ID
            var relatedError = new ComponentError(
                sourceId,
                "RELATED_ERROR",
                "Related error caused by handling original error",
                ErrorSeverity.Warning,
                ErrorSource.Device,
                correlationId: correlationId);
                
            // Report the related error
            await errorMonitor.ReportErrorAsync(relatedError);
            
            // Get correlated errors from the error monitor
            var correlatedErrors = errorMonitor.GetCorrelatedErrors(correlationId);
            
            // Assert
            // Verify that events were published to the event bus
            mockEventBus.Verify(
                b => b.PublishAsync(It.IsAny<object>(), It.IsAny<IEvent>(), It.IsAny<CancellationToken>()),
                Times.Exactly(2));
                
            errorEvents.Should().HaveCount(2, "Two error events should have been published");
            
            // Events should include both the original and related error
            errorEvents.Should().Contain(e => e.ErrorData.ErrorCode == "ORIGINAL_ERROR");
            errorEvents.Should().Contain(e => e.ErrorData.ErrorCode == "RELATED_ERROR");
            
            // Should have found at least the original error plus a related one from the error monitor
            correlatedErrors.Should().HaveCount(2, "Error monitor should track both errors");
            correlatedErrors.Should().Contain(e => e.ErrorCode == "ORIGINAL_ERROR");
            correlatedErrors.Should().Contain(e => e.ErrorCode == "RELATED_ERROR");
            
            // The related error should have the same correlation ID
            correlatedErrors.Should().OnlyContain(e => e.CorrelationId == correlationId);
        }
    }
}