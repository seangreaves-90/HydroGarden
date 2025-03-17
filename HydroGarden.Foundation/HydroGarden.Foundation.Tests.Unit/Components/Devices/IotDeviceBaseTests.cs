using FluentAssertions;
using HydroGarden.Foundation.Abstractions.Interfaces;
using HydroGarden.Foundation.Abstractions.Interfaces.Components;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Foundation.Common.Events;
using HydroGarden.Foundation.Core.Components.Devices;
using HydroGarden.Logger.Abstractions;
using Moq;
using Xunit;

namespace HydroGarden.Foundation.Tests.Unit.Devices
{
    public class IoTDeviceBaseTests
    {
        private class TestIoTDevice : IoTDeviceBase
        {
            public bool OnInitializeCalled { get; private set; }
            public bool OnStartCalled { get; private set; }
            public bool OnStopCalled { get; private set; }
            public bool OnRecoverCalled { get; private set; }
            public bool OnDeviceErrorCalled { get; private set; }
            
            public bool ShouldFailOnRecover { get; set; }
            
            // Flag to control recovery success/failure
            public bool ShouldRecoverySucceed { get; set; } = true;
            
            // Make the method public for testing
            public new Task<bool> ThrottleRecoveryAttemptsAsync(string errorCode, CancellationToken ct = default)
            {
                return base.ThrottleRecoveryAttemptsAsync(errorCode, ct);
            }

            public TestIoTDevice(Guid id, string name, IErrorMonitor errorMonitor, IEventBus? eventBus = null, ILogger? logger = null)
                : base(id, name, errorMonitor, eventBus, logger)
            {
                ShouldFailOnRecover = false;
            }

            protected override async Task<bool> OnInitializeAsync(CancellationToken ct)
            {
                OnInitializeCalled = true;
                return await base.OnInitializeAsync(ct);
            }

            protected override async Task<bool> OnStartAsync(CancellationToken ct)
            {
                OnStartCalled = true;
                return await base.OnStartAsync(ct);
            }

            protected override async Task<bool> OnStopAsync(CancellationToken ct)
            {
                OnStopCalled = true;
                return await base.OnStopAsync(ct);
            }
            
            protected override async Task<bool> OnTryRecoverAsync(CancellationToken ct)
            {
                OnRecoverCalled = true;
                
                if (ShouldFailOnRecover)
                {
                    throw new Exception("Test recovery exception");
                }
                
                return await Task.FromResult(ShouldRecoverySucceed);
            }
            
            protected override Task OnDeviceErrorAsync(IApplicationError error, CancellationToken ct)
            {
                OnDeviceErrorCalled = true;
                return base.OnDeviceErrorAsync(error, ct);
            }
            
            // Expose method for testing
            public new IAlertEvent MapToAlertEvent(IApplicationError error)
            {
                return base.MapToAlertEvent(error);
            }
        }

        private readonly Mock<ILogger> _mockLogger;
        private readonly Mock<IPropertyChangedEventHandler<IEvent>> _mockEventHandler;
        private readonly Mock<IErrorMonitor> _mockErrorMonitor;
        private readonly Mock<IEventBus> _mockEventBus;
        private readonly Guid _testId;
        private readonly string _testName;
        private readonly TestIoTDevice _sut;
        private readonly IIoTDevice _iIoTDeviceSut; // For testing interface implementation

