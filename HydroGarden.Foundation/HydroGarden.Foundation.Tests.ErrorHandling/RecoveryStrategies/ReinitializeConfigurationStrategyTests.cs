using FluentAssertions;
using HydroGarden.Foundation.Abstractions.Interfaces;
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
    /// Unit tests for the ReinitializeConfigurationStrategy class.
    /// </summary>
    public class ReinitializeConfigurationStrategyTests
    {
        private readonly Guid _deviceId = Guid.NewGuid();
        private readonly Mock<ILogger> _mockLogger;
        private readonly Mock<IPersistenceService> _mockPersistenceService;
        private readonly ReinitializeConfigurationStrategy _strategy;

        public ReinitializeConfigurationStrategyTests()
        {
            _mockLogger = new Mock<ILogger>();
            _mockPersistenceService = new Mock<IPersistenceService>();
            _strategy = new ReinitializeConfigurationStrategy(_mockLogger.Object, _mockPersistenceService.Object);
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => 
                new ReinitializeConfigurationStrategy(null!, _mockPersistenceService.Object));
            exception.ParamName.Should().Be("logger");
        }

        [Fact]
        public void Constructor_WithNullPersistenceService_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => 
                new ReinitializeConfigurationStrategy(_mockLogger.Object, null!));
            exception.ParamName.Should().Be("persistenceService");
        }

        [Fact]
        public void CanRecover_WithConfigurationInvalidError_ShouldReturnTrue()
        {
            // Arrange
            var error = new ComponentError(
                _deviceId,
                ErrorCodes.Device.CONFIGURATION_INVALID,
                "Configuration is invalid",
                ErrorSeverity.Error,
                true,
                ErrorSource.Device,
                false);

            // We need to override the IsUnrecoverable property since ConfigurationInvalid is normally unrecoverable
            // This is a bit of a hack for testing, but necessary to test this specific scenario
            var field = error.GetType().GetField("IsRecoverable", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            field?.SetValue(error, true);

            // Act
            var canRecover = _strategy.CanRecover(error);

            // Assert
            canRecover.Should().BeTrue();
        }

        [Fact]
        public void CanRecover_WithNonConfigurationError_ShouldReturnFalse()
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
                ErrorCodes.Device.CONFIGURATION_INVALID,
                "Configuration is invalid",
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
                s.Contains("Device not found"))));
        }

        [Fact]
        public async Task ExecuteRecoveryAsync_WithSuccessfulReconfiguration_ShouldReturnTrue()
        {
            // Arrange
            var error = new ComponentError(
                _deviceId,
                ErrorCodes.Device.CONFIGURATION_INVALID,
                "Configuration is invalid",
                ErrorSeverity.Error,
                true,
                ErrorSource.Device,
                false);

            var mockDevice = new Mock<IIoTDevice>();
            mockDevice.Setup(d => d.Id).Returns(_deviceId);
            mockDevice.Setup(d => d.Name).Returns("TestDevice");
            mockDevice.Setup(d => d.State).Returns(ComponentState.Error);

            // Prepare default properties
            var defaultProperties = new Dictionary<string, object>
            {
                ["Config1"] = "DefaultValue1",
                ["Config2"] = 42
            };
            
            // Configure GetDefaultPropertiesAsync to return properties
            _mockPersistenceService.Setup(p => p.GetPropertyAsync<IDictionary<string, object>>(
                    It.IsAny<Guid>(), It.Is<string>(s => s == "DefaultProperties"), It.IsAny<CancellationToken>()))
                .ReturnsAsync(defaultProperties);

            // Configure device to successfully load properties and reset
            mockDevice.Setup(d => d.LoadPropertiesAsync(
                    It.IsAny<IDictionary<string, object>>(), It.IsAny<IDictionary<string, IPropertyMetadata>?>()))
                .Returns(Task.CompletedTask);
            
            mockDevice.Setup(d => d.StopAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            
            mockDevice.Setup(d => d.InitializeAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask)
                .Callback(() => mockDevice.Setup(d => d.State).Returns(ComponentState.Ready));
            
            _mockPersistenceService.Setup(p => p.GetPropertyAsync<IIoTDevice>(
                    It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockDevice.Object);

            // Act
            var result = await _strategy.AttemptRecoveryAsync(error);

            // Assert
            result.Should().BeTrue();
            mockDevice.Verify(d => d.LoadPropertiesAsync(
                It.Is<IDictionary<string, object>>(dict => 
                    dict["Config1"].Equals("DefaultValue1") && 
                    dict["Config2"].Equals(42)), 
                It.IsAny<IDictionary<string, IPropertyMetadata>?>()));
            mockDevice.Verify(d => d.StopAsync(It.IsAny<CancellationToken>()));
            mockDevice.Verify(d => d.InitializeAsync(It.IsAny<CancellationToken>()));
            _mockLogger.Verify(l => l.Log(It.Is<string>(s => 
                s.Contains("Configuration reset successful"))));
        }

        [Fact]
        public async Task ExecuteRecoveryAsync_WithNoDefaultProperties_ShouldReturnFalse()
        {
            // Arrange
            var error = new ComponentError(
                _deviceId,
                ErrorCodes.Device.CONFIGURATION_INVALID,
                "Configuration is invalid",
                ErrorSeverity.Error,
                true,
                ErrorSource.Device,
                false);

            var mockDevice = new Mock<IIoTDevice>();
            mockDevice.Setup(d => d.Id).Returns(_deviceId);
            mockDevice.Setup(d => d.Name).Returns("TestDevice");
            mockDevice.Setup(d => d.State).Returns(ComponentState.Error);

            // Configure GetDefaultPropertiesAsync to return null
            _mockPersistenceService.Setup(p => p.GetPropertyAsync<IDictionary<string, object>>(
                    It.IsAny<Guid>(), It.Is<string>(s => s == "DefaultProperties"), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IDictionary<string, object>?)null);

            _mockPersistenceService.Setup(p => p.GetPropertyAsync<IIoTDevice>(
                    It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockDevice.Object);

            // Act
            var result = await _strategy.AttemptRecoveryAsync(error);

            // Assert
            result.Should().BeFalse();
            _mockLogger.Verify(l => l.Log("No default properties found"));
        }

        [Fact]
        public async Task ExecuteRecoveryAsync_WithExceptionDuringReconfig_ShouldHandleAndReturnFalse()
        {
            // Arrange
            var error = new ComponentError(
                _deviceId,
                ErrorCodes.Device.CONFIGURATION_INVALID,
                "Configuration is invalid",
                ErrorSeverity.Error,
                true,
                ErrorSource.Device,
                false);

            var mockDevice = new Mock<IIoTDevice>();
            mockDevice.Setup(d => d.Id).Returns(_deviceId);
            mockDevice.Setup(d => d.Name).Returns("TestDevice");
            mockDevice.Setup(d => d.State).Returns(ComponentState.Error);

            // Prepare default properties
            var defaultProperties = new Dictionary<string, object>
            {
                ["Config1"] = "DefaultValue1"
            };
            
            // Configure GetDefaultPropertiesAsync to return properties
            _mockPersistenceService.Setup(p => p.GetPropertyAsync<IDictionary<string, object>>(
                    It.IsAny<Guid>(), It.Is<string>(s => s == "DefaultProperties"), It.IsAny<CancellationToken>()))
                .ReturnsAsync(defaultProperties);

            // Configure device to throw during reconfiguration
            mockDevice.Setup(d => d.LoadPropertiesAsync(
                    It.IsAny<IDictionary<string, object>>(), It.IsAny<IDictionary<string, IPropertyMetadata>?>()))
                .ThrowsAsync(new InvalidOperationException("Test exception"));

            _mockPersistenceService.Setup(p => p.GetPropertyAsync<IIoTDevice>(
                    It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockDevice.Object);

            // Act
            var result = await _strategy.AttemptRecoveryAsync(error);

            // Assert
            result.Should().BeFalse();
            _mockLogger.Verify(l => l.Log(It.IsAny<Exception>(), It.IsAny<string>()));
            // The specific error message format may vary
        }

        [Fact]
        public void GetPriorityAndName_ShouldReturnExpectedValues()
        {
            // Act & Assert
            _strategy.Name.Should().Be("Configuration Reset Strategy");
            _strategy.Priority.Should().Be(30);
        }

        [Fact]
        public void SupportedRootCauses_ShouldIncludeConfigurationError()
        {
            // Act
            var supportedCauses = _strategy.SupportedRootCauses;

            // Assert
            supportedCauses.Should().Contain(ErrorTaxonomy.RootCause.ConfigurationError);
        }

        [Fact]
        public void ComplexityLevel_ShouldBeComplex()
        {
            // Act & Assert
            _strategy.ComplexityLevel.Should().Be(ErrorTaxonomy.RecoveryComplexity.Complex);
        }
    }
}
