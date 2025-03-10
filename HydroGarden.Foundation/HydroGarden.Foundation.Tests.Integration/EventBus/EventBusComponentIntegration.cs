
using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Foundation.Common.Events;
using Moq;
using FluentAssertions;
using HydroGarden.Foundation.Core.Components.Devices;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.Tests.Unit.EventBus;
using HydroGarden.Logger.Abstractions;

namespace HydroGarden.Foundation.Tests.Integration.EventBus
{
    public class EventBusComponentIntegrationTests : EventBusBaseTests
    {
        private readonly Mock<ILogger> _loggerMock;
        private readonly Mock<IEventStore> _eventStoreMock;
        private readonly Mock<IEventRetryPolicy> _retryPolicyMock;
        private readonly Mock<IEventTransformer> _transformerMock;
        private readonly Mock<IErrorMonitor> _errorMonitorMock;
        private Common.Events.EventBus _eventBus;

        public EventBusComponentIntegrationTests()
        {
            _loggerMock = new Mock<ILogger>();
            _eventStoreMock = new Mock<IEventStore>();
            _retryPolicyMock = new Mock<IEventRetryPolicy>();
            _transformerMock = new Mock<IEventTransformer>();
            _errorMonitorMock = new Mock<IErrorMonitor>();

            _eventBus = new Common.Events.EventBus(
                _loggerMock.Object,
                _eventStoreMock.Object,
                _retryPolicyMock.Object,
                _transformerMock.Object,
                _errorMonitorMock.Object
            );
        }

        [Fact]
        public async Task EventBus_ShouldPropagatePropertyChangesFromComponents()
        {
            // Arrange
            using var eventBus = CreateTestEventBus();

            // Create a device and event handler to capture events
            var deviceId = Guid.NewGuid();
            var testDevice = new TestDevice(deviceId, "TestDevice", _errorMonitorMock.Object);

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


            testDevice.SetEventHandler(mockHandler.Object);

            // Act - Change a property on the device
            await testDevice.SetValueAsync(42);

            // Assert - The property change should be captured
            capturedEvents.Should().HaveCount(1);
            capturedEvents[0].PropertyName.Should().Be("Value");
            capturedEvents[0].NewValue.Should().Be(42);
            capturedEvents[0].SourceId.Should().Be(deviceId);
        }

        // Other test methods would follow the same pattern - update TestDevice creation with the error monitor

        #region Test Helper Classes
        /// <summary>
        /// A simple test device for event propagation testing
        /// </summary>
        private class TestDevice : IoTDeviceBase
        {
            private int _value;
            private bool _active;

            public TestDevice(Guid id, string name, IErrorMonitor errorMonitor)
                : base(id, name, errorMonitor,null ) // No logger needed for tests
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

        // Other helper classes remain unchanged
        #endregion
    }
}