using FluentAssertions;
using HydroGarden.Foundation.Abstractions.Interfaces;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Foundation.Abstractions.Interfaces.Events.Routing;
using HydroGarden.Foundation.Abstractions.Interfaces.Services;
using HydroGarden.Foundation.Common.Events;
using HydroGarden.Logger.Abstractions;
using Moq;
using Xunit;

namespace HydroGarden.Foundation.Tests.Integration.EventBus
{
    /// <summary>
    /// Tests that verify event routing based on component topology.
    /// </summary>
    public class EventBusTopologyRoutingTests
    {
        private readonly Mock<ILogger> _mockLogger;
        private readonly Mock<ITopologyService> _mockTopologyService;
        private readonly Mock<HydroGarden.Foundation.Abstractions.Interfaces.Events.Routing.IEventRouter> _mockEventRouter;
        private readonly Mock<IErrorMonitor> _mockErrorMonitor;
        private readonly Common.Events.EventBus _eventBus;
        
        public EventBusTopologyRoutingTests()
        {
            _mockLogger = new Mock<ILogger>();
            _mockTopologyService = new Mock<ITopologyService>();
            _mockEventRouter = new Mock<IEventRouter>();
            _mockErrorMonitor = new Mock<IErrorMonitor>();
            
            _eventBus = new Common.Events.EventBus(
                _mockLogger.Object,
                _mockEventRouter.Object,
                _mockTopologyService.Object);
        }
        
