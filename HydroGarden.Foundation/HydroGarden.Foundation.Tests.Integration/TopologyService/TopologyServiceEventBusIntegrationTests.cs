using FluentAssertions;
using HydroGarden.Foundation.Abstractions.Interfaces.Components;
using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Foundation.Abstractions.Interfaces.Logging;
using HydroGarden.Foundation.Abstractions.Interfaces.Services;
using HydroGarden.Foundation.Common.Events;
using HydroGarden.Foundation.Common.Events.RetryPolicies;
using HydroGarden.Foundation.Common.Events.Stores;
using HydroGarden.Foundation.Common.Events.Transforms;
using HydroGarden.Foundation.Common.Logging;
using HydroGarden.Foundation.Common.PropertyMetadata;
using HydroGarden.Foundation.Core.Components.Devices;
using HydroGarden.Foundation.Core.Services;
using HydroGarden.Foundation.Core.Stores;
using Moq;
using Xunit;

namespace HydroGarden.Foundation.Tests.Integration
{
    public class TopologyServiceEventBusIntegrationTests : IAsyncDisposable
    {
        private readonly IHydroGardenLogger _logger;
        private readonly IStore _store;
        private readonly IEventBus _eventBus;
        private readonly ITopologyService _topologyService;
        private readonly IPersistenceService _persistenceService;
        private readonly string _testDirectory;
        private readonly List<IAsyncDisposable> _disposables = new();

