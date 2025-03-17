using FluentAssertions;
using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Foundation.Abstractions.Interfaces.Events.Routing;
using HydroGarden.Foundation.Common.Events;
using HydroGarden.Foundation.Common.Events.Adapters;
using HydroGarden.Logger.Abstractions;
using Moq;
using Xunit;

namespace HydroGarden.Foundation.Tests.Integration.EventBus
{
    /// <summary>
    /// Tests that verify event transformation and enrichment.
    /// </summary>
    public class EventTransformationTestsFixed
    {
        private readonly Mock<ILogger> _mockLogger;
        private readonly Mock<IEventRouter> _mockEventRouter;
        private readonly Mock<IEventTransformer> _mockTransformer;
        private readonly Common.Events.EventBus _eventBus;

        public EventTransformationTestsFixed()
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

            // Create an event bus with transformer for regular use in other tests
            // The transformer is used in PublishAsync to transform events during regular publishing
            _eventBus = new Common.Events.EventBus(
                _mockLogger.Object,
                _mockEventRouter.Object,
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
            originalEvent.Setup(e => e.Metadata).Returns(new Dictionary<string, object>());

            // Enriched event with additional metadata
            var enrichedEvent = new Mock<IEvent>();
            enrichedEvent.Setup(e => e.EventId).Returns(eventId);
            enrichedEvent.Setup(e => e.EventType).Returns(EventType.Command);
            enrichedEvent.Setup(e => e.SourceId).Returns(sourceId);
            enrichedEvent.Setup(e => e.Timestamp).Returns(DateTime.UtcNow);
            
            // Use proper EventRoutingData
            var routingData = EventRoutingData.CreateBuilder()
                .WithPersistence(true)
                .WithPriority(EventPriority.Normal)
                .Build();
                
            enrichedEvent.Setup(e => e.RoutingData).Returns(routingData);
            
            // Set up metadata
            var metadata = new Dictionary<string, object>
            {
                ["ProcessedTimestamp"] = DateTime.UtcNow,
                ["ProcessingNode"] = "TestNode",
                ["Version"] = "1.0"
            };
            enrichedEvent.Setup(e => e.Metadata).Returns(metadata);

            // Setup transformer to return enriched event
            _mockTransformer
                .Setup(t => t.Transform(It.IsAny<IEvent>()))
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

            // Create an adapter for IEventHandler to IEventHandler<IEvent>
            var adapter = new GenericEventHandlerAdapter<IEvent>(mockHandler.Object, typeof(IEvent));
            _eventBus.Subscribe<IEvent>(adapter);

            // Act - Publish event normally
            var result = await _eventBus.PublishAsync(this, originalEvent.Object);

            // Assert
            result.Should().NotBeNull();
            result!.SuccessCount.Should().Be(1);

            // Verify transformer was called
            _mockTransformer.Verify(t => t.Transform(It.IsAny<IEvent>()), Times.Once);

            // Verify handler received the transformed event
            capturedEvent.Should().NotBeNull();
            capturedEvent.Should().BeSameAs(enrichedEvent.Object);
            
            // Verify that the enriched event has the expected metadata
            capturedEvent!.Metadata.Should().NotBeNull();
            capturedEvent.Metadata.Should().ContainKey("ProcessedTimestamp");
            capturedEvent.Metadata.Should().ContainKey("ProcessingNode");
            capturedEvent.Metadata.Should().ContainKey("Version");
            capturedEvent.Metadata["ProcessingNode"].Should().Be("TestNode");
            capturedEvent.Metadata["Version"].Should().Be("1.0");
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

            var adapter = new GenericEventHandlerAdapter<IEvent>(mockHandler.Object, typeof(IEvent));
            _eventBus.Subscribe<IEvent>(adapter);

            // Act - Publish valid event
            var validResult = await _eventBus.PublishAsync(this, validEvent.Object);

            // Verify valid event was processed correctly
            _mockTransformer.Verify(t => t.Transform(validEvent.Object), Times.Once);
            handlerCallCount.Should().Be(1, "Handler should have been called for valid event");
            validResult.Should().NotBeNull();
            validResult!.SuccessCount.Should().Be(1);
            validResult.HasErrors.Should().BeFalse();

            // Reset for next test
            handlerCallCount = 0;
            _mockTransformer.Invocations.Clear();

            // Act - Publish invalid event, which should fail transformation
            var invalidResult = await _eventBus.PublishAsync(this, invalidEvent.Object);

            // Assert
            // Verify transformer was called and threw exception
            _mockTransformer.Verify(t => t.Transform(invalidEvent.Object), Times.Once);

            // Verify handler was not called for invalid event
            handlerCallCount.Should().Be(0, "Handler should not have been called for invalid event");

            // Verify the publish result contains the error
            invalidResult.Should().NotBeNull();
            invalidResult!.SuccessCount.Should().Be(0);
            invalidResult.HasErrors.Should().BeTrue();
            invalidResult.Errors.Should().ContainSingle(e => e is ArgumentException);
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
            
            // Use proper EventRoutingData
            var commandRoutingData = EventRoutingData.CreateBuilder()
                .WithPersistence(true)
                .WithPriority(EventPriority.High)
                .Build();
                
            commandEvent.Setup(e => e.RoutingData).Returns(commandRoutingData);
            
            // Set up metadata to include original event type information
            var metadata = new Dictionary<string, object>
            {
                ["OriginalEventType"] = EventType.StateChange,
                ["TransformedAt"] = DateTime.UtcNow
            };
            commandEvent.Setup(e => e.Metadata).Returns(metadata);
            
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
            var commandAdapter = new GenericEventHandlerAdapter<IEvent>(commandHandler.Object, typeof(IEvent));
            _eventBus.Subscribe<IEvent>(commandAdapter, new EventSubscriptionOptions
            {
                EventTypes = new[] { EventType.Command }
            });
            
            var stateAdapter = new GenericEventHandlerAdapter<IEvent>(stateHandler.Object, typeof(IEvent));
            _eventBus.Subscribe<IEvent>(stateAdapter, new EventSubscriptionOptions
            {
                EventTypes = new[] { EventType.StateChange }
            });
            
            // Act - Publish the notification event
            var result = await _eventBus.PublishAsync(this, notificationEvent.Object);
            
            // Assert
            result.Should().NotBeNull();
            result!.SuccessCount.Should().Be(1, "Only command handler should be called after transformation");
            
            // Verify transformer was called exactly once
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
        
        [Fact]
        public async Task EventBus_ShouldSkipTransformationIfNoTransformerConfigured()
        {
            // Arrange
            var sourceId = Guid.NewGuid();
            var eventId = Guid.NewGuid();
            
            // Create an event
            var testEvent = new Mock<IEvent>();
            testEvent.Setup(e => e.EventId).Returns(eventId);
            testEvent.Setup(e => e.EventType).Returns(EventType.Command);
            testEvent.Setup(e => e.SourceId).Returns(sourceId);
            testEvent.Setup(e => e.Timestamp).Returns(DateTime.UtcNow);
            
            // Create event bus without a transformer
            var eventBusWithoutTransformer = new Common.Events.EventBus(
                _mockLogger.Object,
                _mockEventRouter.Object);
            
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
            
            var adapter = new GenericEventHandlerAdapter<IEvent>(mockHandler.Object, typeof(IEvent));
            eventBusWithoutTransformer.Subscribe<IEvent>(adapter);
            
            // Act - Publish event
            var result = await eventBusWithoutTransformer.PublishAsync(this, testEvent.Object);
            
            // Assert
            result.Should().NotBeNull();
            result!.SuccessCount.Should().Be(1);
            
            // Verify handler received the original event
            capturedEvent.Should().NotBeNull();
            capturedEvent.Should().BeSameAs(testEvent.Object);
            
            // Verify publishing still works correctly without a transformer
            result.HasErrors.Should().BeFalse();
        }
    }
}