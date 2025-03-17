using FluentAssertions;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Foundation.Abstractions.Interfaces.Events.Routing;
using HydroGarden.Foundation.Common.Events;
using HydroGarden.Foundation.Common.Events.Adapters;
using HydroGarden.Foundation.ErrorHandling;
using HydroGarden.Logger.Abstractions;
using Moq;
using Xunit;

namespace HydroGarden.Foundation.Tests.Integration.EventBus
{
    /// <summary>
    /// Integration tests that verify the complete event processing pipeline.
    /// </summary>
    public class EventBusPipelineIntegrationTests
    {
        private readonly Mock<ILogger> _mockLogger;
        private readonly Mock<IErrorMonitor> _mockErrorMonitor;
        private readonly Mock<IEventRouter> _mockEventRouter;
        private readonly Common.Events.EventBus _eventBus;
        
        public EventBusPipelineIntegrationTests()
        {
            _mockLogger = new Mock<ILogger>();
            _mockErrorMonitor = new Mock<IErrorMonitor>();
            _mockEventRouter = new Mock<IEventRouter>();
            
            // Set up router to pass through all subscriptions
            _mockEventRouter.Setup(r => r.GetMatchingSubscriptionsAsync(
                It.IsAny<IEvent>(), 
                It.IsAny<IEnumerable<IEventSubscription>>(), 
                It.IsAny<CancellationToken>()))
                .Returns<IEvent, IEnumerable<IEventSubscription>, CancellationToken>((_, subs, _) => 
                    Task.FromResult<IReadOnlyList<IEventSubscription>>(subs.ToList()));
            
            _eventBus = new Common.Events.EventBus(
                _mockLogger.Object,
                _mockEventRouter.Object);
        }
        
        [Fact]
        public async Task EventProcessing_ShouldFollowOrderedSteps()
        {
            // Arrange
            var processingSteps = new List<string>();
            var sourceId = Guid.NewGuid();
            var eventId = Guid.NewGuid();
            
            // Create a test event
            var testEvent = new Mock<IEvent>();
            testEvent.Setup(e => e.EventId).Returns(eventId);
            testEvent.Setup(e => e.EventType).Returns(EventType.Command);
            testEvent.Setup(e => e.SourceId).Returns(sourceId);
            testEvent.Setup(e => e.Timestamp).Returns(DateTime.UtcNow);
            
            // Create multiple handlers that will execute in sequence and record their order
            // Each handler will add to the processing steps list to track execution order
            
            var handler1 = new Mock<IEventHandler>();
            handler1
                .Setup(h => h.HandleEventAsync(
                    It.IsAny<object>(),
                    It.IsAny<IEvent>(),
                    It.IsAny<CancellationToken>()))
                .Callback<object, IEvent, CancellationToken>((_, __, ___) => 
                    processingSteps.Add("Handler1"))
                .Returns(Task.CompletedTask);
                
            var handler2 = new Mock<IEventHandler>();
            handler2
                .Setup(h => h.HandleEventAsync(
                    It.IsAny<object>(),
                    It.IsAny<IEvent>(),
                    It.IsAny<CancellationToken>()))
                .Callback<object, IEvent, CancellationToken>((_, __, ___) => 
                    processingSteps.Add("Handler2"))
                .Returns(Task.CompletedTask);
                
            var handler3 = new Mock<IEventHandler>();
            handler3
                .Setup(h => h.HandleEventAsync(
                    It.IsAny<object>(),
                    It.IsAny<IEvent>(),
                    It.IsAny<CancellationToken>()))
                .Callback<object, IEvent, CancellationToken>((_, __, ___) => 
                    processingSteps.Add("Handler3"))
                .Returns(Task.CompletedTask);
            
            // Subscribe all handlers using adapters
            var adapter1 = new GenericEventHandlerAdapter<IEvent>(handler1.Object);
            var adapter2 = new GenericEventHandlerAdapter<IEvent>(handler2.Object);
            var adapter3 = new GenericEventHandlerAdapter<IEvent>(handler3.Object);
            _eventBus.Subscribe<IEvent>(adapter1);
            _eventBus.Subscribe<IEvent>(adapter2);
            _eventBus.Subscribe<IEvent>(adapter3);
            
            // Act
            var result = await _eventBus.PublishAsync(this, testEvent.Object);
            
            // Assert
            result.Should().NotBeNull();
            result!.EventId.Should().Be(eventId);
            result.SuccessCount.Should().Be(3); // Should be 3 after our fix
            
            // Handlers should have been called in order
            processingSteps.Should().HaveCount(3);
            processingSteps.Should().ContainInOrder(new[] { "Handler1", "Handler2", "Handler3" });
        }

