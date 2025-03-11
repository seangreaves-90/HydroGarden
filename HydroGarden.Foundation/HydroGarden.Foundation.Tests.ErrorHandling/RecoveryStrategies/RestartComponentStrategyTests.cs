using FluentAssertions;
using HydroGarden.Foundation.Abstractions.Interfaces.Components;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.Abstractions.Interfaces.Services;
using HydroGarden.Foundation.ErrorHandling;
using HydroGarden.Foundation.ErrorHandling.Common;
using HydroGarden.Foundation.ErrorHandling.RecoveryStrategy;
using HydroGarden.Logger.Abstractions;
using Moq;
using Xunit;

namespace HydroGarden.Foundation.Tests.ErrorHandling.RecoveryStrategies
{
    /// <summary>
    /// Unit tests for the RestartComponentStrategy class.
    /// </summary>
    public class RestartComponentStrategyTests
    {
        private readonly Guid _deviceId = Guid.NewGuid();
        private readonly Mock<ILogger> _mockLogger;
        private readonly Mock<IPersistenceService> _mockPersistenceService;
        private readonly RestartComponentStrategy _strategy;

        public RestartComponentStrategyTests()
        {
            _mockLogger = new Mock<ILogger>();
            _mockPersistenceService = new Mock<IPersistenceService>();
            _strategy = new RestartComponentStrategy(_mockLogger.Object, _mockPersistenceService.Object);
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => 
                new RestartComponentStrategy(null!, _mockPersistenceService.Object));
            exception.ParamName.Should().Be("logger");
        }

