using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Foundation.Abstractions.Interfaces.Services;
using HydroGarden.Foundation.Common.Events;
using HydroGarden.Foundation.Common.PropertyMetadata;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using HydroGarden.Foundation.Tests.Unit.EventBus;

namespace HydroGarden.Foundation.Tests.Unit.Events
{
    /// <summary>
    /// Tests for the EventBus routing functionality, including topology-aware routing.
    /// These tests cover the advanced routing capabilities required by the project overhaul plan.
    /// </summary>
    public class EventBusRoutingTests : EventBusBaseTests
    {
        #region Topology-Aware Routing Tests

        [Fact]
        public async Task EventBus_ShouldRouteEventsBasedOnTopology()
        {
            // Arrange
            using var eventBus = CreateTestEventBus();

            var sourceId = Guid.NewGuid();
            var connectedId = Guid.NewGuid();

            // Set up a mock topology service
            var mockTopologyService = new Mock<ITopologyService>();

            // Create a connection from sourceId to connectedId
            var mockConnection = new Mock<IComponentConnection>();
            mockConnection.Setup(c => c.SourceId).Returns(sourceId);
            mockConnection.Setup(c => c.TargetId).Returns(connectedId);
            mockConnection.Setup(c => c.IsEnabled).Returns(true);

            // Return the connection when queried
            mockTopologyService
                .Setup(ts => ts.GetConnectionsForSourceAsync(sourceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<IComponentConnection> { mockConnection.Object });

            mockTopologyService
                .Setup(ts => ts.EvaluateConnectionConditionAsync(mockConnection.Object, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Subscribe to events with includeConnectedSources
            var options = new EventSubscriptionOptions
            {
                EventTypes = new[] { EventType.PropertyChanged },
                SourceIds = new[] { connectedId },
                IncludeConnectedSources = true, // Important! This tells the EventBus to check topology
                Synchronous = true
            };

            var mockHandler = new Mock<IPropertyChangedEventHandler>();
            mockHandler.Setup(h => h.HandleEventAsync(
                It.IsAny<object>(),
                It.IsAny<IPropertyChangedEvent>(),
                It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            eventBus.Subscribe(mockHandler.Object, options);

            // Set the topology service on the test event bus
            eventBus.SetTopologyService(mockTopologyService.Object);

            // Create an event from the source (which is connected to the target by topology)
            var sourceEvent = new HydroGardenPropertyChangedEvent(
                sourceId,
                sourceId,
                "TestProperty",
                typeof(string),
                null,
                "TestValue",
                new PropertyMetadata());

            // Act
            var result = await eventBus.PublishAsync(this, sourceEvent, CancellationToken.None);

            // Assert - Handler should be invoked due to the topology connection
            result.HandlerCount.Should().Be(1);
            result.SuccessCount.Should().Be(1);
            mockHandler.Verify(h => h.HandleEventAsync(
                It.IsAny<object>(),
                It.IsAny<IPropertyChangedEvent>(),
                It.IsAny<CancellationToken>()),
                Times.Once);

            // Verify topology service was consulted
            mockTopologyService.Verify(ts =>
                ts.GetConnectionsForSourceAsync(sourceId, It.IsAny<CancellationToken>()),
                Times.Once);
            mockTopologyService.Verify(ts =>
                ts.EvaluateConnectionConditionAsync(mockConnection.Object, It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task EventBus_ShouldRespectDisabledConnections()
        {
            // Arrange
            using var eventBus = CreateTestEventBus();

            var sourceId = Guid.NewGuid();
            var connectedId = Guid.NewGuid();

            // Set up a mock topology service
            var mockTopologyService = new Mock<ITopologyService>();

            // Create a DISABLED connection from sourceId to connectedId
            var mockConnection = new Mock<IComponentConnection>();
            mockConnection.Setup(c => c.SourceId).Returns(sourceId);
            mockConnection.Setup(c => c.TargetId).Returns(connectedId);
            mockConnection.Setup(c => c.IsEnabled).Returns(false); // Disabled!

            // Return the connection when queried
            mockTopologyService
                .Setup(ts => ts.GetConnectionsForSourceAsync(sourceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<IComponentConnection> { mockConnection.Object });

            // Subscribe to events with includeConnectedSources
            var options = new EventSubscriptionOptions
            {
                EventTypes = new[] { EventType.PropertyChanged },
                SourceIds = new[] { connectedId },
                IncludeConnectedSources = true,
                Synchronous = true
            };

            var mockHandler = new Mock<IPropertyChangedEventHandler>();
            mockHandler.Setup(h => h.HandleEventAsync(
                It.IsAny<object>(),
                It.IsAny<IPropertyChangedEvent>(),
                It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            eventBus.Subscribe(mockHandler.Object, options);

            // Set the topology service on the test event bus
            eventBus.SetTopologyService(mockTopologyService.Object);

            // Create an event from the source
            var sourceEvent = new HydroGardenPropertyChangedEvent(
                sourceId,
                sourceId,
                "TestProperty",
                typeof(string),
                null,
                "TestValue",
                new PropertyMetadata());

            // Act
            var result = await eventBus.PublishAsync(this, sourceEvent, CancellationToken.None);

            // Assert - Handler should NOT be invoked because connection is disabled
            result.HandlerCount.Should().Be(0);
            mockHandler.Verify(h => h.HandleEventAsync(
                It.IsAny<object>(),
                It.IsAny<IPropertyChangedEvent>(),
                It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task EventBus_ShouldRespectConnectionConditions()
        {
            // Arrange
            using var eventBus = CreateTestEventBus();

            var sourceId = Guid.NewGuid();
            var connectedId = Guid.NewGuid();

            // Set up a mock topology service
            var mockTopologyService = new Mock<ITopologyService>();

            // Create a connection with a condition that evaluates to false
            var mockConnection = new Mock<IComponentConnection>();
            mockConnection.Setup(c => c.SourceId).Returns(sourceId);
            mockConnection.Setup(c => c.TargetId).Returns(connectedId);
            mockConnection.Setup(c => c.IsEnabled).Returns(true);
            mockConnection.Setup(c => c.Condition).Returns("Property1 > 50"); // Example condition

            // Return the connection when queried
            mockTopologyService
                .Setup(ts => ts.GetConnectionsForSourceAsync(sourceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<IComponentConnection> { mockConnection.Object });

            // Condition evaluates to FALSE
            mockTopologyService
                .Setup(ts => ts.EvaluateConnectionConditionAsync(mockConnection.Object, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            // Subscribe to events with includeConnectedSources
            var options = new EventSubscriptionOptions
            {
                EventTypes = new[] { EventType.PropertyChanged },
                SourceIds = new[] { connectedId },
                IncludeConnectedSources = true,
                Synchronous = true
            };

            var mockHandler = new Mock<IPropertyChangedEventHandler>();
            mockHandler.Setup(h => h.HandleEventAsync(
                It.IsAny<object>(),
                It.IsAny<IPropertyChangedEvent>(),
                It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            eventBus.Subscribe(mockHandler.Object, options);

            // Set the topology service on the test event bus
            eventBus.SetTopologyService(mockTopologyService.Object);

            // Create an event from the source
            var sourceEvent = new HydroGardenPropertyChangedEvent(
                sourceId,
                sourceId,
                "TestProperty",
                typeof(string),
                null,
                "TestValue",
                new PropertyMetadata());

            // Act
            var result = await eventBus.PublishAsync(this, sourceEvent, CancellationToken.None);

            // Assert - Handler should NOT be invoked because condition evaluates to false
            result.HandlerCount.Should().Be(0);
            mockHandler.Verify(h => h.HandleEventAsync(
                It.IsAny<object>(),
                It.IsAny<IPropertyChangedEvent>(),
                It.IsAny<CancellationToken>()),
                Times.Never);

            // Verify condition was evaluated
            mockTopologyService.Verify(ts =>
                ts.EvaluateConnectionConditionAsync(mockConnection.Object, It.IsAny<CancellationToken>()),
                Times.Once);
        }

        #endregion

        #region Event Routing with Target IDs Tests

        [Fact]
        public async Task EventBus_ShouldRespectRoutingDataTargetIds()
        {
            // Arrange
            using var eventBus = CreateTestEventBus();

            var sourceId = Guid.NewGuid();
            var targetId1 = Guid.NewGuid();
            var targetId2 = Guid.NewGuid();

            // Create handlers for both targets
            var mockHandler1 = new Mock<IPropertyChangedEventHandler>();
            mockHandler1.Setup(h => h.HandleEventAsync(
                It.IsAny<object>(),
                It.IsAny<IPropertyChangedEvent>(),
                It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var mockHandler2 = new Mock<IPropertyChangedEventHandler>();
            mockHandler2.Setup(h => h.HandleEventAsync(
                It.IsAny<object>(),
                It.IsAny<IPropertyChangedEvent>(),
                It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Subscribe handlers for different target IDs
            var options1 = new EventSubscriptionOptions
            {
                EventTypes = new[] { EventType.PropertyChanged },
                SourceIds = new[] { targetId1 },
                Synchronous = true
            };

            var options2 = new EventSubscriptionOptions
            {
                EventTypes = new[] { EventType.PropertyChanged },
                SourceIds = new[] { targetId2 },
                Synchronous = true
            };

            eventBus.Subscribe(mockHandler1.Object, options1);
            eventBus.Subscribe(mockHandler2.Object, options2);

            // Create routing data that specifies only targetId1
            var routingData = new EventRoutingData(targetId1);

            // Create an event with specific routing
            var propertyEvent = new HydroGardenPropertyChangedEvent(
                sourceId,
                sourceId,
                "TestProperty",
                typeof(string),
                null,
                "TestValue",
                new PropertyMetadata(),
                routingData);

            // Act
            var result = await eventBus.PublishAsync(this, propertyEvent, CancellationToken.None);

            // Assert - Only handler1 should be invoked based on routing data
            result.HandlerCount.Should().Be(1);
            result.SuccessCount.Should().Be(1);
            mockHandler1.Verify(h => h.HandleEventAsync(
                It.IsAny<object>(),
                It.IsAny<IPropertyChangedEvent>(),
                It.IsAny<CancellationToken>()),
                Times.Once);

            mockHandler2.Verify(h => h.HandleEventAsync(
                It.IsAny<object>(),
                It.IsAny<IPropertyChangedEvent>(),
                It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task EventBus_ShouldRespectEventPriority()
        {
            // Arrange
            using var eventBus = CreateTestEventBus();

            var sourceId = Guid.NewGuid();

            // Create a mock handler
            var mockHandler = new Mock<IPropertyChangedEventHandler>();
            mockHandler.Setup(h => h.HandleEventAsync(
                It.IsAny<object>(),
                It.IsAny<IPropertyChangedEvent>(),
                It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Subscribe the handler
            var options = new EventSubscriptionOptions
            {
                EventTypes = new[] { EventType.PropertyChanged },
                Synchronous = true
            };

            eventBus.Subscribe(mockHandler.Object, options);

            // Create routing data with High priority
            var routingData = EventRoutingData.CreateBuilder()
                .AsHighPriority()
                .Build();

            // Create an event with high priority
            var highPriorityEvent = new HydroGardenPropertyChangedEvent(
                sourceId,
                sourceId,
                "TestProperty",
                typeof(string),
                null,
                "HighPriority",
                new PropertyMetadata(),
                routingData);

            // Act & Assert
            var result = await eventBus.PublishAsync(this, highPriorityEvent, CancellationToken.None);

            mockHandler.Verify(h => h.HandleEventAsync(
                It.IsAny<object>(),
                It.Is<IPropertyChangedEvent>(e =>
                    e.RoutingData != null && e.RoutingData.Priority == EventPriority.High),
                It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task EventBus_ShouldRouteToBothTopologyAndDirectTargets()
        {
            // Arrange
            using var eventBus = CreateTestEventBus();

            var sourceId = Guid.NewGuid();
            var directTargetId = Guid.NewGuid();
            var connectedTargetId = Guid.NewGuid();

            // Set up handlers for both direct and topology-connected targets
            var directHandler = new Mock<IPropertyChangedEventHandler>();
            directHandler.Setup(h => h.HandleEventAsync(
                It.IsAny<object>(),
                It.IsAny<IPropertyChangedEvent>(),
                It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var connectedHandler = new Mock<IPropertyChangedEventHandler>();
            connectedHandler.Setup(h => h.HandleEventAsync(
                It.IsAny<object>(),
                It.IsAny<IPropertyChangedEvent>(),
                It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Subscribe direct handler
            var directOptions = new EventSubscriptionOptions
            {
                EventTypes = new[] { EventType.PropertyChanged },
                SourceIds = new[] { sourceId }, // Direct source ID
                Synchronous = true
            };

            // Subscribe topology-connected handler
            var connectedOptions = new EventSubscriptionOptions
            {
                EventTypes = new[] { EventType.PropertyChanged },
                SourceIds = new[] { connectedTargetId },
                IncludeConnectedSources = true,
                Synchronous = true
            };

            eventBus.Subscribe(directHandler.Object, directOptions);
            eventBus.Subscribe(connectedHandler.Object, connectedOptions);

            // Set up topology service
            var mockTopologyService = new Mock<ITopologyService>();
            var mockConnection = new Mock<IComponentConnection>();
            mockConnection.Setup(c => c.SourceId).Returns(sourceId);
            mockConnection.Setup(c => c.TargetId).Returns(connectedTargetId);
            mockConnection.Setup(c => c.IsEnabled).Returns(true);

            mockTopologyService
                .Setup(ts => ts.GetConnectionsForSourceAsync(sourceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<IComponentConnection> { mockConnection.Object });

            mockTopologyService
                .Setup(ts => ts.EvaluateConnectionConditionAsync(mockConnection.Object, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            eventBus.SetTopologyService(mockTopologyService.Object);

            // Create an event
            var propertyEvent = new HydroGardenPropertyChangedEvent(
                sourceId,
                sourceId,
                "TestProperty",
                typeof(string),
                null,
                "TestValue",
                new PropertyMetadata());

            // Act
            var result = await eventBus.PublishAsync(this, propertyEvent, CancellationToken.None);

            // Assert - Both handlers should be invoked
            result.HandlerCount.Should().Be(2);
            result.SuccessCount.Should().Be(2);

            directHandler.Verify(h => h.HandleEventAsync(
                It.IsAny<object>(),
                It.IsAny<IPropertyChangedEvent>(),
                It.IsAny<CancellationToken>()),
                Times.Once);

            connectedHandler.Verify(h => h.HandleEventAsync(
                It.IsAny<object>(),
                It.IsAny<IPropertyChangedEvent>(),
                It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task EventBus_ShouldAllowMultipleTargetsInRoutingData()
        {
            // Arrange
            using var eventBus = CreateTestEventBus();

            var sourceId = Guid.NewGuid();
            var targetId1 = Guid.NewGuid();
            var targetId2 = Guid.NewGuid();
            var targetId3 = Guid.NewGuid(); // This one won't be included in routing

            // Create handlers for all three targets
            var mockHandler1 = new Mock<IPropertyChangedEventHandler>();
            var mockHandler2 = new Mock<IPropertyChangedEventHandler>();
            var mockHandler3 = new Mock<IPropertyChangedEventHandler>();

            mockHandler1.Setup(h => h.HandleEventAsync(It.IsAny<object>(), It.IsAny<IPropertyChangedEvent>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            mockHandler2.Setup(h => h.HandleEventAsync(It.IsAny<object>(), It.IsAny<IPropertyChangedEvent>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            mockHandler3.Setup(h => h.HandleEventAsync(It.IsAny<object>(), It.IsAny<IPropertyChangedEvent>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Subscribe handlers
            eventBus.Subscribe(mockHandler1.Object, new EventSubscriptionOptions
            {
                EventTypes = new[] { EventType.PropertyChanged },
                SourceIds = new[] { targetId1 },
                Synchronous = true
            });

            eventBus.Subscribe(mockHandler2.Object, new EventSubscriptionOptions
            {
                EventTypes = new[] { EventType.PropertyChanged },
                SourceIds = new[] { targetId2 },
                Synchronous = true
            });

            eventBus.Subscribe(mockHandler3.Object, new EventSubscriptionOptions
            {
                EventTypes = new[] { EventType.PropertyChanged },
                SourceIds = new[] { targetId3 },
                Synchronous = true
            });

            // Create routing data with multiple targets
            var routingData = new EventRoutingData(targetId1, targetId2); // Only targets 1 and 2

            // Create an event with multiple targets
            var propertyEvent = new HydroGardenPropertyChangedEvent(
                sourceId,
                sourceId,
                "TestProperty",
                typeof(string),
                null,
                "TestValue",
                new PropertyMetadata(),
                routingData);

            // Act
            var result = await eventBus.PublishAsync(this, propertyEvent, CancellationToken.None);

            // Assert - Only handlers 1 and 2 should be invoked
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

            mockHandler3.Verify(h => h.HandleEventAsync(
                It.IsAny<object>(),
                It.IsAny<IPropertyChangedEvent>(),
                It.IsAny<CancellationToken>()),
                Times.Never);
        }

        #endregion
    }
}