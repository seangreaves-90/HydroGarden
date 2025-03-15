using FluentAssertions;
using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Foundation.Abstractions.Interfaces.Events.Routing;
using HydroGarden.Foundation.Common.Events;
using HydroGarden.Logger.Abstractions;
using Moq;
using Xunit;

namespace HydroGarden.Foundation.Tests.Integration.EventBus
{
    /// <summary>
    /// Tests that verify event transformation and enrichment.
    /// </summary>
    public class EventTransformationTests
    {
        private readonly Mock<ILogger> _mockLogger;
        private readonly Mock<HydroGarden.Foundation.Abstractions.Interfaces.Events.Routing.IEventRouter> _mockEventRouter;
        private readonly Mock<IEventTransformer> _mockTransformer;
        private readonly Common.Events.EventBus _eventBus;
        
        public EventTransformationTests()
        {
            _mockLogger = new Mock<ILogger>();
            _mockEventRouter = new Mock<IEventRouter>();
            _mockTransformer = new Mock<IEventTransformer>();
            
            // Set up router to pass through all subscriptions
            _mockEventRouter.Setup(r => r.GetMatchingSubscriptionsAsync(
                It.IsAny<IEvent>(), 
                It.IsAny<IEnumerable<IEventSubscription>>(), 
                It.IsAny<CancellationToken>()))
                .Returns<IEvent, IEnumerable<IEventSubscription>, CancellationToken>((_, subs, _) => 
                    Task.FromResult<IReadOnlyList<IEventSubscription>>(subs.ToList()));
            
            _eventBus = new Common.Events.EventBus(
                _mockLogger.Object,
                _mockEventRouter.Object,
                null,
                null,
                null,
                _mockTransformer.Object);
        }
        
        [Fact]
        public async Task Transformer_ShouldEnrichEventsWithMetadata()
        {
            // Arrange
            var sourceId = Guid.NewGuid();
            var eventId = Guid.NewGuid();
            
            // Original event
            var originalEvent = new Mock<IEvent>();
            originalEvent.Setup(e => e.EventId).Returns(eventId);
            originalEvent.Setup(e => e.EventType).Returns(EventType.Command);
            originalEvent.Setup(e => e.SourceId).Returns(sourceId);
            originalEvent.Setup(e => e.Timestamp).Returns(DateTime.UtcNow);
            
            // Enriched event with additional metadata
            var enrichedEvent = new Mock<IEvent>();
            enrichedEvent.Setup(e => e.EventId).Returns(eventId);
            enrichedEvent.Setup(e => e.EventType).Returns(EventType.Command);
            enrichedEvent.Setup(e => e.SourceId).Returns(sourceId);
            enrichedEvent.Setup(e => e.Timestamp).Returns(DateTime.UtcNow);
            
            // Setup transformer to return enriched event
            _mockTransformer
                .Setup(t => t.Transform(It.Is<IEvent>(e => e == originalEvent.Object)))
                .Returns(enrichedEvent.Object);
            
            // Setup handler to capture the event
            IEvent? capturedEvent = null;
            var mockHandler = new Mock<IEventHandler>();
            mockHandler
                .Setup(h => h.HandleEventAsync(
                    It.IsAny<object>(),
                    It.IsAny<IEvent>(),
                    It.IsAny<CancellationToken>()))
                .Callback<object, IEvent, CancellationToken>((_, e, _) => capturedEvent = e)
                .Returns(Task.CompletedTask);
            
            _eventBus.Subscribe(mockHandler.Object);
            
            // Transform the original event directly to confirm our setup works
            var transformedEvent = _mockTransformer.Object.Transform(originalEvent.Object);
            transformedEvent.Should().BeSameAs(enrichedEvent.Object);
            
            // Act
            var result = await _eventBus.PublishAsync(this, originalEvent.Object);
            
            // Assert
            result.Should().NotBeNull();
            result!.SuccessCount.Should().Be(1);
            
            // Verify transformer was called
            _mockTransformer.Verify(t => t.Transform(originalEvent.Object), Times.Once);
            
            // Verify handler received the transformed event
            capturedEvent.Should().NotBeNull();
            capturedEvent.Should().BeSameAs(enrichedEvent.Object);
        }
        
