using HydroGarden.Foundation.Abstractions.Interfaces;
using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Foundation.Abstractions.Interfaces.Logging;
using HydroGarden.Foundation.Common.Events;
using Moq;
using FluentAssertions;
using Xunit;
using System;
using System.Threading;
using System.Threading.Tasks;
using HydroGarden.Foundation.Common.PropertyMetadata;

namespace HydroGarden.Foundation.Tests.Unit.Events
{
    /// <summary>
    /// Specialized test class that isolates the subscription behavior without
    /// relying on complex asynchronous event processing.
    /// </summary>
    public class EventBusSubscriptionTests
    {
        [Fact]
        public void EventBus_ShouldFilterEventsByEventType()
        {
            // Arrange
            var mockLogger = new Mock<IHydroGardenLogger>();
            var mockStore = new Mock<IEventStore>();
            var mockRetryPolicy = new Mock<IEventRetryPolicy>();
            var mockTransformer = new Mock<IEventTransformer>();

            // Configure the transformer to return the input event
            mockTransformer.Setup(t => t.Transform(It.IsAny<IHydroGardenEvent>()))
                .Returns<IHydroGardenEvent>(e => e);

            using var eventBus = new EventBus(
                mockLogger.Object,
                mockStore.Object,
                mockRetryPolicy.Object,
                mockTransformer.Object);

            var options = new EventSubscriptionOptions
            {
                EventTypes = new[] { EventType.Command }, // Only subscribe to Command events
                Synchronous = true
            };

            // Should never be called because we're filtering by event type
            var mockHandler = new Mock<IHydroGardenPropertyChangedEventHandler>();
            eventBus.Subscribe(mockHandler.Object, options);

            // Create a property changed event (not a command)
            var propertyEvent = new HydroGardenPropertyChangedEvent(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "TestProperty",
                typeof(string),
                null,
                "TestValue",
                new PropertyMetadata(),
                EventType.PropertyChanged); // Explicitly set event type

            // Act
            var result = eventBus.PublishAsync(this, propertyEvent, CancellationToken.None).Result;

            // Assert - No handlers should be invoked
            result.HandlerCount.Should().Be(0);
            mockHandler.Verify(h => h.HandleEventAsync(
                It.IsAny<object>(),
                It.IsAny<IHydroGardenPropertyChangedEvent>(),
                It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public void EventBus_ShouldFilterEventsBySourceId()
        {
            // Arrange
            var mockLogger = new Mock<IHydroGardenLogger>();
            var mockStore = new Mock<IEventStore>();
            var mockRetryPolicy = new Mock<IEventRetryPolicy>();
            var mockTransformer = new Mock<IEventTransformer>();

            mockTransformer.Setup(t => t.Transform(It.IsAny<IHydroGardenEvent>()))
                .Returns<IHydroGardenEvent>(e => e);

            using var eventBus = new EventBus(
                mockLogger.Object,
                mockStore.Object,
                mockRetryPolicy.Object,
                mockTransformer.Object);

            var sourceId = Guid.NewGuid();
            var differentId = Guid.NewGuid();

            var options = new EventSubscriptionOptions
            {
                EventTypes = new[] { EventType.PropertyChanged },
                SourceIds = new[] { sourceId }, // Only subscribe to events from this source
                Synchronous = true
            };

            // Should never be called because we're filtering by source ID
            var mockHandler = new Mock<IHydroGardenPropertyChangedEventHandler>();
            eventBus.Subscribe(mockHandler.Object, options);

            // Create an event from a different source
            var propertyEvent = new HydroGardenPropertyChangedEvent(
                differentId, // Device ID
                differentId, // Source ID - different from subscribed source
                "TestProperty",
                typeof(string),
                null,
                "TestValue",
                new PropertyMetadata(),
                EventType.PropertyChanged); // Explicitly set event type

            // Act
            var result = eventBus.PublishAsync(this, propertyEvent, CancellationToken.None).Result;

            // Assert - No handlers should be invoked
            result.HandlerCount.Should().Be(0);
            mockHandler.Verify(h => h.HandleEventAsync(
                It.IsAny<object>(),
                It.IsAny<IHydroGardenPropertyChangedEvent>(),
                It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public void EventBus_ShouldApplyCustomFilter()
        {
            // Arrange
            var mockLogger = new Mock<IHydroGardenLogger>();
            var mockStore = new Mock<IEventStore>();
            var mockRetryPolicy = new Mock<IEventRetryPolicy>();
            var mockTransformer = new Mock<IEventTransformer>();

            mockTransformer.Setup(t => t.Transform(It.IsAny<IHydroGardenEvent>()))
                .Returns<IHydroGardenEvent>(e => e);

            using var eventBus = new EventBus(
                mockLogger.Object,
                mockStore.Object,
                mockRetryPolicy.Object,
                mockTransformer.Object);

            // Custom filter to only accept events with property name "AcceptedProperty"
            Func<IHydroGardenEvent, bool> customFilter = evt =>
                evt is IHydroGardenPropertyChangedEvent propEvent &&
                propEvent.PropertyName == "AcceptedProperty";

            var options = new EventSubscriptionOptions
            {
                EventTypes = new[] { EventType.PropertyChanged },
                Filter = customFilter,
                Synchronous = true
            };

            var mockHandler = new Mock<IHydroGardenPropertyChangedEventHandler>();
            mockHandler.Setup(h => h.HandleEventAsync(
                It.IsAny<object>(),
                It.IsAny<IHydroGardenPropertyChangedEvent>(),
                It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            eventBus.Subscribe(mockHandler.Object, options);

            // Create an event that should be rejected by the filter
            var rejectedEvent = new HydroGardenPropertyChangedEvent(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "RejectedProperty", // This should be filtered out
                typeof(string),
                null,
                "TestValue",
                new PropertyMetadata(),
                EventType.PropertyChanged); // Explicitly set event type

            // Act
            var result = eventBus.PublishAsync(this, rejectedEvent, CancellationToken.None).Result;

            // Assert - No handlers should be invoked
            result.HandlerCount.Should().Be(0);
            mockHandler.Verify(h => h.HandleEventAsync(
                It.IsAny<object>(),
                It.IsAny<IHydroGardenPropertyChangedEvent>(),
                It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public void EventBus_ShouldPassEventsWhenAllFiltersMatch()
        {
            // Arrange
            var mockLogger = new Mock<IHydroGardenLogger>();
            var mockStore = new Mock<IEventStore>();
            var mockRetryPolicy = new Mock<IEventRetryPolicy>();
            var mockTransformer = new Mock<IEventTransformer>();

            mockTransformer.Setup(t => t.Transform(It.IsAny<IHydroGardenEvent>()))
                .Returns<IHydroGardenEvent>(e => e);

            using var eventBus = new EventBus(
                mockLogger.Object,
                mockStore.Object,
                mockRetryPolicy.Object,
                mockTransformer.Object);

            var sourceId = Guid.NewGuid();

            // Custom filter to only accept events with property name "AcceptedProperty"
            Func<IHydroGardenEvent, bool> customFilter = evt =>
                evt is IHydroGardenPropertyChangedEvent propEvent &&
                propEvent.PropertyName == "AcceptedProperty";

            var options = new EventSubscriptionOptions
            {
                EventTypes = new[] { EventType.PropertyChanged },
                SourceIds = new[] { sourceId },
                Filter = customFilter,
                Synchronous = true
            };

            var mockHandler = new Mock<IHydroGardenPropertyChangedEventHandler>();
            mockHandler.Setup(h => h.HandleEventAsync(
                It.IsAny<object>(),
                It.IsAny<IHydroGardenPropertyChangedEvent>(),
                It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            eventBus.Subscribe(mockHandler.Object, options);

            // Create an event that matches all filters
            var matchingEvent = new HydroGardenPropertyChangedEvent(
                Guid.NewGuid(),  // Device ID
                sourceId,        // Source ID matches
                "AcceptedProperty", // Property name matches filter
                typeof(string),
                null,
                "TestValue",
                new PropertyMetadata(),
                EventType.PropertyChanged); // Event type matches

            // Act
            var result = eventBus.PublishAsync(this, matchingEvent, CancellationToken.None).Result;

            // Assert - Handler should be invoked
            result.HandlerCount.Should().Be(1);
            mockHandler.Verify(h => h.HandleEventAsync(
                It.IsAny<object>(),
                It.IsAny<IHydroGardenPropertyChangedEvent>(),
                It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }
}