        public IoTDeviceBaseTests()
        {
            _mockLogger = new Mock<ILogger>();
            _mockEventHandler = new Mock<IPropertyChangedEventHandler<IEvent>>();
            _mockErrorMonitor = new Mock<IErrorMonitor>();
            _mockEventBus = new Mock<IEventBus>();
            _testId = Guid.NewGuid();
            _testName = "Test IoT Device";
            _sut = new TestIoTDevice(_testId, _testName, _mockErrorMonitor.Object, _mockEventBus.Object, _mockLogger.Object);
            _iIoTDeviceSut = _sut; // Reference to same object but through the interface
            
            // Setup event handler mock
            _mockEventHandler
                .Setup(e => e.HandleEventAsync(
                    It.IsAny<object>(),
                    It.IsAny<IEvent>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
                
            // Setup event bus mock
            _mockEventBus
                .Setup(e => e.PublishAsync(
                    It.IsAny<object>(),
                    It.IsAny<IEvent>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Mock<IPublishResult>().Object);
                
            // Setup error monitor mock
            _mockErrorMonitor
                .Setup(m => m.ReportErrorAsync(
                    It.IsAny<IApplicationError>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
                
            _mockErrorMonitor
                .Setup(m => m.ReportExceptionAsync(
                    It.IsAny<object>(),
                    It.IsAny<Exception>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<ErrorSeverity>(),
                    It.IsAny<ErrorSource>(),
                    It.IsAny<IDictionary<string, object>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
                
            _sut.SetEventHandler(_mockEventHandler.Object);
        }

        [Fact]
        public async Task InitializeAsync_ShouldTransitionThroughCorrectStates()
        {
            // Act
            var result = await _sut.InitializeAsync();

            // Assert
            result.Should().BeTrue();
            _sut.OnInitializeCalled.Should().BeTrue();
            _sut.State.Should().Be(ComponentState.Ready);
            
            // Verify state transitions
            _mockEventBus.Verify(
                e => e.PublishAsync(
                    It.Is<object>(o => o == _sut),
                    It.Is<IStateChangeEvent>(evt => 
                        evt.OldState == ComponentState.Created && 
                        evt.NewState == ComponentState.Initializing),
                    It.IsAny<CancellationToken>()),
                Times.Once);
                
            // Similarly, the Initializing->Ready transition happens during initialization
            _mockEventBus.Verify(
                e => e.PublishAsync(
                    It.Is<object>(o => o == _sut),
                    It.Is<IStateChangeEvent>(evt => 
                        evt.OldState == ComponentState.Initializing && 
                        evt.NewState == ComponentState.Ready),
                    It.IsAny<CancellationToken>()),
                Times.AtLeastOnce);
        }

        [Fact]
        public async Task InitializeAsync_ShouldSetCommonProperties()
        {
            // Act
            await _sut.InitializeAsync();

            // Assert
            var idProperty = await _sut.GetPropertyAsync<Guid>("Id");
            var nameProperty = await _sut.GetPropertyAsync<string>("Name");
            var typeProperty = await _sut.GetPropertyAsync<string>("AssemblyType");
            var stateProperty = await _sut.GetPropertyAsync<ComponentState>("State");
            var deviceTypeProperty = await _sut.GetPropertyAsync<string>("DeviceType");
            var connectionStatusProperty = await _sut.GetPropertyAsync<string>("ConnectionStatus");

            idProperty.Should().Be(_testId);
            nameProperty.Should().Be(_testName);
            typeProperty.Should().Be(typeof(TestIoTDevice).FullName);
            stateProperty.Should().Be(ComponentState.Ready);
            deviceTypeProperty.Should().Be("TestIoTDevice");
            connectionStatusProperty.Should().Be("Disconnected");
        }

        [Fact]
        public async Task StartAsync_ShouldTransitionToRunning()
        {
            // Arrange
            await _sut.InitializeAsync();

            // Act
            var result = await _sut.StartAsync();

            // Assert
            result.Should().BeTrue();
            _sut.OnStartCalled.Should().BeTrue();
            _sut.State.Should().Be(ComponentState.Running);
            
            // Verify connection status property updated
            var connectionStatus = await _sut.GetPropertyAsync<string>("ConnectionStatus");
            connectionStatus.Should().Be("Connected");
            
            // Verify state transition
            _mockEventBus.Verify(
                e => e.PublishAsync(
                    It.Is<object>(o => o == _sut),
                    It.Is<IStateChangeEvent>(evt => 
                        evt.OldState == ComponentState.Ready && 
                        evt.NewState == ComponentState.Running),
                    It.IsAny<CancellationToken>()),
                Times.AtLeastOnce);
        }

        [Fact]
        public async Task StartAsync_WhenNotInitialized_ShouldFail()
        {
            // Act
            var result = await _sut.StartAsync();

            // Assert
            result.Should().BeFalse();
            // State should still be Created since it never initialized
            _sut.State.Should().Be(ComponentState.Created);
        }

        [Fact]
        public async Task StopAsync_ShouldTransitionThroughCorrectStates()
        {
            // Arrange
            await _sut.InitializeAsync();
            await _sut.StartAsync();

            // Act
            var result = await _sut.StopAsync();

            // Assert
            result.Should().BeTrue();
            _sut.OnStopCalled.Should().BeTrue();
            _sut.State.Should().Be(ComponentState.Ready);
            
            // Verify connection status property updated
            var connectionStatus = await _sut.GetPropertyAsync<string>("ConnectionStatus");
            connectionStatus.Should().Be("Disconnected");
            
            // Verify state transitions - use AtLeastOnce since multiple transitions can occur
            _mockEventBus.Verify(
                e => e.PublishAsync(
                    It.Is<object>(o => o == _sut),
                    It.Is<IStateChangeEvent>(evt => 
                        evt.OldState == ComponentState.Running && 
                        evt.NewState == ComponentState.Stopping),
                    It.IsAny<CancellationToken>()),
                Times.AtLeastOnce);
                
            _mockEventBus.Verify(
                e => e.PublishAsync(
                    It.Is<object>(o => o == _sut),
                    It.Is<IStateChangeEvent>(evt => 
                        evt.OldState == ComponentState.Stopping && 
                        evt.NewState == ComponentState.Ready),
                    It.IsAny<CancellationToken>()),
                Times.AtLeastOnce);
        }

        [Fact]
        public async Task FullLifecycle_ShouldTransitionThroughStatesProperly()
        {
            // Initial state
            _sut.State.Should().Be(ComponentState.Created);

            // Initialize
            await _sut.InitializeAsync();
            _sut.State.Should().Be(ComponentState.Ready);

            // Start
            await _sut.StartAsync();
            _sut.State.Should().Be(ComponentState.Running);

            // Stop
            await _sut.StopAsync();
            _sut.State.Should().Be(ComponentState.Ready);

            // Dispose
            _sut.Dispose();
            _sut.State.Should().Be(ComponentState.Disposed);
        }

        [Fact]
        public async Task InterfaceLifecycle_ShouldWorkCorrectly()
        {
            // Test IIoTDevice interface methods
            // Initialize
            await _iIoTDeviceSut.InitializeAsync(CancellationToken.None);
            _sut.State.Should().Be(ComponentState.Ready);
            _sut.OnInitializeCalled.Should().BeTrue();

            // Start
            await _iIoTDeviceSut.StartAsync(CancellationToken.None);
            _sut.State.Should().Be(ComponentState.Running);
            _sut.OnStartCalled.Should().BeTrue();

            // Stop
            await _iIoTDeviceSut.StopAsync(CancellationToken.None);
            _sut.State.Should().Be(ComponentState.Ready);
            _sut.OnStopCalled.Should().BeTrue();
        }

        [Fact]
        public async Task HandleErrorAsync_ShouldTransitionToErrorState()
        {
            // Arrange
            await _sut.InitializeAsync();
            await _sut.StartAsync();
            
            var mockError = new Mock<IApplicationError>();
            mockError.Setup(e => e.DeviceId).Returns(_testId);
            mockError.Setup(e => e.Severity).Returns(ErrorSeverity.Error);
            mockError.Setup(e => e.Message).Returns("Test error");
            mockError.Setup(e => e.ErrorCode).Returns("TEST_ERROR");
            mockError.Setup(e => e.Context).Returns(new Dictionary<string, object>());

            // Act
            var result = await _sut.HandleErrorAsync(mockError.Object);

            // Assert
            result.Should().BeTrue();
            _sut.State.Should().Be(ComponentState.Error);
            _sut.OnDeviceErrorCalled.Should().BeTrue();
            
            // Verify state transition
            _mockEventBus.Verify(
                e => e.PublishAsync(
                    It.Is<object>(o => o == _sut),
                    It.Is<IStateChangeEvent>(evt => 
                        evt.OldState == ComponentState.Running && 
                        evt.NewState == ComponentState.Error),
                    It.IsAny<CancellationToken>()),
                Times.AtLeastOnce);
        }

        [Fact]
        public async Task ReportErrorAsync_WithSevereError_ShouldTransitionToErrorAndPublishAlert()
        {
            // Arrange
            await _sut.InitializeAsync();
            await _sut.StartAsync();
            
            var mockError = new Mock<IApplicationError>();
            mockError.Setup(e => e.DeviceId).Returns(_testId);
            mockError.Setup(e => e.Severity).Returns(ErrorSeverity.Critical);
            mockError.Setup(e => e.Message).Returns("Critical error");
            mockError.Setup(e => e.ErrorCode).Returns("CRITICAL_ERROR");
            mockError.Setup(e => e.Source).Returns(ErrorSource.Device);
            mockError.Setup(e => e.Context).Returns(new Dictionary<string, object>());

            // Act
            await _sut.ReportErrorAsync(mockError.Object);

            // Assert
            _sut.State.Should().Be(ComponentState.Error);
            
            // Verify error reported to monitor
            _mockErrorMonitor.Verify(
                m => m.ReportErrorAsync(
                    It.Is<IApplicationError>(e => e.ErrorCode == "CRITICAL_ERROR"),
                    It.IsAny<CancellationToken>()),
                Times.Once);
                
            // Verify alert published
            _mockEventBus.Verify(
                e => e.PublishAsync(
                    It.Is<object>(o => o == _sut),
                    It.Is<IAlertEvent>(evt => 
                        evt.Severity == AlertSeverity.Critical && 
                        evt.Message == "Critical error"),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task MapToAlertEvent_ShouldMapErrorPropertiesToAlert()
        {
            // Arrange
            var mockError = new Mock<IApplicationError>();
            mockError.Setup(e => e.DeviceId).Returns(_testId);
            mockError.Setup(e => e.Severity).Returns(ErrorSeverity.Error);
            mockError.Setup(e => e.Message).Returns("Test error message");
            mockError.Setup(e => e.ErrorCode).Returns("TEST_ERROR_CODE");
            mockError.Setup(e => e.Source).Returns(ErrorSource.Device);
            mockError.Setup(e => e.Context).Returns(new Dictionary<string, object> { ["TestKey"] = "TestValue" });

            // Act
            var alertEvent = _sut.MapToAlertEvent(mockError.Object);

            // Assert
            alertEvent.Should().NotBeNull();
            alertEvent.DeviceId.Should().Be(_testId);
            alertEvent.Severity.Should().Be(AlertSeverity.Error);
            alertEvent.Message.Should().Be("Test error message");
            alertEvent.AlertData.Should().ContainKey("ErrorCode");
            alertEvent.AlertData["ErrorCode"].Should().Be("TEST_ERROR_CODE");
            alertEvent.AlertData.Should().ContainKey("ErrorSource");
            alertEvent.AlertData.Should().ContainKey("ErrorSeverity");
            alertEvent.AlertData.Should().ContainKey("TestKey");
            alertEvent.AlertData["TestKey"].Should().Be("TestValue");
        }

        [Fact]
        public async Task TryRecoverAsync_WhenSuccessful_ShouldReturnToReadyState()
        {
            // Arrange
            await _sut.InitializeAsync();
            
            var mockError = new Mock<IApplicationError>();
            mockError.Setup(e => e.DeviceId).Returns(_testId);
            mockError.Setup(e => e.Severity).Returns(ErrorSeverity.Error);
            
            // Put into error state
            await _sut.HandleErrorAsync(mockError.Object);
            _sut.State.Should().Be(ComponentState.Error);
            
            _sut.ShouldRecoverySucceed = true;

            // Act
            var result = await _sut.TryRecoverAsync();

            // Assert
            result.Should().BeTrue();
            _sut.OnRecoverCalled.Should().BeTrue();
            _sut.State.Should().Be(ComponentState.Ready);
            
            // Verify state transitions - use AtLeastOnce since the error->initializing transition can happen multiple times
            // during the recovery process (once in HandleErrorAsync and again in recovery)
            _mockEventBus.Verify(
                e => e.PublishAsync(
                    It.Is<object>(o => o == _sut),
                    It.Is<IStateChangeEvent>(evt => 
                        evt.OldState == ComponentState.Error && 
                        evt.NewState == ComponentState.Initializing),
                    It.IsAny<CancellationToken>()),
                Times.AtLeastOnce);
                
            _mockEventBus.Verify(
                e => e.PublishAsync(
                    It.Is<object>(o => o == _sut),
                    It.Is<IStateChangeEvent>(evt => 
                        evt.OldState == ComponentState.Initializing && 
                        evt.NewState == ComponentState.Ready),
                    It.IsAny<CancellationToken>()),
                Times.AtLeastOnce);
        }

        [Fact]
        public async Task TryRecoverAsync_WhenDeviceRecoveryFails_ShouldStayInErrorState()
        {
            // Arrange
            await _sut.InitializeAsync();
            
            var mockError = new Mock<IApplicationError>();
            mockError.Setup(e => e.DeviceId).Returns(_testId);
            mockError.Setup(e => e.Severity).Returns(ErrorSeverity.Error);
            
            // Put into error state
            await _sut.HandleErrorAsync(mockError.Object);
            _sut.State.Should().Be(ComponentState.Error);
            
            // Configure recovery to fail
            _sut.ShouldRecoverySucceed = false;

            // Act
            var result = await _sut.TryRecoverAsync();

            // Assert
            result.Should().BeFalse();
            _sut.OnRecoverCalled.Should().BeTrue();
            _sut.State.Should().Be(ComponentState.Error);
        }

        [Fact]
        public async Task TryRecoverAsync_WhenRecoveryThrowsException_ShouldStayInErrorState()
        {
            // Arrange
            await _sut.InitializeAsync();
            
            var mockError = new Mock<IApplicationError>();
            mockError.Setup(e => e.DeviceId).Returns(_testId);
            mockError.Setup(e => e.Severity).Returns(ErrorSeverity.Error);
            
            // Put into error state
            await _sut.HandleErrorAsync(mockError.Object);
            _sut.State.Should().Be(ComponentState.Error);
            
            // Configure recovery to throw
            _sut.ShouldFailOnRecover = true;

            // Act
            var result = await _sut.TryRecoverAsync();

            // Assert
            result.Should().BeFalse();
            _sut.OnRecoverCalled.Should().BeTrue();
            _sut.State.Should().Be(ComponentState.Error);
            
            // Verify exception reported
            _mockErrorMonitor.Verify(
                m => m.ReportExceptionAsync(
                    It.Is<object>(o => o == _sut),
                    It.IsAny<Exception>(),
                    It.Is<string>(s => s == "DEVICE_RECOVERY_ERROR"),
                    It.IsAny<string>(),
                    It.IsAny<ErrorSeverity>(),
                    It.IsAny<ErrorSource>(),
                    It.IsAny<IDictionary<string, object>>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task ThrottleRecoveryAttemptsAsync_ShouldAllowFirstAttempt()
        {
            // Act
            var result = await _sut.ThrottleRecoveryAttemptsAsync("TEST_ERROR");

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task ThrottleRecoveryAttemptsAsync_ShouldThrottleRepeatedAttempts()
        {
            // Arrange - first attempt
            await _sut.ThrottleRecoveryAttemptsAsync("TEST_ERROR");

            // Act - immediate second attempt
            var result = await _sut.ThrottleRecoveryAttemptsAsync("TEST_ERROR");

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void Dispose_ShouldCleanupResources()
        {
            // Act
            _sut.Dispose();

            // Assert
            _sut.State.Should().Be(ComponentState.Disposed);
            
            // Verify state change
            _mockEventBus.Verify(
                e => e.PublishAsync(
                    It.Is<object>(o => o == _sut),
                    It.Is<IStateChangeEvent>(evt => 
                        evt.NewState == ComponentState.Disposed),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }
}