        [Fact]
        public async Task Transformer_ShouldValidateEvents()
        {
            // Arrange
            var sourceId = Guid.NewGuid();
            var validEventId = Guid.NewGuid();
            var invalidEventId = Guid.NewGuid();
            
            // Valid event
            var validEvent = new Mock<IEvent>();
            validEvent.Setup(e => e.EventId).Returns(validEventId);
            validEvent.Setup(e => e.EventType).Returns(EventType.Command);
            validEvent.Setup(e => e.SourceId).Returns(sourceId);
            validEvent.Setup(e => e.Timestamp).Returns(DateTime.UtcNow);
            
            // Invalid event - missing required fields
            var invalidEvent = new Mock<IEvent>();
            invalidEvent.Setup(e => e.EventId).Returns(invalidEventId);
            invalidEvent.Setup(e => e.EventType).Returns(EventType.Command);
            invalidEvent.Setup(e => e.SourceId).Returns(Guid.Empty); // Invalid source ID
            invalidEvent.Setup(e => e.Timestamp).Returns(DateTime.MinValue); // Invalid timestamp
            
            // Setup transformer to validate and throw for invalid events
            _mockTransformer
                .Setup(t => t.Transform(It.Is<IEvent>(e => e.EventId == validEventId)))
                .Returns<IEvent>(e => e); // Return valid event unchanged
                
            _mockTransformer
                .Setup(t => t.Transform(It.Is<IEvent>(e => e.EventId == invalidEventId)))
                .Throws(new ArgumentException("Event validation failed: Missing required fields"));
                
            // Ensure the handler isn't called for invalid events by configuring the router
            _mockEventRouter
                .Setup(r => r.GetMatchingSubscriptionsAsync(
                    It.Is<IEvent>(e => e.EventId == invalidEventId),
                    It.IsAny<IEnumerable<IEventSubscription>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult<IReadOnlyList<IEventSubscription>>(new List<IEventSubscription>()));
            
            // Setup handler
            var handlerCallCount = 0;
            var mockHandler = new Mock<IEventHandler>();
            mockHandler
                .Setup(h => h.HandleEventAsync(
                    It.IsAny<object>(),
                    It.IsAny<IEvent>(),
                    It.IsAny<CancellationToken>()))
                .Callback(() => handlerCallCount++)
                .Returns(Task.CompletedTask);
            
            _eventBus.Subscribe(mockHandler.Object);
            
            // Act - Publish valid event
            var validResult = await _eventBus.PublishAsync(this, validEvent.Object);
            
            // Act - Publish invalid event
            var invalidResult = await _eventBus.PublishAsync(this, invalidEvent.Object);
            
            // Assert
            validResult.Should().NotBeNull();
            validResult!.SuccessCount.Should().Be(1);
            validResult.HasErrors.Should().BeFalse();
            
            invalidResult.Should().NotBeNull();
            invalidResult!.SuccessCount.Should().Be(0);
            invalidResult.HasErrors.Should().BeTrue();
            invalidResult.Errors.Should().ContainSingle(e => e is ArgumentException);
            
            // Verify transformer was called for both events
            _mockTransformer.Verify(t => t.Transform(validEvent.Object), Times.Once);
            _mockTransformer.Verify(t => t.Transform(invalidEvent.Object), Times.Once);
            
            // Verify handler was only called for valid event
            handlerCallCount.Should().Be(1);
        }
        
        [Fact]
        public async Task Transformer_ShouldTransformEventTypes()
        {
            // Arrange
            var sourceId = Guid.NewGuid();
            var eventId = Guid.NewGuid();
            
            // Original notification event
            var notificationEvent = new Mock<IEvent>();
            notificationEvent.Setup(e => e.EventId).Returns(eventId);
            notificationEvent.Setup(e => e.EventType).Returns(EventType.StateChange);
            notificationEvent.Setup(e => e.SourceId).Returns(sourceId);
            notificationEvent.Setup(e => e.Timestamp).Returns(DateTime.UtcNow);
            
            // Transformed command event derived from notification
            var commandEvent = new Mock<IEvent>();
            commandEvent.Setup(e => e.EventId).Returns(eventId);
            commandEvent.Setup(e => e.EventType).Returns(EventType.Command);
            commandEvent.Setup(e => e.SourceId).Returns(sourceId);
            commandEvent.Setup(e => e.Timestamp).Returns(DateTime.UtcNow);
            commandEvent.Setup(e => e.RoutingData).Returns(new Dictionary<string, object>
            {
                ["OriginalEventType"] = EventType.StateChange,
                ["TransformedAt"] = DateTime.UtcNow
            } as IEventRoutingData);
            
            // Setup transformer to convert StateChange to Command
            _mockTransformer
                .Setup(t => t.Transform(It.Is<IEvent>(e => e.EventType == EventType.StateChange)))
                .Returns(commandEvent.Object);
            
            // Subscribe handlers for each event type
            IEvent? capturedCommandEvent = null;
            var commandHandler = new Mock<IEventHandler>();
            commandHandler
                .Setup(h => h.HandleEventAsync(
                    It.IsAny<object>(),
                    It.IsAny<IEvent>(),
                    It.IsAny<CancellationToken>()))
                .Callback<object, IEvent, CancellationToken>((_, e, _) => capturedCommandEvent = e)
                .Returns(Task.CompletedTask);
            
            var stateHandler = new Mock<IEventHandler>();
            stateHandler
                .Setup(h => h.HandleEventAsync(
                    It.IsAny<object>(),
                    It.IsAny<IEvent>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            
            // Subscribe with specific event type options
            _eventBus.Subscribe(commandHandler.Object, new EventSubscriptionOptions
            {
                EventTypes = new[] { EventType.Command }
            });
            
            _eventBus.Subscribe(stateHandler.Object, new EventSubscriptionOptions
            {
                EventTypes = new[] { EventType.StateChange }
            });
            
            // Transform the notification event directly to verify our setup works
            var transformedEvent = _mockTransformer.Object.Transform(notificationEvent.Object);
            transformedEvent.Should().BeSameAs(commandEvent.Object);
            
            // Act - Publish notification event (actually StateChange), which should be transformed to command
            var result = await _eventBus.PublishAsync(this, notificationEvent.Object);
            
            // Assert
            result.Should().NotBeNull();
            result!.SuccessCount.Should().Be(1, "Only command handler should be called after transformation");
            
            // Verify transformer was called
            _mockTransformer.Verify(t => t.Transform(notificationEvent.Object), Times.Once);
            
            // Verify command handler received the transformed event
            capturedCommandEvent.Should().NotBeNull();
            capturedCommandEvent.Should().BeSameAs(commandEvent.Object);
            capturedCommandEvent!.EventType.Should().Be(EventType.Command);
            
            // Verify notification (StateChange) handler was not called
            stateHandler.Verify(
                h => h.HandleEventAsync(It.IsAny<object>(), It.IsAny<IEvent>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }
    }
}