        [Fact]
        public async Task EventTransformation_ShouldModifyEvents()
        {
            // Arrange
            // Create a test event
            var eventId = Guid.NewGuid();
            var sourceId = Guid.NewGuid();
            var testEvent = new Mock<IEvent>();
            testEvent.Setup(e => e.EventId).Returns(eventId);
            testEvent.Setup(e => e.EventType).Returns(EventType.Command);
            testEvent.Setup(e => e.SourceId).Returns(sourceId);
            testEvent.Setup(e => e.Timestamp).Returns(DateTime.UtcNow);

            // Create a transformed event with modified properties
            var transformedEvent = new Mock<IEvent>();
            transformedEvent.Setup(e => e.EventId).Returns(eventId);
            transformedEvent.Setup(e => e.EventType).Returns(EventType.StateChange); // Changed type
            transformedEvent.Setup(e => e.SourceId).Returns(sourceId);
            transformedEvent.Setup(e => e.Timestamp).Returns(DateTime.UtcNow);

            // Create a transformer
            var transformer = new Mock<IEventTransformer>();
            transformer
                .Setup(t => t.Transform(It.IsAny<IEvent>()))
                .Returns(transformedEvent.Object);

            // Set up the handlers with tracking
            var receivedByCommand = false;
            var receivedByState = false;
            var commandHandler = new Mock<IEventHandler>();
            var stateHandler = new Mock<IEventHandler>();

            // Setup custom router to apply transformation when getting subscriptions
            var mockRouter = new Mock<IEventRouter>();
            mockRouter
                .Setup(r => r.GetMatchingSubscriptionsAsync(
                    It.IsAny<IEvent>(),
                    It.IsAny<IEnumerable<IEventSubscription>>(),
                    It.IsAny<CancellationToken>()))
                .Returns<IEvent, IEnumerable<IEventSubscription>, CancellationToken>((evt, subs, _) =>
                {
                    // Apply the transformation to the event before matching subscriptions
                    IEvent eventToMatch = transformer.Object.Transform(evt);
                    _mockLogger.Object.Log($"Router using transformed event type: {eventToMatch.EventType}");
                    
                    // Only return handlers that match the event type of the transformed event
                    return Task.FromResult<IReadOnlyList<IEventSubscription>>(
                        subs.Where(s => 
                            s.Options.EventTypes.Length == 0 || 
                            s.Options.EventTypes.Contains(eventToMatch.EventType))
                        .ToList());
                });

            // Create event bus with our custom router and transformer
            var eventBus = new Common.Events.EventBus(
                _mockLogger.Object,
                mockRouter.Object,
                null,
                transformer.Object);

            // Setup the handlers for specific event types
            commandHandler
                .Setup(h => h.HandleEventAsync(
                    It.IsAny<object>(),
                    It.IsAny<IEvent>(),
                    It.IsAny<CancellationToken>()))
                .Callback<object, IEvent, CancellationToken>((_, e, __) => {
                    if (e.EventType == EventType.Command) {
                        receivedByCommand = true;
                        _mockLogger.Object.Log("Command handler received event");
                    }
                })
                .Returns(Task.CompletedTask);

            stateHandler
                .Setup(h => h.HandleEventAsync(
                    It.IsAny<object>(),
                    It.IsAny<IEvent>(),
                    It.IsAny<CancellationToken>()))
                .Callback<object, IEvent, CancellationToken>((_, e, __) => {
                    // The handler should be called with the transformed event
                    // But will pass the state check if our custom router worked correctly
                    if (e.EventType == EventType.StateChange) {
                        receivedByState = true;
                        _mockLogger.Object.Log("State handler received event");
                    }
                })
                .Returns(Task.CompletedTask);

            // Subscribe handlers with specific event types using adapters
            var commandAdapter = new GenericEventHandlerAdapter<IEvent>(commandHandler.Object);
            var stateAdapter = new GenericEventHandlerAdapter<IEvent>(stateHandler.Object);
            
            eventBus.Subscribe<IEvent>(commandAdapter, new EventSubscriptionOptions
            {
                EventTypes = new[] { EventType.Command }
            });

            eventBus.Subscribe<IEvent>(stateAdapter, new EventSubscriptionOptions
            {
                EventTypes = new[] { EventType.StateChange }
            });

            // Act - Test handlers individually first to confirm setup is correct
            // First confirm direct handler invocation works
            await commandHandler.Object.HandleEventAsync(this, testEvent.Object, CancellationToken.None);
            receivedByCommand.Should().BeTrue("Command handler should handle Command events directly");
            receivedByCommand = false; // Reset
            
            await stateHandler.Object.HandleEventAsync(this, transformedEvent.Object, CancellationToken.None);
            receivedByState.Should().BeTrue("State handler should handle StateChange events directly");
            receivedByState = false; // Reset

            // Then publish through the EventBus with our custom router
            var result = await eventBus.PublishAsync(this, testEvent.Object);

            // Assert
            result.Should().NotBeNull();
            
            // The router should have been called to get subscriptions
            mockRouter.Verify(r => r.GetMatchingSubscriptionsAsync(
                It.IsAny<IEvent>(),
                It.IsAny<IEnumerable<IEventSubscription>>(),
                It.IsAny<CancellationToken>()), Times.Once);

            // The transformer should have been called at least once
            transformer.Verify(t => t.Transform(It.IsAny<IEvent>()), Times.AtLeastOnce());

            // The state handler should have been called, not the command handler
            receivedByState.Should().BeTrue("State handler should receive transformed event");
            receivedByCommand.Should().BeFalse("Command handler should not receive transformed event");
        }


