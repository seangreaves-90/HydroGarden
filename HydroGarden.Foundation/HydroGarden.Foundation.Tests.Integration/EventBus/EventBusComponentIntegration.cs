using HydroGarden.Foundation.Abstractions.Interfaces.Components;
using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Foundation.Common.Events;
using Moq;
using FluentAssertions;
using HydroGarden.Foundation.Tests.Unit.Events;
using HydroGarden.Foundation.Core.Components.Devices;
using HydroGarden.Foundation.Abstractions.Interfaces.Logging;
using HydroGarden.Foundation.Common.PropertyMetadata;


namespace HydroGarden.Foundation.Tests.Integration.EventBus
{
    /// <summary>
    /// Tests for EventBus integration with components.
    /// These tests verify the end-to-end event flow, from property changes in components
    /// through the EventBus to subscribers.
    /// </summary>
    public class EventBusComponentIntegrationTests : EventBusBaseTests
    {
        private readonly Mock<ILogger> _loggerMock;
        private readonly Mock<IEventStore> _eventStoreMock;
        private readonly Mock<IEventRetryPolicy> _retryPolicyMock;
        private readonly Mock<IEventTransformer> _transformerMock;
        private Common.Events.EventBus _eventBus;

        public EventBusComponentIntegrationTests()
        {
            _loggerMock = new Mock<ILogger>();
            _eventStoreMock = new Mock<IEventStore>();
            _retryPolicyMock = new Mock<IEventRetryPolicy>();
            _transformerMock = new Mock<IEventTransformer>();

            _eventBus = new Common.Events.EventBus(
                _loggerMock.Object,
                _eventStoreMock.Object,
                _retryPolicyMock.Object,
                _transformerMock.Object
            );
        }

        [Fact]
        public async Task EventBus_ShouldPropagatePropertyChangesFromComponents()
        {
            // Arrange
            using var eventBus = CreateTestEventBus();

            // Create a device and event handler to capture events
            var deviceId = Guid.NewGuid();
            var testDevice = new TestDevice(deviceId, "TestDevice");

            var capturedEvents = new List<IPropertyChangedEvent>();
            var mockHandler = new Mock<IPropertyChangedEventHandler>();
            mockHandler.Setup(h => h.HandleEventAsync(
                It.IsAny<object>(),
                It.IsAny<IPropertyChangedEvent>(),
                It.IsAny<CancellationToken>()))
                .Callback<object, IPropertyChangedEvent, CancellationToken>(
                    (_, evt, __) => capturedEvents.Add(evt))
                .Returns(Task.CompletedTask);

            // Subscribe to events from the device
            var options = new EventSubscriptionOptions
            {
                EventTypes = new[] { EventType.PropertyChanged },
                SourceIds = new[] { deviceId },
                Synchronous = true
            };

            eventBus.Subscribe(mockHandler.Object, options);

            // Set the event handler on the device
            testDevice.SetEventHandler(new EventRelayHandler(eventBus));

            // Act - Change a property on the device
            await testDevice.SetValueAsync(42);

            // Assert - The property change should be captured
            capturedEvents.Should().HaveCount(1);
            capturedEvents[0].PropertyName.Should().Be("Value");
            capturedEvents[0].NewValue.Should().Be(42);
            capturedEvents[0].SourceId.Should().Be(deviceId);
        }