        [Fact]
        public void Constructor_WithNullPersistenceService_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => 
                new RestartComponentStrategy(_mockLogger.Object, null!));
            exception.ParamName.Should().Be("persistenceService");
        }

        [Fact]
        public void CanRecover_WithNullError_ShouldReturnFalse()
        {
            // Arrange & Act
            var canRecover = _strategy.CanRecover(null);

            // Assert
            canRecover.Should().BeFalse();
        }

        [Fact]
        public void CanRecover_WithStateTransitionFailedError_ShouldReturnTrue()
        {
            // Arrange
            var error = new ComponentError(
                _deviceId,
                ErrorCodes.Device.STATE_TRANSITION_FAILED,
                "State transition failed",
                ErrorSeverity.Error,
                true,
                ErrorSource.Device,
                false);

            // Act
            var canRecover = _strategy.CanRecover(error);

            // Assert
            canRecover.Should().BeTrue();
        }

        [Fact]
        public void CanRecover_WithCommunicationLostError_ShouldReturnTrue()
        {
            // Arrange
            var error = new ComponentError(
                _deviceId,
                ErrorCodes.Device.COMMUNICATION_LOST,
                "Communication lost",
                ErrorSeverity.Error,
                true,
                ErrorSource.Device,
                false);

            // Act
            var canRecover = _strategy.CanRecover(error);

            // Assert
            canRecover.Should().BeTrue();
        }

        [Fact]
        public void CanRecover_WithOtherRecoverableDeviceError_ShouldReturnTrue()
        {
            // Arrange
            var error = new ComponentError(
                _deviceId,
                ErrorCodes.Device.SENSOR_MALFUNCTION,
                "Sensor malfunction",
                ErrorSeverity.Error,
                true,
                ErrorSource.Device,
                false);

            // Act
            var canRecover = _strategy.CanRecover(error);

            // Assert
            canRecover.Should().BeTrue();
        }

        [Fact]
        public void CanRecover_WithNonDeviceError_ShouldReturnFalse()
        {
            // Arrange
            var error = new ComponentError(
                _deviceId,
                ErrorCodes.Communication.CONNECTION_FAILED,
                "Connection failed",
                ErrorSeverity.Error,
                true,
                ErrorSource.Communication,
                false);

            // Act
            var canRecover = _strategy.CanRecover(error);

            // Assert
            canRecover.Should().BeFalse();
        }

        [Fact]
        public void CanRecover_WithUnrecoverableDeviceError_ShouldReturnFalse()
        {
            // Arrange
            var error = new ComponentError(
                _deviceId,
                ErrorCodes.Device.HARDWARE_FAILURE,
                "Hardware failure",
                ErrorSeverity.Critical,
                false,
                ErrorSource.Device,
                false);

            // Act
            var canRecover = _strategy.CanRecover(error);

            // Assert
            canRecover.Should().BeFalse();
        }

        [Fact]
        public async Task ExecuteRecoveryAsync_WithDeviceNotFound_ShouldReturnFalse()
        {
            // Arrange
            var error = new ComponentError(
                _deviceId,
                ErrorCodes.Device.COMMUNICATION_LOST,
                "Communication lost",
                ErrorSeverity.Error,
                true,
                ErrorSource.Device,
                false);

            _mockPersistenceService.Setup(p => p.GetPropertyAsync<IIoTDevice>(
                    It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IIoTDevice?)null);

            _mockPersistenceService.Setup(p => p.GetPropertyAsync<IEnumerable<IIoTDevice>>(
                    It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IEnumerable<IIoTDevice>?)null);

            // Act
            var result = await _strategy.AttemptRecoveryAsync(error);

            // Assert
            result.Should().BeFalse();
            _mockLogger.Verify(l => l.Log(It.Is<string>(s => 
                s.Contains("Device") && s.Contains("not found"))));
        }

        [Fact]
        public async Task ExecuteRecoveryAsync_WithGetDeviceException_ShouldReturnFalse()
        {
            // Arrange
            var error = new ComponentError(
                _deviceId,
                ErrorCodes.Device.COMMUNICATION_LOST,
                "Communication lost",
                ErrorSeverity.Error,
                true,
                ErrorSource.Device,
                false);

            _mockPersistenceService.Setup(p => p.GetPropertyAsync<IIoTDevice>(
                    It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Test exception"));

            // Act
            var result = await _strategy.AttemptRecoveryAsync(error);

            // Assert
            result.Should().BeFalse();
            _mockLogger.Verify(l => l.Log(It.IsAny<Exception>(), It.Is<string>(s => 
                s.Contains("Error during device restart recovery"))));
        }

        [Fact]
        public async Task ExecuteRecoveryAsync_WithRestartSuccess_ShouldReturnTrue()
        {
            // Arrange
            var error = new ComponentError(
                _deviceId,
                ErrorCodes.Device.COMMUNICATION_LOST,
                "Communication lost",
                ErrorSeverity.Error,
                true,
                ErrorSource.Device,
                false);

            var mockDevice = new Mock<IIoTDevice>();
            mockDevice.Setup(d => d.Id).Returns(_deviceId);
            mockDevice.Setup(d => d.Name).Returns("TestDevice");
            mockDevice.Setup(d => d.State).Returns(ComponentState.Error);

            mockDevice.Setup(d => d.StopAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            
            mockDevice.Setup(d => d.InitializeAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask)
                .Callback(() => mockDevice.Setup(d => d.State).Returns(ComponentState.Ready));
            
            mockDevice.Setup(d => d.StartAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask)
                .Callback(() => mockDevice.Setup(d => d.State).Returns(ComponentState.Running));

            _mockPersistenceService.Setup(p => p.GetPropertyAsync<IIoTDevice>(
                    It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockDevice.Object);

            // Act
            var result = await _strategy.AttemptRecoveryAsync(error);

            // Assert
            result.Should().BeTrue();
            mockDevice.Verify(d => d.StopAsync(It.IsAny<CancellationToken>()));
            mockDevice.Verify(d => d.InitializeAsync(It.IsAny<CancellationToken>()));
            mockDevice.Verify(d => d.StartAsync(It.IsAny<CancellationToken>()));
            _mockLogger.Verify(l => l.Log(It.Is<string>(s => 
                s.Contains("Restart recovery successful"))));
        }

        [Fact]
        public async Task ExecuteRecoveryAsync_WithRestartSequenceFailure_ShouldReturnFalse()
        {
            // Arrange
            var error = new ComponentError(
                _deviceId,
                ErrorCodes.Device.COMMUNICATION_LOST,
                "Communication lost",
                ErrorSeverity.Error,
                true,
                ErrorSource.Device,
                false);

            var mockDevice = new Mock<IIoTDevice>();
            mockDevice.Setup(d => d.Id).Returns(_deviceId);
            mockDevice.Setup(d => d.Name).Returns("TestDevice");
            mockDevice.Setup(d => d.State).Returns(ComponentState.Error);

            mockDevice.Setup(d => d.StopAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            
            mockDevice.Setup(d => d.InitializeAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask)
                .Callback(() => mockDevice.Setup(d => d.State).Returns(ComponentState.Error)); // Initialize fails
            
            // Start won't be called because Initialize fails

            _mockPersistenceService.Setup(p => p.GetPropertyAsync<IIoTDevice>(
                    It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockDevice.Object);

            // Act
            var result = await _strategy.AttemptRecoveryAsync(error);

            // Assert
            result.Should().BeFalse();
            mockDevice.Verify(d => d.StopAsync(It.IsAny<CancellationToken>()));
            mockDevice.Verify(d => d.InitializeAsync(It.IsAny<CancellationToken>()));
            mockDevice.Verify(d => d.StartAsync(It.IsAny<CancellationToken>()), Times.Never);
            _mockLogger.Verify(l => l.Log(It.Is<string>(s => 
                s.Contains("Restart recovery failed"))));
        }

        [Fact]
        public async Task ExecuteRecoveryAsync_WithExceptionDuringRestart_ShouldReturnFalse()
        {
            // Arrange
            var error = new ComponentError(
                _deviceId,
                ErrorCodes.Device.COMMUNICATION_LOST,
                "Communication lost",
                ErrorSeverity.Error,
                true,
                ErrorSource.Device,
                false);

            var mockDevice = new Mock<IIoTDevice>();
            mockDevice.Setup(d => d.Id).Returns(_deviceId);
            mockDevice.Setup(d => d.Name).Returns("TestDevice");
            mockDevice.Setup(d => d.State).Returns(ComponentState.Error);

            mockDevice.Setup(d => d.StopAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Test exception"));

            _mockPersistenceService.Setup(p => p.GetPropertyAsync<IIoTDevice>(
                    It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockDevice.Object);

            // Act
            var result = await _strategy.AttemptRecoveryAsync(error);

            // Assert
            result.Should().BeFalse();
            _mockLogger.Verify(l => l.Log(It.IsAny<Exception>(), It.Is<string>(s => 
                s.Contains("Error during device restart recovery"))));
        }

        [Fact]
        public async Task ExecuteRecoveryAsync_WithDeviceInDeadState_ShouldReturnFalse()
        {
            // Arrange
            var error = new ComponentError(
                _deviceId,
                ErrorCodes.Device.COMMUNICATION_LOST,
                "Communication lost",
                ErrorSeverity.Error,
                true,
                ErrorSource.Device,
                false);

            var mockDevice = new Mock<IIoTDevice>();
            mockDevice.Setup(d => d.Id).Returns(_deviceId);
            mockDevice.Setup(d => d.Name).Returns("TestDevice");
            mockDevice.Setup(d => d.State).Returns(ComponentState.Disposed); // Device is in a dead state

            _mockPersistenceService.Setup(p => p.GetPropertyAsync<IIoTDevice>(
                    It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockDevice.Object);

            // Act
            var result = await _strategy.AttemptRecoveryAsync(error);

            // Assert
            result.Should().BeFalse();
            // Verify the device state was checked
            _mockLogger.Verify(l => l.Log(It.Is<string>(s => 
                s.Contains("Current device state: Disposed"))));
        }

        [Fact]
        public async Task ExecuteRecoveryAsync_WithStateRunning_ShouldStopBeforeRestarting()
        {
            // Arrange
            var error = new ComponentError(
                _deviceId,
                ErrorCodes.Device.COMMUNICATION_LOST,
                "Communication lost",
                ErrorSeverity.Error,
                true,
                ErrorSource.Device,
                false);

            var mockDevice = new Mock<IIoTDevice>();
            mockDevice.Setup(d => d.Id).Returns(_deviceId);
            mockDevice.Setup(d => d.Name).Returns("TestDevice");
            mockDevice.Setup(d => d.State).Returns(ComponentState.Running); // Device is running

            mockDevice.Setup(d => d.StopAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask)
                .Callback(() => mockDevice.Setup(d => d.State).Returns(ComponentState.Created));
            
            mockDevice.Setup(d => d.InitializeAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask)
                .Callback(() => mockDevice.Setup(d => d.State).Returns(ComponentState.Ready));
            
            mockDevice.Setup(d => d.StartAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask)
                .Callback(() => mockDevice.Setup(d => d.State).Returns(ComponentState.Running));

            _mockPersistenceService.Setup(p => p.GetPropertyAsync<IIoTDevice>(
                    It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockDevice.Object);

            // Act
            var result = await _strategy.AttemptRecoveryAsync(error);

            // Assert
            result.Should().BeTrue();
            mockDevice.Verify(d => d.StopAsync(It.IsAny<CancellationToken>()));
            mockDevice.Verify(d => d.InitializeAsync(It.IsAny<CancellationToken>()));
            mockDevice.Verify(d => d.StartAsync(It.IsAny<CancellationToken>()));
            _mockLogger.Verify(l => l.Log(It.Is<string>(s => 
                s.Contains("Stopping device"))));
        }

        [Fact]
        public async Task ExecuteRecoveryAsync_WithStateReady_ShouldOnlyStart()
        {
            // Arrange
            var error = new ComponentError(
                _deviceId,
                ErrorCodes.Device.COMMUNICATION_LOST,
                "Communication lost",
                ErrorSeverity.Error,
                true,
                ErrorSource.Device,
                false);

            var mockDevice = new Mock<IIoTDevice>();
            mockDevice.Setup(d => d.Id).Returns(_deviceId);
            mockDevice.Setup(d => d.Name).Returns("TestDevice");
            mockDevice.Setup(d => d.State).Returns(ComponentState.Ready); // Device is already initialized

            mockDevice.Setup(d => d.StartAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask)
                .Callback(() => mockDevice.Setup(d => d.State).Returns(ComponentState.Running));

            _mockPersistenceService.Setup(p => p.GetPropertyAsync<IIoTDevice>(
                    It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockDevice.Object);

            // Act
            var result = await _strategy.AttemptRecoveryAsync(error);

            // Assert
            result.Should().BeTrue();
            mockDevice.Verify(d => d.StopAsync(It.IsAny<CancellationToken>()), Times.Never);
            mockDevice.Verify(d => d.InitializeAsync(It.IsAny<CancellationToken>()), Times.Never);
            mockDevice.Verify(d => d.StartAsync(It.IsAny<CancellationToken>()));
        }

        [Fact]
        public void SupportedRootCauses_ShouldIncludeRelevantCauses()
        {
            // Act
            var supportedCauses = _strategy.SupportedRootCauses;

            // Assert
            supportedCauses.Should().Contain(ErrorTaxonomy.RootCause.InvalidState);
            supportedCauses.Should().Contain(ErrorTaxonomy.RootCause.ConnectionTimeout);
            supportedCauses.Should().Contain(ErrorTaxonomy.RootCause.NetworkFailure);
            supportedCauses.Should().Contain(ErrorTaxonomy.RootCause.MemoryExhaustion);
            supportedCauses.Should().Contain(ErrorTaxonomy.RootCause.ResourceExhaustion);
        }
    }
}