        public TopologyServiceEventBusIntegrationTests()
        {
            // Create a unique test directory
            _testDirectory = Path.Combine(Path.GetTempPath(), "TopologyServiceTests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testDirectory);

            // Setup real components
            _logger = new HydroGardenLogger();
            _store = new JsonStore(_testDirectory, _logger);

            _eventBus = new Common.Events.EventBus(
                _logger,
                new DeadLetterEventStore(),
                new ExponentialBackoffRetryPolicy(),
                new DefaultEventTransformer());

            _persistenceService = new PersistenceService(_store, _eventBus);
            _topologyService = new TopologyService(_logger, _store, _persistenceService);

            // Initialize EventBus with topology service
            _eventBus.SetTopologyService(_topologyService);

            // Add to disposables
            _disposables.Add(_topologyService);
        }

        [Fact]
        public async Task EventBus_ShouldRouteEventsBasedOnTopology()
        {
            // Arrange - Create two test devices
            var sourceDevice = new PumpDevice(Guid.NewGuid(), "Source Device", 100, 0, _logger);
            var targetDevice = new PumpDevice(Guid.NewGuid(), "Target Device", 100, 0, _logger);

            // Add devices to persistence service
            await _persistenceService.AddOrUpdateAsync(sourceDevice);
            await _persistenceService.AddOrUpdateAsync(targetDevice);

            // Create a connection from source to target
            var connection = new ComponentConnection
            {
                SourceId = sourceDevice.Id,
                TargetId = targetDevice.Id,
                ConnectionType = "TestConnection",
                IsEnabled = true
            };

            await _topologyService.CreateConnectionAsync(connection);

            // Setup a mock handler to capture events
            var targetEvents = new List<IHydroGardenPropertyChangedEvent>();
            var mockHandler = new Mock<IHydroGardenPropertyChangedEventHandler>();
            mockHandler.Setup(h => h.HandleEventAsync(
                It.IsAny<object>(),
                It.IsAny<IHydroGardenPropertyChangedEvent>(),
                It.IsAny<CancellationToken>()))
                .Callback<object, IHydroGardenPropertyChangedEvent, CancellationToken>(
                    (_, evt, __) => targetEvents.Add(evt))
                .Returns(Task.CompletedTask);

            // Subscribe the handler to events from the target device, but include connected sources
            var options = new EventSubscriptionOptions
            {
                EventTypes = new[] { EventType.PropertyChanged },
                SourceIds = new[] { targetDevice.Id },
                IncludeConnectedSources = true, // Important! This tells the EventBus to check topology
                Synchronous = true
            };

            _eventBus.Subscribe(mockHandler.Object, options);

            // Act - Create an event from the source device
            var sourceEvent = new HydroGardenPropertyChangedEvent(
                sourceDevice.Id,
                sourceDevice.Id,
                "TestProperty",
                typeof(string),
                null,
                "TestValue",
                new PropertyMetadata());

            // Publish the event through the event bus
            await _eventBus.PublishAsync(this, sourceEvent);

            // Wait a short time for async processing
            await Task.Delay(100);

            // Assert - The event should have been routed to the target device's handler
            targetEvents.Should().NotBeEmpty();
            targetEvents.Should().Contain(e => e.SourceId == sourceDevice.Id);
        }

        [Fact]
        public async Task EventBus_ShouldRespectConnectionConditionsWhenRouting()
        {
            // Arrange - Create two test devices
            var sourceDevice = new PumpDevice(Guid.NewGuid(), "Source Device", 100, 0, _logger);
            var targetDevice = new PumpDevice(Guid.NewGuid(), "Target Device", 100, 0, _logger);

            // Add devices to persistence service
            await _persistenceService.AddOrUpdateAsync(sourceDevice);
            await _persistenceService.AddOrUpdateAsync(targetDevice);

            // Set the FlowRate property on source device to trigger the condition
            await sourceDevice.SetPropertyAsync("FlowRate", 75);

            // Create a connection from source to target with a condition
            var connection = new ComponentConnection
            {
                SourceId = sourceDevice.Id,
                TargetId = targetDevice.Id,
                ConnectionType = "TestConnection",
                IsEnabled = true,
                Condition = "source.FlowRate > 50"  // This condition should evaluate to true
            };

            await _topologyService.CreateConnectionAsync(connection);

            // Setup a mock handler to capture events
            var targetEvents = new List<IHydroGardenPropertyChangedEvent>();
            var mockHandler = new Mock<IHydroGardenPropertyChangedEventHandler>();
            mockHandler.Setup(h => h.HandleEventAsync(
                It.IsAny<object>(),
                It.IsAny<IHydroGardenPropertyChangedEvent>(),
                It.IsAny<CancellationToken>()))
                .Callback<object, IHydroGardenPropertyChangedEvent, CancellationToken>(
                    (_, evt, __) => targetEvents.Add(evt))
                .Returns(Task.CompletedTask);

            // Subscribe the handler to events from the target device, but include connected sources
            var options = new EventSubscriptionOptions
            {
                EventTypes = new[] { EventType.PropertyChanged },
                SourceIds = new[] { targetDevice.Id },
                IncludeConnectedSources = true,
                Synchronous = true
            };

            _eventBus.Subscribe(mockHandler.Object, options);

            // Act - Create an event from the source device
            var sourceEvent = new HydroGardenPropertyChangedEvent(
                sourceDevice.Id,
                sourceDevice.Id,
                "TestProperty",
                typeof(string),
                null,
                "TestValue",
                new PropertyMetadata());

            // Publish the event through the event bus
            await _eventBus.PublishAsync(this, sourceEvent);

            // Wait a short time for async processing
            await Task.Delay(100);

            // Assert - The event should have been routed to the target device's handler
            targetEvents.Should().NotBeEmpty();
            targetEvents.Should().Contain(e => e.SourceId == sourceDevice.Id);

            // Now update the connection with a condition that will be false
            connection.Condition = "source.FlowRate > 80";  // This condition should evaluate to false
            await _topologyService.UpdateConnectionAsync(connection);

            // Clear the events
            targetEvents.Clear();

            // Publish another event
            var sourceEvent2 = new HydroGardenPropertyChangedEvent(
                sourceDevice.Id,
                sourceDevice.Id,
                "TestProperty2",
                typeof(string),
                null,
                "TestValue2",
                new PropertyMetadata());

            await _eventBus.PublishAsync(this, sourceEvent2);

            // Wait a short time for async processing
            await Task.Delay(100);

            // Assert - The event should NOT have been routed due to the condition
            targetEvents.Should().BeEmpty();
        }

        [Fact]
        public async Task EventBus_ShouldNotRouteEventsWhenConnectionIsDisabled()
        {
            // Arrange - Create two test devices
            var sourceDevice = new PumpDevice(Guid.NewGuid(), "Source Device", 100, 0, _logger);
            var targetDevice = new PumpDevice(Guid.NewGuid(), "Target Device", 100, 0, _logger);

            // Add devices to persistence service
            await _persistenceService.AddOrUpdateAsync(sourceDevice);
            await _persistenceService.AddOrUpdateAsync(targetDevice);

            // Create a connection from source to target that's initially disabled
            var connection = new ComponentConnection
            {
                SourceId = sourceDevice.Id,
                TargetId = targetDevice.Id,
                ConnectionType = "TestConnection",
                IsEnabled = false  // Connection is disabled
            };

            await _topologyService.CreateConnectionAsync(connection);

            // Setup a mock handler to capture events
            var targetEvents = new List<IHydroGardenPropertyChangedEvent>();
            var mockHandler = new Mock<IHydroGardenPropertyChangedEventHandler>();
            mockHandler.Setup(h => h.HandleEventAsync(
                It.IsAny<object>(),
                It.IsAny<IHydroGardenPropertyChangedEvent>(),
                It.IsAny<CancellationToken>()))
                .Callback<object, IHydroGardenPropertyChangedEvent, CancellationToken>(
                    (_, evt, __) => targetEvents.Add(evt))
                .Returns(Task.CompletedTask);

            // Subscribe the handler to events from the target device, but include connected sources
            var options = new EventSubscriptionOptions
            {
                EventTypes = new[] { EventType.PropertyChanged },
                SourceIds = new[] { targetDevice.Id },
                IncludeConnectedSources = true,
                Synchronous = true
            };

            _eventBus.Subscribe(mockHandler.Object, options);

            // Act - Create an event from the source device
            var sourceEvent = new HydroGardenPropertyChangedEvent(
                sourceDevice.Id,
                sourceDevice.Id,
                "TestProperty",
                typeof(string),
                null,
                "TestValue",
                new PropertyMetadata());

            // Publish the event through the event bus
            await _eventBus.PublishAsync(this, sourceEvent);

            // Wait a short time for async processing
            await Task.Delay(100);

            // Assert - The event should NOT have been routed because the connection is disabled
            targetEvents.Should().BeEmpty();

            // Now enable the connection
            connection.IsEnabled = true;
            await _topologyService.UpdateConnectionAsync(connection);

            // Publish another event
            var sourceEvent2 = new HydroGardenPropertyChangedEvent(
                sourceDevice.Id,
                sourceDevice.Id,
                "TestProperty2",
                typeof(string),
                null,
                "TestValue2",
                new PropertyMetadata());

            await _eventBus.PublishAsync(this, sourceEvent2);

            // Wait a short time for async processing
            await Task.Delay(100);

            // Assert - Now the event should be routed
            targetEvents.Should().NotBeEmpty();
            targetEvents.Should().Contain(e => e.SourceId == sourceDevice.Id);
        }

        [Fact]
        public async Task TopologyService_ShouldPersistConnectionsBetweenRestarts()
        {
            // Arrange - Create test devices
            var sourceId = Guid.NewGuid();
            var targetId = Guid.NewGuid();

            // Create a connection
            var connection = new ComponentConnection
            {
                SourceId = sourceId,
                TargetId = targetId,
                ConnectionType = "TestConnection",
                IsEnabled = true
            };

            // Save the connection with the first instance of the service
            await _topologyService.CreateConnectionAsync(connection);

            // Create a new instance of the service (simulating a restart)
            var newTopologyService = new TopologyService(_logger, _store, _persistenceService);
            _disposables.Add(newTopologyService);

            // IMPORTANT: Initialize the new service explicitly
            await newTopologyService.InitializeAsync();

            // Act - Get connections from the new service instance
            var sourceConnections = await newTopologyService.GetConnectionsForSourceAsync(sourceId);
            var targetConnections = await newTopologyService.GetConnectionsForTargetAsync(targetId);

            // Assert - The connections should be loaded from persistence
            sourceConnections.Should().HaveCount(1);
            sourceConnections[0].SourceId.Should().Be(sourceId);
            sourceConnections[0].TargetId.Should().Be(targetId);

            targetConnections.Should().HaveCount(1);
            targetConnections[0].SourceId.Should().Be(sourceId);
            targetConnections[0].TargetId.Should().Be(targetId);
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var disposable in _disposables)
            {
                await disposable.DisposeAsync();
            }

            await _persistenceService.DisposeAsync();

            // Clean up the test directory
            try
            {
                if (Directory.Exists(_testDirectory))
                {
                    Directory.Delete(_testDirectory, true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }

            await ValueTask.CompletedTask;
        }
    }
}