        [Fact]
        public async Task EventBus_ShouldPropagateMultiplePropertyChangesFromComponents()
        {
            // Arrange
            using var eventBus = CreateTestEventBus();

            var deviceId = Guid.NewGuid();
            var testDevice = new TestDevice(deviceId, "TestDevice");

            var capturedEvents = new List<IPropertyChangedEvent>();
            var mockHandler = new Mock<IPropertyChangedEventHandler>();
            mockHandler.Setup(h => h.HandleEventAsync(
                It.IsAny<object>(),
                It.IsAny<IPropertyChangedEvent>(),
                It.IsAny<CancellationToken>()))
                .Callback<object, IPropertyChangedEvent, CancellationToken>(
                    (_, evt, __) => capturedEvents.Add(evt))
                .Returns(Task.CompletedTask);

            // Subscribe to events from the device
            var options = new EventSubscriptionOptions
            {
                EventTypes = new[] { EventType.PropertyChanged },
                SourceIds = new[] { deviceId },
                Synchronous = true
            };

            eventBus.Subscribe(mockHandler.Object, options);
            testDevice.SetEventHandler(new EventRelayHandler(eventBus));

            // Act - Change multiple properties
            await testDevice.SetValueAsync(42);
            await testDevice.SetNameAsync("RenamedDevice");
            await testDevice.SetActiveAsync(true);

            // Assert - All property changes should be captured
            capturedEvents.Should().HaveCount(3);

            // Verify first property change (Value)
            capturedEvents[0].PropertyName.Should().Be("Value");
            capturedEvents[0].NewValue.Should().Be(42);

            // Verify second property change (Name)
            capturedEvents[1].PropertyName.Should().Be("Name");
            capturedEvents[1].NewValue.Should().Be("RenamedDevice");

            // Verify third property change (Active)
            capturedEvents[2].PropertyName.Should().Be("Active");
            capturedEvents[2].NewValue.Should().Be(true);
        }

        [Fact]
        public async Task EventBus_ComponentChanges_ShouldTriggerMultipleHandlers()
        {
            // Arrange
            using var eventBus = CreateTestEventBus();

            var deviceId = Guid.NewGuid();
            var testDevice = new TestDevice(deviceId, "TestDevice");

            // Create two separate handlers
            var handler1Events = new List<IPropertyChangedEvent>();
            var mockHandler1 = new Mock<IPropertyChangedEventHandler>();
            mockHandler1.Setup(h => h.HandleEventAsync(
                It.IsAny<object>(),
                It.IsAny<IPropertyChangedEvent>(),
                It.IsAny<CancellationToken>()))
                .Callback<object, IPropertyChangedEvent, CancellationToken>(
                    (_, evt, __) => handler1Events.Add(evt))
                .Returns(Task.CompletedTask);

            var handler2Events = new List<IPropertyChangedEvent>();
            var mockHandler2 = new Mock<IPropertyChangedEventHandler>();
            mockHandler2.Setup(h => h.HandleEventAsync(
                It.IsAny<object>(),
                It.IsAny<IPropertyChangedEvent>(),
                It.IsAny<CancellationToken>()))
                .Callback<object, IPropertyChangedEvent, CancellationToken>(
                    (_, evt, __) => handler2Events.Add(evt))
                .Returns(Task.CompletedTask);

            // Subscribe both handlers with different criteria
            var options1 = new EventSubscriptionOptions
            {
                EventTypes = new[] { EventType.PropertyChanged },
                SourceIds = new[] { deviceId },
                Synchronous = true
            };

            var options2 = new EventSubscriptionOptions
            {
                EventTypes = new[] { EventType.PropertyChanged },
                Filter = evt => evt is IPropertyChangedEvent propEvt &&
                               propEvt.PropertyName == "Value",
                Synchronous = true
            };

            eventBus.Subscribe(mockHandler1.Object, options1); // All property events
            eventBus.Subscribe(mockHandler2.Object, options2); // Only Value property

            testDevice.SetEventHandler(new EventRelayHandler(eventBus));

            // Act - Change multiple properties
            await testDevice.SetValueAsync(42);
            await testDevice.SetActiveAsync(true);

            // Assert - Handler 1 should receive both events, Handler 2 only the Value event
            handler1Events.Should().HaveCount(2);
            handler2Events.Should().HaveCount(1);

            handler2Events[0].PropertyName.Should().Be("Value");
        }

