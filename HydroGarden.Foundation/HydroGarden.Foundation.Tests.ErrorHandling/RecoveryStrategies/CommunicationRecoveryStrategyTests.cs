using FluentAssertions;
using HydroGarden.Foundation.Abstractions.Interfaces.Components;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling.Taxonomy;
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
    /// Unit tests for the CommunicationRecoveryStrategy class.
    /// </summary>
    public class CommunicationRecoveryStrategyTests
    {
        private readonly Guid _deviceId = Guid.NewGuid();
        private readonly Mock<ILogger> _mockLogger;
        private readonly Mock<IPersistenceService> _mockPersistenceService;
        private readonly CommunicationRecoveryStrategy _strategy;

        public CommunicationRecoveryStrategyTests()
        {
            _mockLogger = new Mock<ILogger>();
            _mockPersistenceService = new Mock<IPersistenceService>();
            _strategy = new CommunicationRecoveryStrategy(_mockLogger.Object, _mockPersistenceService.Object);
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => 
                new CommunicationRecoveryStrategy(null!, _mockPersistenceService.Object));
            exception.ParamName.Should().Be("logger");
        }

        [Fact]
        public void Constructor_WithNullPersistenceService_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => 
                new CommunicationRecoveryStrategy(_mockLogger.Object, null!));
            exception.ParamName.Should().Be("persistenceService");
        }

        [Fact]
        public void CanRecover_WithCommunicationRelatedError_ShouldReturnTrue()
        {
            // Arrange - Test each communication-related error code
            var connectionFailedError = new ComponentError(
                _deviceId,
                ErrorCodes.Communication.CONNECTION_FAILED,
                "Connection failed",
                ErrorSeverity.Error,
                true,
                ErrorSource.Communication,
                true);
                
            var messageDeliveryFailedError = new ComponentError(
                _deviceId,
                ErrorCodes.Communication.MESSAGE_DELIVERY_FAILED,
                "Message delivery failed",
                ErrorSeverity.Error,
                true,
                ErrorSource.Communication,
                true);
                
            var timeoutError = new ComponentError(
                _deviceId,
                ErrorCodes.Communication.TIMEOUT,
                "Communication timeout",
                ErrorSeverity.Error,
                true,
                ErrorSource.Communication,
                true);

            // Act & Assert
            _strategy.CanRecover(connectionFailedError).Should().BeTrue();
            _strategy.CanRecover(messageDeliveryFailedError).Should().BeTrue();
            _strategy.CanRecover(timeoutError).Should().BeTrue();
        }

        [Fact]
        public void CanRecover_WithDeviceCommunicationLostError_ShouldReturnTrue()
        {
            // Arrange
            var error = new ComponentError(
                _deviceId,
                ErrorCodes.Device.COMMUNICATION_LOST,
                "Device communication lost",
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
        public void CanRecover_WithNonCommunicationError_ShouldReturnFalse()
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
            canRecover.Should().BeFalse();
        }

        [Fact]
        public async Task ExecuteRecoveryAsync_WithDeviceNotFound_ShouldReturnFalse()
        {
            // Arrange
            var error = new ComponentError(
                _deviceId,
                ErrorCodes.Communication.CONNECTION_FAILED,
                "Connection failed",
                ErrorSeverity.Error,
                true,
                ErrorSource.Communication,
                true);

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
        public async Task ExecuteRecoveryAsync_WithSuccessfulCommunicationReset_ShouldReturnTrue()
        {
            // Arrange
            var error = new ComponentError(
                _deviceId,
                ErrorCodes.Communication.CONNECTION_FAILED,
                "Connection failed",
                ErrorSeverity.Error,
                true,
                ErrorSource.Communication,
                true);

            // Create mock device that successfully recovers
            var mockDevice = new Mock<IIoTDevice>();
            mockDevice.Setup(d => d.Id).Returns(_deviceId);
            mockDevice.Setup(d => d.Name).Returns("TestDevice");
            mockDevice.Setup(d => d.State).Returns(ComponentState.Error);
            mockDevice.Setup(d => d.TryRecoverAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            _mockPersistenceService.Setup(p => p.GetPropertyAsync<IIoTDevice>(
                    It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockDevice.Object);

            // Act
            var result = await _strategy.AttemptRecoveryAsync(error);

            // Assert
            result.Should().BeTrue();
            mockDevice.Verify(d => d.TryRecoverAsync(It.IsAny<CancellationToken>()));
            _mockLogger.Verify(l => l.Log(It.Is<string>(s => 
                s.Contains("Communication recovery successful"))));
        }

        [Fact]
        public async Task ExecuteRecoveryAsync_WithFailedCommunicationReset_ShouldReturnFalse()
        {
            // Arrange
            var error = new ComponentError(
                _deviceId,
                ErrorCodes.Communication.CONNECTION_FAILED,
                "Connection failed",
                ErrorSeverity.Error,
                true,
                ErrorSource.Communication,
                true);

            // Create mock device that fails to recover
            var mockDevice = new Mock<IIoTDevice>();
            mockDevice.Setup(d => d.Id).Returns(_deviceId);
            mockDevice.Setup(d => d.Name).Returns("TestDevice");
            mockDevice.Setup(d => d.State).Returns(ComponentState.Error);
            mockDevice.Setup(d => d.TryRecoverAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            _mockPersistenceService.Setup(p => p.GetPropertyAsync<IIoTDevice>(
                    It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockDevice.Object);

            // Act
            var result = await _strategy.AttemptRecoveryAsync(error);

            // Assert
            result.Should().BeFalse();
            mockDevice.Verify(d => d.TryRecoverAsync(It.IsAny<CancellationToken>()));
            _mockLogger.Verify(l => l.Log(It.Is<string>(s => 
                s.Contains("Failed to recover communication"))));
        }

        [Fact]
        public async Task ExecuteRecoveryAsync_WithExceptionDuringRecovery_ShouldHandleAndReturnFalse()
        {
            // Arrange
            var error = new ComponentError(
                _deviceId,
                ErrorCodes.Communication.CONNECTION_FAILED,
                "Connection failed",
                ErrorSeverity.Error,
                true,
                ErrorSource.Communication,
                true);

            // Create mock device that throws during recovery
            var mockDevice = new Mock<IIoTDevice>();
            mockDevice.Setup(d => d.Id).Returns(_deviceId);
            mockDevice.Setup(d => d.Name).Returns("TestDevice");
            mockDevice.Setup(d => d.State).Returns(ComponentState.Error);
            mockDevice.Setup(d => d.TryRecoverAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Test exception"));

            _mockPersistenceService.Setup(p => p.GetPropertyAsync<IIoTDevice>(
                    It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockDevice.Object);

            // Act
            var result = await _strategy.AttemptRecoveryAsync(error);

            // Assert
            result.Should().BeFalse();
            _mockLogger.Verify(l => l.Log(It.IsAny<Exception>(), It.Is<string>(s => 
                s.Contains("Error during communication recovery"))));
        }

        [Fact]
        public void Strategy_ShouldHaveAppropriateProperties()
        {
            // Act & Assert
            _strategy.Name.Should().Be("Communication Recovery Strategy");
            _strategy.Priority.Should().Be(10);  // High priority
            _strategy.ComplexityLevel.Should().Be(ErrorTaxonomy.RecoveryComplexity.Simple);
            
            // Should support the right root causes
            _strategy.SupportedRootCauses.Should().Contain(ErrorTaxonomy.RootCause.NetworkFailure);
            _strategy.SupportedRootCauses.Should().Contain(ErrorTaxonomy.RootCause.ConnectionTimeout);
        }
    }
}