        [Fact]
        public async Task ErrorHandling_ShouldCaptureExceptions()
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
            
            // Create a handler that will throw an exception
            var failingHandler = new Mock<IEventHandler>();
            failingHandler
                .Setup(h => h.HandleEventAsync(
                    It.IsAny<object>(),
                    It.IsAny<IEvent>(),
                    It.IsAny<CancellationToken>()))
                .Throws(new InvalidOperationException("Handler failure"));
            
            // Create a handler that reports errors to the error monitor
            var reportingHandler = new Mock<IEventHandler>();
            reportingHandler
                .Setup(h => h.HandleEventAsync(
                    It.IsAny<object>(),
                    It.IsAny<IEvent>(),
                    It.IsAny<CancellationToken>()))
                .Callback<object, IEvent, CancellationToken>(async (_, e, __) => 
                {
                    // Report an error
                    await _mockErrorMonitor.Object.ReportErrorAsync(
                        new ComponentError(
                            sourceId,
                            "HANDLER_ERROR",
                            "Error in event handler",
                            ErrorSeverity.Error,
                            ErrorSource.Device), __);
                })
                    .Returns(Task.CompletedTask);
            
            // Subscribe both handlers with adapters
            var failingAdapter = new GenericEventHandlerAdapter<IEvent>(failingHandler.Object);
            var reportingAdapter = new GenericEventHandlerAdapter<IEvent>(reportingHandler.Object);
            _eventBus.Subscribe<IEvent>(failingAdapter);
            _eventBus.Subscribe<IEvent>(reportingAdapter);
            
            // Act
            var result = await _eventBus.PublishAsync(this, testEvent.Object);
            
            // Assert
            result.Should().NotBeNull();
            result!.EventId.Should().Be(eventId);
            result.HandlerCount.Should().Be(2); // After our fix, handler count should be 2
            result.SuccessCount.Should().Be(1); // Only one handler succeeded
            result.HasErrors.Should().BeTrue();
            result.Errors.Should().ContainSingle(e => e is InvalidOperationException);
            
            // Verify the error monitor was called
            _mockErrorMonitor.Verify(
                m => m.ReportErrorAsync(
                    It.Is<IApplicationError>(e => e.ErrorCode == "HANDLER_ERROR"),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }
}