        [Fact]
        public async Task EventBus_ComponentCascadingChanges_ShouldPropagate()
        {
            // Arrange
            using var eventBus = CreateTestEventBus();

            // Create two devices with a relationship
            var parentId = Guid.NewGuid();
            var childId = Guid.NewGuid();

            var parentDevice = new TestDevice(parentId, "ParentDevice");
            var childDevice = new TestDevice(childId, "ChildDevice");

            // Set up event capture
            var capturedEvents = new List<IPropertyChangedEvent>();
            var mockHandler = new Mock<IPropertyChangedEventHandler>();
            mockHandler.Setup(h => h.HandleEventAsync(
                It.IsAny<object>(),
                It.IsAny<IPropertyChangedEvent>(),
                It.IsAny<CancellationToken>()))
                .Callback<object, IPropertyChangedEvent, CancellationToken>(
                    (_, evt, __) => capturedEvents.Add(evt))
                .Returns(Task.CompletedTask);

            // Subscribe to events from both devices
            var options = new EventSubscriptionOptions
            {
                EventTypes = new[] { EventType.PropertyChanged },
                Synchronous = true
            };

            eventBus.Subscribe(mockHandler.Object, options);

            // Set up handlers and link devices
            parentDevice.SetEventHandler(new EventRelayHandler(eventBus));
            childDevice.SetEventHandler(new EventRelayHandler(eventBus));

            // Create a cascading change handler that updates the child when parent changes
            var cascadingHandler = new CascadingChangeHandler(childDevice);
            eventBus.Subscribe(cascadingHandler, new EventSubscriptionOptions
            {
                EventTypes = new[] { EventType.PropertyChanged },
                SourceIds = new[] { parentId },
                Filter = evt => evt is IPropertyChangedEvent propEvt &&
                               propEvt.PropertyName == "Value",
                Synchronous = true
            });

            // Act - Change parent value which should trigger child update
            await parentDevice.SetValueAsync(100);

            // Assert - Both parent and child events should be captured
            capturedEvents.Should().HaveCountGreaterThanOrEqualTo(2);

            // Find the child event
            var childEvent = capturedEvents.FirstOrDefault(e =>
                e.SourceId == childId && e.PropertyName == "Value");

            childEvent.Should().NotBeNull("Child device should have received cascaded change");
            childEvent.NewValue.Should().Be(50, "Child value should be half of parent value");
        }


        [Fact]
        public async Task EventBus_ComponentState_ShouldBeTrackedThroughEvents()
        {
            // Arrange
            using var eventBus = CreateTestEventBus();
            var deviceId = Guid.NewGuid();
            var stateChanges = new List<ComponentState>();
            var completionSource = new TaskCompletionSource<bool>();

            // Create a properly implemented handler
            var lifecycleHandler = new HydroGardenLifecycleChangedEvent(stateChanges, completionSource);

            // Subscribe with options that include both event types to ensure compatibility
            var options = new EventSubscriptionOptions
            {
                EventTypes = new[] { EventType.PropertyChanged, EventType.Lifecycle },
                SourceIds = new[] { deviceId },
                Synchronous = true
            };

            // Subscribe the handler to the event bus
            eventBus.Subscribe(lifecycleHandler, options);

            // Create property change events that represent state changes
            var metadata = new PropertyMetadata(true, true, "State", "Component state");

            // Act - Publish events that will represent state transitions
            await eventBus.PublishAsync(this, new HydroGardenPropertyChangedEvent(
                deviceId, deviceId, "State", typeof(ComponentState),
                null, ComponentState.Initializing, metadata));

            await eventBus.PublishAsync(this, new HydroGardenPropertyChangedEvent(
                deviceId, deviceId, "State", typeof(ComponentState),
                ComponentState.Initializing, ComponentState.Ready, metadata));

            await eventBus.PublishAsync(this, new HydroGardenPropertyChangedEvent(
                deviceId, deviceId, "State", typeof(ComponentState),
                ComponentState.Ready, ComponentState.Running, metadata));

            await eventBus.PublishAsync(this, new HydroGardenPropertyChangedEvent(
                deviceId, deviceId, "State", typeof(ComponentState),
                ComponentState.Running, ComponentState.Stopping, metadata));

            await eventBus.PublishAsync(this, new HydroGardenPropertyChangedEvent(
                deviceId, deviceId, "State", typeof(ComponentState),
                ComponentState.Stopping, ComponentState.Disposed, metadata));

            // Wait up to 500ms for the completion source to be signaled
            await Task.WhenAny(completionSource.Task, Task.Delay(500));

            // Assert
            Assert.True(stateChanges.Count >= 5, $"Expected at least 5 state changes, found {stateChanges.Count}.");

            // Additional assertions to verify the sequence
            Assert.Equal(ComponentState.Initializing, stateChanges[0]);
            Assert.Equal(ComponentState.Ready, stateChanges[1]);
            Assert.Equal(ComponentState.Running, stateChanges[2]);
            Assert.Equal(ComponentState.Stopping, stateChanges[3]);
            Assert.Equal(ComponentState.Disposed, stateChanges[4]);
        }
        #region Test Helper Classes
        /// <summary>
        /// A simple test device for event propagation testing
        /// </summary>
        private class TestDevice : IoTDeviceBase
        {
            private int _value;
            private bool _active;

