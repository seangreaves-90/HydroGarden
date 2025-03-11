using FluentAssertions;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.ErrorHandling;
using HydroGarden.Foundation.ErrorHandling.Common;
using HydroGarden.Foundation.ErrorHandling.Exceptions;
using HydroGarden.Foundation.ErrorHandling.RecoveryStrategy;
using HydroGarden.Logger.Abstractions;
using Moq;
using Xunit;

namespace HydroGarden.Foundation.Tests.ErrorHandling.RecoveryStrategies
{
    /// <summary>
    /// Unit tests for the CircuitBreakerRecoveryStrategy class.
    /// </summary>
    public class CircuitBreakerRecoveryStrategyTests
    {
        private readonly Guid _deviceId = Guid.NewGuid();
        private readonly Mock<ILogger> _mockLogger;
        private readonly Mock<IServiceProvider> _mockServiceProvider;
        private readonly Mock<CircuitBreakerMiddleware> _mockMiddleware;
        private readonly CircuitBreakerRecoveryStrategy _strategy;

        public CircuitBreakerRecoveryStrategyTests()
        {
            _mockLogger = new Mock<ILogger>();
            _mockServiceProvider = new Mock<IServiceProvider>();
            _mockMiddleware = new Mock<CircuitBreakerMiddleware>();
            
            // Configure service provider to return our middleware mock
            _mockServiceProvider.Setup(sp => sp.GetService(typeof(CircuitBreakerMiddleware)))
                .Returns(_mockMiddleware.Object);
                
            _strategy = new CircuitBreakerRecoveryStrategy(_mockLogger.Object, _mockServiceProvider.Object);
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => 
                new CircuitBreakerRecoveryStrategy(null!, _mockServiceProvider.Object));
            exception.ParamName.Should().Be("logger");
        }

