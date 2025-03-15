using FluentAssertions;
using HydroGarden.ErrorHandling.Core;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Foundation.Abstractions.Interfaces.Events.Routing;
using HydroGarden.Foundation.Common.Events;
using HydroGarden.Foundation.Common.Events.Pipeline;
using HydroGarden.Foundation.Common.Events.Pipeline.Middleware;
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
            
            // Create the event bus
            _eventBus = new Common.Events.EventBus(
                _mockLogger.Object,
                _mockEventRouter.Object,
                null,
                _mockEventStore.Object);
                
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
            _eventBus.Subscribe(mockHandler.Object);
            
            // Set up event store to capture persisted events
            var persistedEvents = new List<IEvent>();
            _mockEventStore
                .Setup(s => s.PersistEventAsync(It.IsAny<IEvent>()))
                .Callback<IEvent>(evt => persistedEvents.Add(evt))
                .Returns(Task.CompletedTask);
            
            // Act
            var result = await _eventBus.PublishAsync(this, testEvent.Object);
            
            // Assert
            result.Should().NotBeNull();
            result!.EventId.Should().Be(eventId);
            result.HandlerCount.Should().Be(1);
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
        public async Task ErrorHandlingMiddleware_ShouldCaptureAndReportErrors()
        {
            // Arrange
            var sourceId = Guid.NewGuid();
            var eventId = Guid.NewGuid();

            // Create a test event
            var testEvent = new Mock<IEvent>();
            testEvent.Setup(e => e.EventId).Returns(eventId);
            testEvent.Setup(e => e.EventType).Returns(EventType.Command);
            testEvent.Setup(e => e.SourceId).Returns(sourceId);
            testEvent.Setup(e => e.Timestamp).Returns(DateTime.UtcNow);
            
            // Create a handler that will throw an exception which will be reported to error monitor
            var mockHandler = new Mock<IEventHandler>();
            _ = mockHandler
                .Setup(h => h.HandleEventAsync(
                    It.IsAny<object>(),
                    It.IsAny<IEvent>(),
                    It.IsAny<CancellationToken>()))
                .Callback<object, IEvent, CancellationToken>(async (sender, evt, ct) =>
                {
                    // Report error to the error monitor directly
                    var error = new ComponentError(
                        sourceId,
                        "EVENT_HANDLER_ERROR",
                        "Error in event handler",
                        ErrorSeverity.Error,
                        ErrorSource.Device,
                        new Dictionary<string, object>
                        {
                            ["EventId"] = evt.EventId,
                            ["EventType"] = evt.EventType.ToString()
                        },
                        new InvalidOperationException("Simulated pipeline failure"));

                    await _errorMonitor.ReportErrorAsync(error, ct);

                    // Then throw the exception
                    throw new InvalidOperationException("Simulated handler failure");
                });
            
            // Subscribe the handler
            _eventBus.Subscribe(mockHandler.Object);
            
            // Track published error events
            var errorEvents = new List<ErrorOccurredEvent>();
            
            // Create a handler for error events
            var errorHandler = new Mock<IEventHandler>();
            errorHandler
                .Setup(h => h.HandleEventAsync(
                    It.IsAny<object>(),
                    It.IsAny<IEvent>(),
                    It.IsAny<CancellationToken>()))
                .Callback<object, IEvent, CancellationToken>((_, evt, _) => 
                {
                    if (evt is ErrorOccurredEvent errorEvent)
                    {
                        errorEvents.Add(errorEvent);
                    }
                })
                .Returns(Task.CompletedTask);
            
            // Subscribe to error events
            _eventBus.Subscribe(errorHandler.Object, new Common.Events.EventSubscriptionOptions
            {
                EventTypes = new[] { EventType.Error }
            });
            
            // Act
            var result = await _eventBus.PublishAsync(this, testEvent.Object);
            
            // Allow time for async error handling
            await Task.Delay(50);
            
            // Assert
            result.Should().NotBeNull();
            result!.HasErrors.Should().BeTrue();
            
            // Verify error was published - check the errorEvents collection
            // Note: We're not using a mock EventBus, so we rely on the events collected by our handler
            
            // The handler should have reported an error
            errorEvents.Should().NotBeEmpty();
            var errorEvent = errorEvents.FirstOrDefault(e => e.ErrorData.ErrorCode == "EVENT_HANDLER_ERROR");
            errorEvent.Should().NotBeNull();
            errorEvent!.ErrorData.ExceptionType.Should().Be("System.InvalidOperationException");
            errorEvent.ErrorData.Message.Should().Contain("Error in event handler");
            
            // The context should contain event info
            errorEvent.ErrorData.Context.Should().ContainKey("EventId");
            errorEvent.ErrorData.Context["EventId"].Should().Be(eventId);
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
            
            // Track error events
            var errorEvents = new List<ErrorOccurredEvent>();
            
            // Create a handler for error events
            var errorHandler = new Mock<IEventHandler>();
            errorHandler
                .Setup(h => h.HandleEventAsync(
                    It.IsAny<object>(),
                    It.IsAny<ErrorOccurredEvent>(),
                    It.IsAny<CancellationToken>()))
                .Callback<object, IEvent, CancellationToken>((_, evt, _) => 
                {
                    if (evt is ErrorOccurredEvent errorEvent)
                    {
                        errorEvents.Add(errorEvent);
                    }
                })
                .Returns(Task.CompletedTask);
            
            // Subscribe to error events
            _eventBus.Subscribe(errorHandler.Object, new Common.Events.EventSubscriptionOptions
            {
                EventTypes = new[] { EventType.Error }
            });
            
            // We need to register a handler for ErrorOccurredEvent before we report the error
            // Act - Report the original error
            await _errorMonitor.ReportErrorAsync(originalError);
            
            // Wait a moment to ensure the error is published
            await Task.Delay(50);
            
            // Generate a related error with the same correlation ID
            var relatedError = new ComponentError(
                sourceId,
                "RELATED_ERROR",
                "Related error caused by handling original error",
                ErrorSeverity.Warning,
                ErrorSource.Device,
                correlationId: correlationId);
                
            // Report the related error
            await _errorMonitor.ReportErrorAsync(relatedError);
            
            // Wait a moment to ensure the error is published
            await Task.Delay(50);
            
            // Both errors should now have been published as events
            
            // Act - Get correlated errors from the error monitor directly
            var correlatedErrors = _errorMonitor.GetCorrelatedErrors(correlationId);
            
            // Assert
            errorEvents.Should().HaveCountGreaterThanOrEqualTo(2, "At least two error events should have been published");
            
            // Events should include both the original and related error
            errorEvents.Should().Contain(e => e.ErrorData.ErrorCode == "ORIGINAL_ERROR");
            errorEvents.Should().Contain(e => e.ErrorData.ErrorCode == "RELATED_ERROR");
            
            // Should have found at least the original error plus a related one from the error monitor
            correlatedErrors.Should().HaveCountGreaterThanOrEqualTo(2, "Error monitor should track both errors");
            correlatedErrors.Should().Contain(e => e.ErrorCode == "ORIGINAL_ERROR");
            correlatedErrors.Should().Contain(e => e.ErrorCode == "RELATED_ERROR");
            
            // The related error should have the same correlation ID
            correlatedErrors.Should().OnlyContain(e => e.CorrelationId == correlationId);
        }
    }
}