using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using HydroGarden.Foundation.Abstractions.Interfaces;
using HydroGarden.Foundation.Core.Components.Devices;
using Moq;
using Moq.Protected;
using Xunit;

namespace HydroGarden.Foundation.Tests.Unit.Devices
{
    public class IoTDeviceBaseTests
    {
        private class TestIoTDevice : IoTDeviceBase
        {
            public bool InitializeCalled { get; private set; }
            public bool StartCalled { get; private set; }
            public bool StopCalled { get; private set; }

            public TestIoTDevice(Guid id, string name, IHydroGardenLogger logger)
                : base(id, name, logger)
            {
            }

            protected override Task OnInitializeAsync(CancellationToken ct)
            {
                InitializeCalled = true;
                return base.OnInitializeAsync(ct);
            }

            protected override Task OnStartAsync(CancellationToken ct)
            {
                StartCalled = true;
                return base.OnStartAsync(ct);
            }

            protected override Task OnStopAsync(CancellationToken ct)
            {
                StopCalled = true;
                return base.OnStopAsync(ct);
            }
        }

        private readonly Mock<IHydroGardenLogger> _mockLogger;
        private readonly Mock<IHydroGardenEventHandler> _mockEventHandler;
        private readonly Guid _testId;
        private readonly string _testName;
        private readonly TestIoTDevice _sut;

        public IoTDeviceBaseTests()
        {
            _mockLogger = new Mock<IHydroGardenLogger>();
            _mockEventHandler = new Mock<IHydroGardenEventHandler>();
            _testId = Guid.NewGuid();
            _testName = "Test IoT Device";
            _sut = new TestIoTDevice(_testId, _testName, _mockLogger.Object);
            _sut.SetEventHandler(_mockEventHandler.Object);

            // Setup event handler to handle any events
            _mockEventHandler
                .Setup(e => e.HandleEventAsync(
                    It.IsAny<object>(),
                    It.IsAny<IHydroGardenPropertyChangedEvent>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        }

        [Fact]
        public async Task InitializeAsync_ShouldSetStateToInitializing()
        {
            // Act
            await ((IIoTDevice)_sut).InitializeAsync();

            // Assert
            _sut.InitializeCalled.Should().BeTrue();
            _sut.State.Should().Be(ComponentState.Ready);
        }

        [Fact]
        public async Task InitializeAsync_ShouldSetCommonProperties()
        {
            // Act
            await ((IIoTDevice)_sut).InitializeAsync();

            // Assert
            var idProperty = await _sut.GetPropertyAsync<Guid>("Id");
            var nameProperty = await _sut.GetPropertyAsync<string>("Name");
            var typeProperty = await _sut.GetPropertyAsync<string>("AssemblyType");
            var stateProperty = await _sut.GetPropertyAsync<ComponentState>("State");

            idProperty.Should().Be(_testId);
            nameProperty.Should().Be(_testName);
            Type type = typeof(TestIoTDevice);
            typeProperty.Should().Be(type.FullName);
            stateProperty.Should().Be(ComponentState.Ready);
        }

        [Fact]
        public async Task InitializeAsync_WhenAlreadyInitialized_ShouldThrowException()
        {
            // Arrange
            await ((IIoTDevice)_sut).InitializeAsync();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await ((IIoTDevice)_sut).InitializeAsync());

            ex.Message.Should().Contain("Cannot initialize device in state");
        }

        [Fact]
        public async Task StartAsync_ShouldSetStateToRunning()
        {
            // Arrange
            await ((IIoTDevice)_sut).InitializeAsync();

            // Act
            await ((IIoTDevice)_sut).StartAsync();

            // Assert
            _sut.StartCalled.Should().BeTrue();
            _sut.State.Should().Be(ComponentState.Running);
        }

        [Fact]
        public async Task StartAsync_WhenNotInitialized_ShouldThrowException()
        {
            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await ((IIoTDevice)_sut).StartAsync());

            ex.Message.Should().Contain("Cannot start device in state");
        }

        [Fact]
        public async Task StopAsync_ShouldSetStateToReady()
        {
            // Arrange
            await ((IIoTDevice)_sut).InitializeAsync();
            await ((IIoTDevice)_sut).StartAsync();

            // Act
            await ((IIoTDevice)_sut).StopAsync();

            // Assert
            _sut.StopCalled.Should().BeTrue();
            _sut.State.Should().Be(ComponentState.Ready);
        }

        [Fact]
        public async Task StopAsync_WhenNotRunning_ShouldReturnWithoutStateChange()
        {
            // Arrange
            await ((IIoTDevice)_sut).InitializeAsync();

            // Act
            await ((IIoTDevice)_sut).StopAsync();

            // Assert
            _sut.StopCalled.Should().BeFalse();
            _sut.State.Should().Be(ComponentState.Ready);
        }

        [Fact]
        public async Task Dispose_ShouldCleanUpResources()
        {
            // Arrange
            await ((IIoTDevice)_sut).InitializeAsync();
            await ((IIoTDevice)_sut).StartAsync();

            // Act
            _sut.Dispose();

            // Assert
            _sut.State.Should().Be(ComponentState.Disposed);
        }

        [Fact]
        public async Task FullLifecycle_ShouldTransitionThroughStatesProperly()
        {
            // Arrange & Act - Initial state
            _sut.State.Should().Be(ComponentState.Created);

            // Initialize
            await ((IIoTDevice)_sut).InitializeAsync();
            _sut.State.Should().Be(ComponentState.Ready);

            // Start
            await ((IIoTDevice)_sut).StartAsync();
            _sut.State.Should().Be(ComponentState.Running);

            // Stop
            await ((IIoTDevice)_sut).StopAsync();
            _sut.State.Should().Be(ComponentState.Ready);

            // Dispose
            _sut.Dispose();
            _sut.State.Should().Be(ComponentState.Disposed);
        }

        [Fact]
        public async Task InitializeAsync_OnInitializeException_ShouldSetStateToError()
        {
            // Arrange - use a mock for direct interface access
            var mockDevice = new Mock<IoTDeviceBase>(_testId, _testName, _mockLogger.Object) { CallBase = true };

            // Setup the method to throw
            mockDevice.Protected()
                .Setup<Task>("OnInitializeAsync", ItExpr.IsAny<CancellationToken>())
                .Throws(new Exception("Test exception"));

            // Setup event handler
            mockDevice.Object.SetEventHandler(_mockEventHandler.Object);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<Exception>(async () =>
                await ((IIoTDevice)mockDevice.Object).InitializeAsync());

            ex.Message.Should().Be("Test exception");

            // Verify state changed to Error
            mockDevice.Object.State.Should().Be(ComponentState.Error);
        }

        [Fact]
        public async Task StartAsync_OnStartException_ShouldSetStateToError()
        {
            // Arrange - use a mock that allows the base implementation
            var mockDevice = new Mock<IoTDeviceBase>(_testId, _testName, _mockLogger.Object) { CallBase = true };

            // Setup InitializeAsync to work normally
            await ((IIoTDevice)mockDevice.Object).InitializeAsync();

            // Then setup StartAsync to throw
            mockDevice.Protected()
                .Setup<Task>("OnStartAsync", ItExpr.IsAny<CancellationToken>())
                .Throws(new Exception("Test exception"));

            // Act & Assert
            var ex = await Assert.ThrowsAsync<Exception>(async () =>
                await ((IIoTDevice)mockDevice.Object).StartAsync());

            ex.Message.Should().Be("Test exception");

            // Verify state changed to Error
            mockDevice.Object.State.Should().Be(ComponentState.Error);
        }
    }
}