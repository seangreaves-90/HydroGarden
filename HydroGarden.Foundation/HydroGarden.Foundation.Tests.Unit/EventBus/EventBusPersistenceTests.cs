using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Foundation.Common.Events;
using HydroGarden.Foundation.Common.PropertyMetadata;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace HydroGarden.Foundation.Tests.Unit.Events
{
    /// <summary>
    /// Tests for EventBus persistence functionality.
    /// These tests verify event persistence behavior required by the project overhaul plan.
    /// </summary>
    public class EventBusPersistenceTests : EventBusBaseTests
    {
        [Fact]
        public async Task EventBus_ShouldPersistEvents_WhenSpecifiedInRoutingData()
        {
            // Arrange
            using var eventBus = CreateTestEventBus();

            var sourceId = Guid.NewGuid();

            // Create routing data with persistence enabled
            var routingData = EventRoutingData.CreateBuilder()
                .WithPersistence(true)
                .Build();

            // Create an event with persistence enabled
            var persistableEvent = new HydroGardenPropertyChangedEvent(
                sourceId,
                sourceId,
                "TestProperty",
                typeof(string),
                null,
                "PersistMe",
                new PropertyMetadata(),
                routingData);

            // Act
            var result = await eventBus.PublishAsync(this, persistableEvent, CancellationToken.None);

            // Assert - The event should be persisted
            MockStore.Verify(s => s.PersistEventAsync(It.Is<IHydroGardenEvent>(
                e => e.EventId == persistableEvent.EventId)),
                Times.Once);
        }

        [Fact]
        public async Task EventBus_ShouldNotPersistEvents_WhenNotSpecifiedInRoutingData()
        {
            // Arrange
            using var eventBus = CreateTestEventBus();

            var sourceId = Guid.NewGuid();

            // Create routing data with persistence disabled (default)
            var routingData = new EventRoutingData();

            // Create an event with persistence disabled
            var nonPersistableEvent = new HydroGardenPropertyChangedEvent(
                sourceId,
                sourceId,
                "TestProperty",
                typeof(string),
                null,
                "DontPersistMe",
                new PropertyMetadata(),
                routingData);

            // Act
            var result = await eventBus.PublishAsync(this, nonPersistableEvent, CancellationToken.None);

            // Assert - The event should not be persisted
            MockStore.Verify(s => s.PersistEventAsync(It.IsAny<IHydroGardenEvent>()),
                Times.Never);
        }

        [Fact]
        public async Task EventBus_ShouldPersistFailedEvents()
        {
            // Arrange
            using var eventBus = CreateTestEventBus();

            var sourceId = Guid.NewGuid();

            // Create a handler that throws an exception
            var mockHandler = new Mock<IHydroGardenPropertyChangedEventHandler>();
            mockHandler.Setup(h => h.HandleEventAsync(
                It.IsAny<object>(),
                It.IsAny<IHydroGardenPropertyChangedEvent>(),
                It.IsAny<CancellationToken>()))
                .Throws(new InvalidOperationException("Test exception"));

            // Subscribe the handler
            var options = new EventSubscriptionOptions
            {
                EventTypes = new[] { EventType.PropertyChanged },
                SourceIds = new[] { sourceId },
                Synchronous = true
            };

            eventBus.Subscribe(mockHandler.Object, options);

            // Create a standard event (no persistence specified)
            var evt = new HydroGardenPropertyChangedEvent(
                sourceId,
                sourceId,
                "TestProperty",
                typeof(string),
                null,
                "TestValue",
                new PropertyMetadata());

            // Act
            var result = await eventBus.PublishAsync(this, evt, CancellationToken.None);

            // Assert - Failed events should be persisted for retry
            result.HasErrors.Should().BeTrue();
            MockStore.Verify(s => s.PersistEventAsync(It.Is<IHydroGardenEvent>(
                e => e.EventId == evt.EventId)),
                Times.Once);
        }

        [Fact]
        public async Task EventBus_ShouldAttemptToRetryFailedEvents()
        {
            // Arrange
            using var eventBus = CreateTestEventBus();

            var sourceId = Guid.NewGuid();
            var failedEvent = new HydroGardenPropertyChangedEvent(
                sourceId,
                sourceId,
                "TestProperty",
                typeof(string),
                null,
                "RetryMe",
                new PropertyMetadata());

            // Set up retry policy to allow a retry
            MockRetryPolicy.Setup(r => r.ShouldRetryAsync(
                It.Is<IHydroGardenEvent>(e => e.EventId == failedEvent.EventId),
                It.Is<int>(i => i == 1)))
                .ReturnsAsync(true);

            // Set up event store to return the failed event
            MockStore.Setup(s => s.RetrieveFailedEventAsync())
                .ReturnsAsync(failedEvent);

            // Act
            await eventBus.ProcessFailedEventsAsync(CancellationToken.None);

            // Assert - Retry policy should be consulted
            MockRetryPolicy.Verify(r => r.ShouldRetryAsync(
                It.Is<IHydroGardenEvent>(e => e.EventId == failedEvent.EventId),
                It.IsAny<int>()),
                Times.Once);

            // Verify republishing of the event
            MockTransformer.Verify(t => t.Transform(
                It.Is<IHydroGardenEvent>(e => e.EventId == failedEvent.EventId)),
                Times.Once);
        }
    }
}