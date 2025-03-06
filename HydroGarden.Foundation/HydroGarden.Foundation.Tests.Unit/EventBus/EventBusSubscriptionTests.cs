using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Foundation.Common.Events;
using HydroGarden.Foundation.Common.PropertyMetadata;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using HydroGarden.Foundation.Tests.Unit.EventBus;

namespace HydroGarden.Foundation.Tests.Unit.Events
{
    /// <summary>
    /// Tests for basic EventBus subscription and filtering functionality.
    /// These tests cover the core requirements for event filtering and routing.
    /// </summary>
    public class EventBusSubscriptionTests : EventBusBaseTests
    {
        #region Original Tests (Fixed with Async)

        [Fact]
        public async Task EventBus_ShouldFilterEventsByEventType()
        {
            // Arrange
            using var eventBus = CreateTestEventBus();

            var options = new EventSubscriptionOptions
            {
                EventTypes = new[] { EventType.Command }, // Only subscribe to Command events
                Synchronous = true
            };

            // Should never be called because we're filtering by event type
            var mockHandler = new Mock<IEventHandler>();

            // Subscribe using a handler for IEvent instead of IPropertyChangedEvent
            eventBus.Subscribe(mockHandler.Object, options);

            // Create a property changed event (not a command)
            var propertyEvent = new HydroGardenPropertyChangedEvent(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "TestProperty",
                typeof(string),
                null,
                "TestValue",
                new PropertyMetadata());

            // Act
            var result = await eventBus.PublishAsync(this, propertyEvent, CancellationToken.None);

            // Assert - No handlers should be invoked
            result.HandlerCount.Should().Be(0);
            mockHandler.Verify(h => h.HandleEventAsync(
                It.IsAny<object>(),
                It.IsAny<IPropertyChangedEvent>(),
                It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task EventBus_ShouldFilterEventsBySourceId()
        {
            // Arrange
            using var eventBus = CreateTestEventBus();

            var sourceId = Guid.NewGuid();
            var differentId = Guid.NewGuid();

            var options = new EventSubscriptionOptions
            {
                EventTypes = new[] { EventType.PropertyChanged },
                SourceIds = new[] { sourceId }, // Only subscribe to events from this source
                Synchronous = true
            };

            // Should never be called because we're filtering by source ID
            var mockHandler = new Mock<IPropertyChangedEventHandler>();
            eventBus.Subscribe(mockHandler.Object, options);

            // Create an event from a different source
            var propertyEvent = new HydroGardenPropertyChangedEvent(
                differentId, // Device ID
                differentId, // Source ID - different from subscribed source
                "TestProperty",
                typeof(string),
                null,
                "TestValue",
                new PropertyMetadata());

            // Act
            var result = await eventBus.PublishAsync(this, propertyEvent, CancellationToken.None);

            // Assert - No handlers should be invoked
            result.HandlerCount.Should().Be(0);
            mockHandler.Verify(h => h.HandleEventAsync(
                It.IsAny<object>(),
                It.IsAny<IPropertyChangedEvent>(),
                It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task EventBus_ShouldApplyCustomFilter()
        {
            // Arrange
            using var eventBus = CreateTestEventBus();

            // Custom filter to only accept events with property name "AcceptedProperty"
            Func<IEvent, bool> customFilter = evt =>
                evt is IPropertyChangedEvent propEvent &&
                propEvent.PropertyName == "AcceptedProperty";

            var options = new EventSubscriptionOptions
            {
                EventTypes = new[] { EventType.PropertyChanged },
                Filter = customFilter,
                Synchronous = true
            };

            var mockHandler = new Mock<IPropertyChangedEventHandler>();
            mockHandler.Setup(h => h.HandleEventAsync(
                It.IsAny<object>(),
                It.IsAny<IPropertyChangedEvent>(),
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
                new PropertyMetadata());

            // Act
            var result = await eventBus.PublishAsync(this, rejectedEvent, CancellationToken.None);

            // Assert - No handlers should be invoked
            result.HandlerCount.Should().Be(0);
            mockHandler.Verify(h => h.HandleEventAsync(
                It.IsAny<object>(),
                It.IsAny<IPropertyChangedEvent>(),
                It.IsAny<CancellationToken>()),
                Times.Never);
        }

        #endregion

        #region Additional Basic Tests

        [Fact]
        public async Task EventBus_ShouldProcessEventsMatchingCriteria()
        {
            // Arrange
            using var eventBus = CreateTestEventBus();

            var sourceId = Guid.NewGuid();

            var options = new EventSubscriptionOptions
            {
                EventTypes = new[] { EventType.PropertyChanged },
                SourceIds = new[] { sourceId },
                Synchronous = true
            };

            var mockHandler = new Mock<IPropertyChangedEventHandler>();
            mockHandler.Setup(h => h.HandleEventAsync(
                It.IsAny<object>(),
                It.IsAny<IPropertyChangedEvent>(),
                It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            eventBus.Subscribe(mockHandler.Object, options);

            // Create an event that matches the criteria
            var matchingEvent = new HydroGardenPropertyChangedEvent(
                sourceId, // Device ID
                sourceId, // Source ID - matches the subscription
                "TestProperty",
                typeof(string),
                null,
                "TestValue",
                new PropertyMetadata());

            // Act
            var result = await eventBus.PublishAsync(this, matchingEvent, CancellationToken.None);

            // Assert - Handler should be invoked once
            result.HandlerCount.Should().Be(1);
            result.SuccessCount.Should().Be(1);
            mockHandler.Verify(h => h.HandleEventAsync(
                It.IsAny<object>(),
                It.IsAny<IPropertyChangedEvent>(),
                It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task EventBus_CanUnsubscribeHandlers()
        {
            // Arrange
            using var eventBus = CreateTestEventBus();

            var sourceId = Guid.NewGuid();

            var options = new EventSubscriptionOptions
            {
                EventTypes = new[] { EventType.PropertyChanged },
                SourceIds = new[] { sourceId },
                Synchronous = true
            };

            var mockHandler = new Mock<IPropertyChangedEventHandler>();
            mockHandler.Setup(h => h.HandleEventAsync(
                It.IsAny<object>(),
                It.IsAny<IPropertyChangedEvent>(),
                It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Subscribe and get the subscription ID
            var subscriptionId = eventBus.Subscribe(mockHandler.Object, options);

            // Create an event that matches the criteria
            var matchingEvent = new HydroGardenPropertyChangedEvent(
                sourceId,
                sourceId,
                "TestProperty",
                typeof(string),
                null,
                "TestValue",
                new PropertyMetadata());

            // Act - First verify the handler is called
            var firstResult = await eventBus.PublishAsync(this, matchingEvent, CancellationToken.None);

            // Unsubscribe
            var unsubscribeResult = eventBus.Unsubscribe(subscriptionId);

            // Publish again
            var secondResult = await eventBus.PublishAsync(this, matchingEvent, CancellationToken.None);

            // Assert
            unsubscribeResult.Should().BeTrue();
            firstResult.HandlerCount.Should().Be(1);
            firstResult.SuccessCount.Should().Be(1);
            secondResult.HandlerCount.Should().Be(0);

            mockHandler.Verify(h => h.HandleEventAsync(
                It.IsAny<object>(),
                It.IsAny<IPropertyChangedEvent>(),
                It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task EventBus_ShouldHandleMultipleSubscriptions()
        {
            // Arrange
            using var eventBus = CreateTestEventBus();
            var sourceId = Guid.NewGuid();

            // Create first handler and subscription
            var mockHandler1 = new Mock<IPropertyChangedEventHandler>();
            mockHandler1.Setup(h => h.HandleEventAsync(
                It.IsAny<object>(),
                It.IsAny<IPropertyChangedEvent>(),
                It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var options1 = new EventSubscriptionOptions
            {
                EventTypes = new[] { EventType.PropertyChanged },
                SourceIds = new[] { sourceId },
                Synchronous = true
            };

            // Create second handler and subscription with different filter
            var mockHandler2 = new Mock<IPropertyChangedEventHandler>();
            mockHandler2.Setup(h => h.HandleEventAsync(
                It.IsAny<object>(),
                It.IsAny<IPropertyChangedEvent>(),
                It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var options2 = new EventSubscriptionOptions
            {
                EventTypes = new[] { EventType.PropertyChanged },
                Filter = evt => evt is IPropertyChangedEvent propEvt &&
                                propEvt.PropertyName == "TestProperty",
                Synchronous = true
            };

            // Subscribe both handlers
            eventBus.Subscribe(mockHandler1.Object, options1);
            eventBus.Subscribe(mockHandler2.Object, options2);

            // Create an event that matches both criteria
            var matchingEvent = new HydroGardenPropertyChangedEvent(
                sourceId,
                sourceId,
                "TestProperty",
                typeof(string),
                null,
                "TestValue",
                new PropertyMetadata());

            // Act
            var result = await eventBus.PublishAsync(this, matchingEvent, CancellationToken.None);

            // Assert - Both handlers should be invoked
            result.HandlerCount.Should().Be(2);
            result.SuccessCount.Should().Be(2);
            mockHandler1.Verify(h => h.HandleEventAsync(
                It.IsAny<object>(),
                It.IsAny<IPropertyChangedEvent>(),
                It.IsAny<CancellationToken>()),
                Times.Once);
            mockHandler2.Verify(h => h.HandleEventAsync(
                It.IsAny<object>(),
                It.IsAny<IPropertyChangedEvent>(),
                It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task EventBus_ShouldHandleErrorsInEventHandlers()
        {
            // Arrange
            using var eventBus = CreateTestEventBus();
            var sourceId = Guid.NewGuid();

            // Create a handler that throws an exception
            var mockHandler = new Mock<IPropertyChangedEventHandler>();
            mockHandler.Setup(h => h.HandleEventAsync(
                It.IsAny<object>(),
                It.IsAny<IPropertyChangedEvent>(),
                It.IsAny<CancellationToken>()))
                .Throws(new InvalidOperationException("Test exception"));

            var options = new EventSubscriptionOptions
            {
                EventTypes = new[] { EventType.PropertyChanged },
                SourceIds = new[] { sourceId },
                Synchronous = true
            };

            eventBus.Subscribe(mockHandler.Object, options);

            // Create an event that matches the criteria
            var matchingEvent = new HydroGardenPropertyChangedEvent(
                sourceId,
                sourceId,
                "TestProperty",
                typeof(string),
                null,
                "TestValue",
                new PropertyMetadata());

            // Act
            var result = await eventBus.PublishAsync(this, matchingEvent, CancellationToken.None);

            // Assert - Handler should have been invoked, but failed
            result.HandlerCount.Should().Be(1);
            result.SuccessCount.Should().Be(0);
            result.HasErrors.Should().BeTrue();
            result.Errors.Should().HaveCount(1);
            result.Errors[0].Should().BeOfType<InvalidOperationException>();
            result.Errors[0].Message.Should().Be("Test exception");

            // Verify store got called to persist the failed event
            MockStore.Verify(s => s.PersistEventAsync(It.IsAny<IEvent>()), Times.Once);
        }

        #endregion
    }
}