        [Fact]
        public void Constructor_WithNullMiddleware_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => 
                new CircuitBreakerRecoveryStrategy(_mockLogger.Object, null!));
            exception.ParamName.Should().Be("serviceProvider");
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
        public void CanRecover_WithCircuitOpenErrorCode_ShouldReturnTrue()
        {
            // Arrange
            var error = new ComponentError(
                _deviceId,
                ErrorCodes.Recovery.CIRCUIT_OPEN,
                "Circuit is open",
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
        public void CanRecover_WithNonCircuitOpenErrorCode_ShouldReturnFalse()
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
        public async Task ExecuteRecoveryAsync_WithValidCircuitOpenError_ShouldTryResetCircuit()
        {
            // Arrange
            var error = new ComponentError(
                _deviceId,
                ErrorCodes.Recovery.CIRCUIT_OPEN,
                "Circuit is open",
                ErrorSeverity.Error,
                true,
                ErrorSource.Device,
                false);

            // Configure context to contain the service key
            error.Context["ServiceKey"] = "TestService";

            // Configure middleware to return Closed state after reset
            _mockMiddleware.Setup(m => m.GetCircuitState("TestService"))
                .Returns(CircuitBreakerMiddleware.CircuitState.Open);

            // Act
            var result = await _strategy.AttemptRecoveryAsync(error);

            // Assert
            _mockMiddleware.Verify(m => m.ResetCircuit("TestService"));
            result.Should().BeTrue();
            _mockLogger.Verify(l => l.Log(It.Is<string>(s => 
                s.Contains("Successfully reset circuit") && s.Contains("TestService"))));
        }

        [Fact]
        public async Task ExecuteRecoveryAsync_WithoutServiceKeyInContext_ShouldReturnFalse()
        {
            // Arrange
            var error = new ComponentError(
                _deviceId,
                ErrorCodes.Recovery.CIRCUIT_OPEN,
                "Circuit is open",
                ErrorSeverity.Error,
                true,
                ErrorSource.Device,
                false);

            // Context does not contain service key

            // Act
            var result = await _strategy.AttemptRecoveryAsync(error);

            // Assert
            result.Should().BeFalse();
            _mockLogger.Verify(l => l.Log(It.Is<string>(s => 
                s.Contains("Service key not found in error context"))));
        }

        [Fact]
        public async Task ExecuteRecoveryAsync_CircuitStillOpenAfterReset_ShouldReturnFalse()
        {
            // Arrange
            var error = new ComponentError(
                _deviceId,
                ErrorCodes.Recovery.CIRCUIT_OPEN,
                "Circuit is open",
                ErrorSeverity.Error,
                true,
                ErrorSource.Device,
                false);

            // Configure context to contain the service key
            error.Context["ServiceKey"] = "TestService";

            // Configure middleware to still return Open state after reset
            _mockMiddleware.Setup(m => m.GetCircuitState("TestService"))
                .Returns(CircuitBreakerMiddleware.CircuitState.Open);

            // Act
            var result = await _strategy.AttemptRecoveryAsync(error);

            // Assert
            _mockMiddleware.Verify(m => m.ResetCircuit("TestService"));
            result.Should().BeFalse();
            _mockLogger.Verify(l => l.Log(It.Is<string>(s => 
                s.Contains("Failed to reset circuit") && s.Contains("TestService"))));
        }

        [Fact]
        public async Task ExecuteRecoveryAsync_WhenCircuitIsHalfOpen_ShouldReturnTrue()
        {
            // Arrange
            var error = new ComponentError(
                _deviceId,
                ErrorCodes.Recovery.CIRCUIT_OPEN,
                "Circuit is open",
                ErrorSeverity.Error,
                true,
                ErrorSource.Device,
                false);

            // Configure context to contain the service key
            error.Context["ServiceKey"] = "TestService";

            // Configure middleware to return HalfOpen state after reset
            var sequence = new MockSequence();
            _mockMiddleware.InSequence(sequence)
                .Setup(m => m.GetCircuitState("TestService"))
                .Returns(CircuitBreakerMiddleware.CircuitState.Open);
            
            _mockMiddleware.InSequence(sequence)
                .Setup(m => m.GetCircuitState("TestService"))
                .Returns(CircuitBreakerMiddleware.CircuitState.HalfOpen);

            // Act
            var result = await _strategy.AttemptRecoveryAsync(error);

            // Assert
            _mockMiddleware.Verify(m => m.ResetCircuit("TestService"));
            result.Should().BeTrue();
            _mockLogger.Verify(l => l.Log(It.Is<string>(s => 
                s.Contains("Successfully reset circuit") && s.Contains("TestService"))));
        }

        [Fact]
        public async Task ExecuteRecoveryAsync_WhenMiddlewareThrowsException_ShouldHandleAndReturnFalse()
        {
            // Arrange
            var error = new ComponentError(
                _deviceId,
                ErrorCodes.Recovery.CIRCUIT_OPEN,
                "Circuit is open",
                ErrorSeverity.Error,
                true,
                ErrorSource.Device,
                false);

            // Configure context to contain the service key
            error.Context["ServiceKey"] = "TestService";

            // Configure middleware to throw exception
            _mockMiddleware.Setup(m => m.ResetCircuit("TestService"))
                .Throws(new InvalidOperationException("Test exception"));

            // Act
            var result = await _strategy.AttemptRecoveryAsync(error);

            // Assert
            result.Should().BeFalse();
            _mockLogger.Verify(l => l.Log(It.IsAny<Exception>(), It.Is<string>(s => 
                s.Contains("Error while resetting circuit") && s.Contains("TestService"))));
        }

        [Fact]
        public void PriorityProperty_ShouldBeHighPriority()
        {
            // Act & Assert
            _strategy.Priority.Should().Be(20);
        }

        [Fact]
        public void ComplexityLevelProperty_ShouldBeSimple()
        {
            // Act & Assert
            _strategy.ComplexityLevel.Should().Be(ErrorTaxonomy.RecoveryComplexity.Simple);
        }

        [Fact]
        public void SupportedRootCauses_ShouldIncludeCircuitBreakerOpen()
        {
            // Act & Assert
            _strategy.SupportedRootCauses.Should().Contain(ErrorTaxonomy.RootCause.CircuitBreakerOpen);
        }
    }
}