        [Fact]
        public async Task EventBus_ShouldRouteEventsBasedOnTopology()
        {
            // Arrange
            var sourceId = Guid.NewGuid();
            var targetId = Guid.NewGuid();
            var eventId = Guid.NewGuid();
            
            // Setup topology service to have a connection between source and target
            var connection = new ComponentConnection
            {
                SourceId = sourceId,
                TargetId = targetId,
                ConnectionType = "TestConnection",
                IsEnabled = true
            };
            
            _mockTopologyService
                .Setup(t => t.GetConnectionsForSourceAsync(sourceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { connection });
                
            _mockTopologyService
                .Setup(t => t.GetConnectionsForTargetAsync(targetId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { connection });
            
            // Setup a custom router that checks topology
            _mockEventRouter
                .Setup(r => r.GetMatchingSubscriptionsAsync(
                    It.IsAny<IEvent>(),
                    It.IsAny<IEnumerable<IEventSubscription>>(),
                    It.IsAny<CancellationToken>()))
                .Returns<IEvent, IEnumerable<IEventSubscription>, CancellationToken>(
                    async (evt, subs, ct) =>
                    {
                        // Filter subscriptions based on topology
                        var result = new List<IEventSubscription>();
                        foreach (var sub in subs)
                        {
                            if (sub.Options.IncludeConnectedSources && sub.Options.SourceIds.Contains(targetId))
                            {
                                // Check if source is connected to any target in our subscription
                                var connections = await _mockTopologyService.Object
                                    .GetConnectionsForTargetAsync(targetId, ct);
                                
                                if (connections.Any(c => c.SourceId == evt.SourceId && c.IsEnabled))
                                {
                                    result.Add(sub);
                                }
                            }
                        }
                        return result;
                    });
            
            // Create a handler subscribed to the target
            var handlerCalled = false;
            var mockHandler = new Mock<IEventHandler>();
            mockHandler
                .Setup(h => h.HandleEventAsync(
                    It.IsAny<object>(),
                    It.IsAny<IEvent>(),
                    It.IsAny<CancellationToken>()))
                .Callback(() => handlerCalled = true)
                .Returns(Task.CompletedTask);
            
            // Subscribe to events for the target, including connected sources
            var options = new EventSubscriptionOptions
            {
                EventTypes = new[] { EventType.Command },
                SourceIds = new[] { targetId },
                IncludeConnectedSources = true
            };
            
            _eventBus.Subscribe(mockHandler.Object, options);
            
            // Create a test event from the source
            var testEvent = new Mock<IEvent>();
            testEvent.Setup(e => e.EventId).Returns(eventId);
            testEvent.Setup(e => e.EventType).Returns(EventType.Command);
            testEvent.Setup(e => e.SourceId).Returns(sourceId); // Event is from source
            testEvent.Setup(e => e.Timestamp).Returns(DateTime.UtcNow);
            
            // Act
            var result = await _eventBus.PublishAsync(this, testEvent.Object);
            
            // Assert
            handlerCalled.Should().BeTrue("The handler should be called due to topology connection");
            result.Should().NotBeNull();
            result!.SuccessCount.Should().Be(1);
            
            // Verify that the topology service was queried
            _mockTopologyService.Verify(
                t => t.GetConnectionsForTargetAsync(targetId, It.IsAny<CancellationToken>()),
                Times.Once);
        }
        
        [Fact]
        public async Task EventBus_ShouldRespectDisabledConnections()
        {
            // Arrange
            var sourceId = Guid.NewGuid();
            var targetId = Guid.NewGuid();
            var eventId = Guid.NewGuid();
            
            // Setup topology service with a disabled connection
            var connection = new ComponentConnection
            {
                SourceId = sourceId,
                TargetId = targetId,
                ConnectionType = "TestConnection",
                IsEnabled = false // Connection is disabled
            };
            
            _mockTopologyService
                .Setup(t => t.GetConnectionsForSourceAsync(sourceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { connection });
                
            _mockTopologyService
                .Setup(t => t.GetConnectionsForTargetAsync(targetId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { connection });
            
            // Setup router to check topology connections
            _mockEventRouter
                .Setup(r => r.GetMatchingSubscriptionsAsync(
                    It.IsAny<IEvent>(),
                    It.IsAny<IEnumerable<IEventSubscription>>(),
                    It.IsAny<CancellationToken>()))
                .Returns<IEvent, IEnumerable<IEventSubscription>, CancellationToken>(
                    async (evt, subs, ct) =>
                    {
                        // Filter subscriptions based on topology
                        var result = new List<IEventSubscription>();
                        foreach (var sub in subs)
                        {
                            if (sub.Options.IncludeConnectedSources && sub.Options.SourceIds.Contains(targetId))
                            {
                                // Check if source is connected to any target in our subscription
                                var connections = await _mockTopologyService.Object
                                    .GetConnectionsForTargetAsync(targetId, ct);
                                
                                if (connections.Any(c => c.SourceId == evt.SourceId && c.IsEnabled))
                                {
                                    result.Add(sub);
                                }
                            }
                        }
                        return result;
                    });
            
            // Create a handler subscribed to the target
            var handlerCalled = false;
            var mockHandler = new Mock<IEventHandler>();
            mockHandler
                .Setup(h => h.HandleEventAsync(
                    It.IsAny<object>(),
                    It.IsAny<IEvent>(),
                    It.IsAny<CancellationToken>()))
                .Callback(() => handlerCalled = true)
                .Returns(Task.CompletedTask);
            
            // Subscribe to events for the target, including connected sources
            var options = new EventSubscriptionOptions
            {
                EventTypes = new[] { EventType.Command },
                SourceIds = new[] { targetId },
                IncludeConnectedSources = true
            };
            
            _eventBus.Subscribe(mockHandler.Object, options);
            
            // Create a test event from the source
            var testEvent = new Mock<IEvent>();
            testEvent.Setup(e => e.EventId).Returns(eventId);
            testEvent.Setup(e => e.EventType).Returns(EventType.Command);
            testEvent.Setup(e => e.SourceId).Returns(sourceId); // Event is from source
            testEvent.Setup(e => e.Timestamp).Returns(DateTime.UtcNow);
            
            // Act
            var result = await _eventBus.PublishAsync(this, testEvent.Object);
            
            // Assert
            handlerCalled.Should().BeFalse("The handler should not be called due to disabled connection");
            result.Should().NotBeNull();
            result!.SuccessCount.Should().Be(0);
            
            // Verify that the topology service was queried
            _mockTopologyService.Verify(
                t => t.GetConnectionsForTargetAsync(targetId, It.IsAny<CancellationToken>()),
                Times.Once);
        }
        
        [Fact]
        public async Task EventBus_ShouldHandleConditionalRouting()
        {
            // Arrange
            var sourceId = Guid.NewGuid();
            var targetId = Guid.NewGuid();
            var eventId = Guid.NewGuid();
            
            // Setup topology service with a conditional connection
            var connection = new ComponentConnection
            {
                SourceId = sourceId,
                TargetId = targetId,
                ConnectionType = "TestConnection",
                IsEnabled = true,
                Condition = "evt.Priority >= 5" // Only route high priority events
            };
            
            _mockTopologyService
                .Setup(t => t.GetConnectionsForSourceAsync(sourceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { connection });
                
            _mockTopologyService
                .Setup(t => t.GetConnectionsForTargetAsync(targetId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { connection });
            
            // We need to set up two versions of our mock events with different behaviors
            // Create custom mocks that indicate high and low priority through an extension method/property
            
            // Setup router that uses a simple algorithm to determine priority based on event ID
            // It will consider an event "high priority" if the first byte is greater than 128
            _mockEventRouter
                .Setup(r => r.GetMatchingSubscriptionsAsync(
                    It.IsAny<IEvent>(),
                    It.IsAny<IEnumerable<IEventSubscription>>(),
                    It.IsAny<CancellationToken>()))
                .Returns<IEvent, IEnumerable<IEventSubscription>, CancellationToken>(
                    async (evt, subs, ct) =>
                    {
                        // Filter subscriptions based on topology
                        var result = new List<IEventSubscription>();
                        foreach (var sub in subs)
                        {
                            if (sub.Options.IncludeConnectedSources && sub.Options.SourceIds.Contains(targetId))
                            {
                                // Check if source is connected to any target in our subscription
                                var connections = await _mockTopologyService.Object
                                    .GetConnectionsForTargetAsync(targetId, ct);
                                
                                foreach (var conn in connections.Where(c => 
                                    c.SourceId == evt.SourceId && 
                                    c.IsEnabled))
                                {
                                    // Simple priority check - use the first byte of the event ID
                                    // This lets us control priority by setting specific event IDs
                                    var isPriority = evt.EventId.ToByteArray()[0] > 128;
                                    
                                    // If it's a priority event or there's no condition, add the subscription
                                    if (isPriority || string.IsNullOrEmpty(conn.Condition))
                                    {
                                        result.Add(sub);
                                        break;
                                    }
                                }
                            }
                        }
                        return result;
                    });
            
            // Create a handler subscribed to the target
            var handlerCallCount = 0;
            var mockHandler = new Mock<IEventHandler>();
            mockHandler
                .Setup(h => h.HandleEventAsync(
                    It.IsAny<object>(),
                    It.IsAny<IEvent>(),
                    It.IsAny<CancellationToken>()))
                .Callback(() => handlerCallCount++)
                .Returns(Task.CompletedTask);
            
            // Subscribe to events for the target, including connected sources
            var options = new EventSubscriptionOptions
            {
                EventTypes = new[] { EventType.Command },
                SourceIds = new[] { targetId },
                IncludeConnectedSources = true
            };
            
            _eventBus.Subscribe(mockHandler.Object, options);
            
            // Create high priority event with a first byte > 128
            var highPriorityGuid = new Guid(
                new byte[] { 200, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }
            );
            
            var highPriorityEvent = new Mock<IEvent>();
            highPriorityEvent.Setup(e => e.EventId).Returns(highPriorityGuid);
            highPriorityEvent.Setup(e => e.EventType).Returns(EventType.Command);
            highPriorityEvent.Setup(e => e.SourceId).Returns(sourceId);
            highPriorityEvent.Setup(e => e.Timestamp).Returns(DateTime.UtcNow);
            
            // Create low priority event with a first byte < 128
            var lowPriorityGuid = new Guid(
                new byte[] { 50, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }
            );
            
            var lowPriorityEvent = new Mock<IEvent>();
            lowPriorityEvent.Setup(e => e.EventId).Returns(lowPriorityGuid);
            lowPriorityEvent.Setup(e => e.EventType).Returns(EventType.Command);
            lowPriorityEvent.Setup(e => e.SourceId).Returns(sourceId);
            lowPriorityEvent.Setup(e => e.Timestamp).Returns(DateTime.UtcNow);
            
            // Act - publish both events
            await _eventBus.PublishAsync(this, highPriorityEvent.Object);
            await _eventBus.PublishAsync(this, lowPriorityEvent.Object);
            
            // Assert
            handlerCallCount.Should().Be(1, "Only high priority event should be routed");
        }
    }
}