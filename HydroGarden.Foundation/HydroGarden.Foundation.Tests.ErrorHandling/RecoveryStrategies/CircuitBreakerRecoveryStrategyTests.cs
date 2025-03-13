using FluentAssertions;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling.Taxonomy;
using HydroGarden.Foundation.ErrorHandling;
using HydroGarden.Foundation.ErrorHandling.Common;
using HydroGarden.Foundation.ErrorHandling.Exceptions;
using HydroGarden.Foundation.ErrorHandling.Interfaces;
using HydroGarden.Foundation.ErrorHandling.RecoveryStrategy;
using HydroGarden.Foundation.Tests.ErrorHandling.Mocks;
using HydroGarden.Logger.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using IResiliencePolicy = HydroGarden.Foundation.Tests.ErrorHandling.Mocks.IResiliencePolicy;

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
        private readonly TestableCircuitBreakerMiddleware _testableMiddleware;
        private readonly Mock<IResiliencePolicy> _mockResiliencePolicy;
        private readonly CircuitBreakerRecoveryStrategy _strategy;

        public CircuitBreakerRecoveryStrategyTests()
        {
            _mockLogger = new Mock<ILogger>();
            _mockServiceProvider = new Mock<IServiceProvider>();
            _testableMiddleware = new TestableCircuitBreakerMiddleware();
            _mockResiliencePolicy = new Mock<IResiliencePolicy>();
            
            // Configure service provider to return our middleware and policy
            _mockServiceProvider.Setup(sp => sp.GetService(It.Is<Type>(t => t == typeof(TestableCircuitBreakerMiddleware) || 
                                                                  t == typeof(CircuitBreakerMiddleware))))
                .Returns(_testableMiddleware);
                
            _mockServiceProvider.Setup(sp => sp.GetService(typeof(IResiliencePolicy)))
                .Returns(_mockResiliencePolicy.Object);
                
            // Create a method to handle GetServices (needed to avoid extension method mocking issues)
            _mockServiceProvider.Setup(sp => sp.GetService(typeof(IEnumerable<object>)))
                .Returns(new List<object>());
                
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

            // Configure context to contain the service key and circuit breaker type
            error.Context["ServiceKey"] = "TestService";
            error.Context["CircuitBreakerType"] = "TestCircuitBreaker";

            // Create a mock middleware that transitions to HalfOpen when reset
            var mockMiddleware = new Mock<ICircuitBreakerMiddleware>();
            var isReset = false;
            
            mockMiddleware.Setup(m => m.GetCircuitState("TestService"))
                .Returns(() => isReset ? 
                    TestableCircuitBreakerMiddleware.CircuitState.HalfOpen : 
                    TestableCircuitBreakerMiddleware.CircuitState.Open);
                    
            mockMiddleware.Setup(m => m.ResetCircuit("TestService"))
                .Callback(() => isReset = true);
            
            // Replace the middleware in the service provider
            _mockServiceProvider.Setup(sp => sp.GetService(It.Is<Type>(t => t == typeof(TestableCircuitBreakerMiddleware) || 
                                                                   t == typeof(CircuitBreakerMiddleware) ||
                                                                   t == typeof(ICircuitBreakerMiddleware))))
                .Returns(mockMiddleware.Object);

            // Act
            var result = await _strategy.AttemptRecoveryAsync(error);

            // Assert
            mockMiddleware.Verify(m => m.ResetCircuit("TestService"), Times.AtLeastOnce());
            result.Should().BeTrue();
            _mockLogger.Verify(l => l.Log(It.Is<string>(s => 
                s.Contains("reset circuit") && s.Contains("TestService"))));
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

            // Configure context to contain the service key and circuit breaker type
            error.Context["ServiceKey"] = "TestService";
            error.Context["CircuitBreakerType"] = "TestCircuitBreaker";

            // For this test, create a mocked middleware that doesn't change state on reset
            // Create a stubborn middleware that stays in Open state
            var stubbornMiddleware = new TestableCircuitBreakerMiddleware();
            stubbornMiddleware.SetCircuitState("TestService", TestableCircuitBreakerMiddleware.CircuitState.Open);
            
            // Create a mock to verify method calls
            var mockedMiddleware = new Mock<ICircuitBreakerMiddleware>();
            mockedMiddleware.Setup(m => m.ResetCircuit("TestService"));
            mockedMiddleware.Setup(m => m.GetCircuitState("TestService"))
                .Returns(TestableCircuitBreakerMiddleware.CircuitState.Open);
            
            // Replace the middleware in the service provider
            _mockServiceProvider.Setup(sp => sp.GetService(It.Is<Type>(t => t == typeof(TestableCircuitBreakerMiddleware) || 
                                                                   t == typeof(CircuitBreakerMiddleware) || 
                                                                   t == typeof(ICircuitBreakerMiddleware))))
                .Returns(mockedMiddleware.Object);

            // Act
            var result = await _strategy.AttemptRecoveryAsync(error);

            // Assert
            // The middleware should have been asked to reset the circuit
            mockedMiddleware.Verify(m => m.ResetCircuit("TestService"), Times.AtLeastOnce());
            result.Should().BeFalse();
            _mockLogger.Verify(l => l.Log(It.Is<string>(s => 
                s.Contains("reset circuit") && s.Contains("TestService"))));
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

            // Configure context to contain the service key and circuit breaker type
            error.Context["ServiceKey"] = "TestService";
            error.Context["CircuitBreakerType"] = "TestCircuitBreaker";

            // Configure a middleware that transitions to HalfOpen on reset
            var transitioningMiddleware = new Mock<TestableCircuitBreakerMiddleware>() { CallBase = true };
            var isReset = false;
            
            transitioningMiddleware.Setup(m => m.GetCircuitState("TestService"))
                .Returns(() => isReset ? 
                    TestableCircuitBreakerMiddleware.CircuitState.HalfOpen : 
                    TestableCircuitBreakerMiddleware.CircuitState.Open);
                    
            transitioningMiddleware.Setup(m => m.ResetCircuit("TestService"))
                .Callback(() => isReset = true);
                
            // Replace the middleware in the service provider
            _mockServiceProvider.Setup(sp => sp.GetService(It.Is<Type>(t => t == typeof(TestableCircuitBreakerMiddleware) || 
                                                                   t == typeof(CircuitBreakerMiddleware) ||
                                                                   t == typeof(ICircuitBreakerMiddleware))))
                .Returns(transitioningMiddleware.Object);

            // Act
            var result = await _strategy.AttemptRecoveryAsync(error);

            // Assert
            transitioningMiddleware.Verify(m => m.ResetCircuit("TestService"), Times.AtLeastOnce());
            result.Should().BeTrue();
            _mockLogger.Verify(l => l.Log(It.Is<string>(s => 
                s.Contains("reset") && s.Contains("TestService"))));
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

            // Configure context to contain the service key and circuit breaker type
            error.Context["ServiceKey"] = "TestService";
            error.Context["CircuitBreakerType"] = "TestCircuitBreaker";

            // Configure middleware to throw exception
            var throwingMiddleware = new Mock<ICircuitBreakerMiddleware>();
            throwingMiddleware.Setup(m => m.GetCircuitState("TestService"))
                .Returns(TestableCircuitBreakerMiddleware.CircuitState.Open);
                
            throwingMiddleware.Setup(m => m.ResetCircuit("TestService"))
                .Throws(new InvalidOperationException("Test exception"));
                
            // Replace the middleware in the service provider
            _mockServiceProvider.Setup(sp => sp.GetService(It.Is<Type>(t => t == typeof(TestableCircuitBreakerMiddleware) || 
                                                                   t == typeof(CircuitBreakerMiddleware) ||
                                                                   t == typeof(ICircuitBreakerMiddleware))))
                .Returns(throwingMiddleware.Object);

            // Act
            var result = await _strategy.AttemptRecoveryAsync(error);

            // Assert
            result.Should().BeFalse();
            // The error might be logged with a different message format
            _mockLogger.Verify(l => l.Log(It.IsAny<Exception>(), It.IsAny<string>()));
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