            public TestDevice(Guid id, string name)
                : base(id, name, null) // No logger needed for tests
            {
            }

            public async Task SetValueAsync(int value)
            {
                _value = value;
                await SetPropertyAsync("Value", value);
            }

            public async Task SetNameAsync(string name)
            {
                await SetPropertyAsync("Name", name);
            }

            public async Task SetActiveAsync(bool active)
            {
                _active = active;
                await SetPropertyAsync("Active", active);
            }

            protected override async Task OnInitializeAsync(CancellationToken ct)
            {
                // Default initialization
                await SetPropertyAsync("Value", 0);
                await SetPropertyAsync("Active", false);
                await base.OnInitializeAsync(ct);
            }

            protected override async Task OnStartAsync(CancellationToken ct)
            {
                // Start implementation that publishes events via event handler
                await SetActiveAsync(true);
                await base.OnStartAsync(ct);
            }

            protected override async Task OnStopAsync(CancellationToken ct)
            {
                // Stop implementation that publishes events via event handler
                await SetActiveAsync(false);
                await base.OnStopAsync(ct);
            }
        }

        /// <summary>
        /// An event handler that relays events to the EventBus
        /// </summary>
        private class EventRelayHandler : IPropertyChangedEventHandler
        {
            private readonly IEventBus _eventBus;

            public EventRelayHandler(IEventBus eventBus)
            {
                _eventBus = eventBus;
            }

            public async Task HandleEventAsync<T>(object sender, T evt, CancellationToken ct = default) where T : IEvent
            {
                if (evt is IPropertyChangedEvent e)
                {
                    await _eventBus.PublishAsync(sender, e, ct);
                }
            }

            public ValueTask DisposeAsync()
            {
                return ValueTask.CompletedTask;
            }
        }
        /// <summary>
        /// An event handler that demonstrates cascading changes between components
        /// </summary>
        private class CascadingChangeHandler : IPropertyChangedEventHandler
        {
            private readonly TestDevice _targetDevice;

            public CascadingChangeHandler(TestDevice targetDevice)
            {
                _targetDevice = targetDevice;
            }

            public async Task HandleEventAsync<T>(object sender, T evt, CancellationToken ct = default) where T : IEvent
            {
                if (evt is IPropertyChangedEvent e && e.PropertyName == "Value" && e.NewValue is int parentValue)
                {
                    // Child value is half of parent value
                    await _targetDevice.SetValueAsync(parentValue / 2);
                }
            }

            public ValueTask DisposeAsync()
            {
                return ValueTask.CompletedTask;
            }
        }
        public class LifecycleEventHandler : IPropertyChangedEventHandler
        {
            private readonly List<ComponentState> _stateChanges;
            private readonly TaskCompletionSource<bool> _completionSource;

            public LifecycleEventHandler(List<ComponentState> stateChanges, TaskCompletionSource<bool> completionSource)
            {
                _stateChanges = stateChanges;
                _completionSource = completionSource;
            }

            public Task HandleEventAsync<T>(object sender, T evt, CancellationToken ct = default) where T : IEvent
            {
                // This won't be called in our test
                return Task.CompletedTask;
            }

            // Add this method to handle lifecycle events directly
            public Task HandleLifecycleEventAsync(object sender, ILifecycleEvent evt, CancellationToken ct = default)
            {
                _stateChanges.Add(evt.State);
                if (_stateChanges.Count >= 5)
                    _completionSource.SetResult(true);
                return Task.CompletedTask;
            }

            public ValueTask DisposeAsync()
            {
                return ValueTask.CompletedTask;
            }

        }

        #endregion
    }
}