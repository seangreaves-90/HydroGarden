using FluentAssertions;
using HydroGarden.Foundation.ErrorHandling.Exceptions;
using Xunit;

namespace HydroGarden.Foundation.Tests.ErrorHandling.Exceptions
{
    /// <summary>
    /// Unit tests for circuit breaker exception classes.
    /// </summary>
    public class CircuitBreakerExceptionsTests
    {
        [Fact]
        public void CircuitBreakerOpenException_Constructor_ShouldSetMessageAndLastFailureTime()
        {
            // Arrange
            var message = "Circuit is open for service XYZ";
            
            // Act
            var exception = new CircuitBreakerOpenException(message);
            
            // Assert
            exception.Message.Should().Be(message);
            exception.LastFailureTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        }

        [Fact]
        public void CircuitBreakerOpenException_ShouldDeriveFromException()
        {
            // Arrange & Act
            var exception = new CircuitBreakerOpenException("Test message");
            
            // Assert
            exception.Should().BeAssignableTo<Exception>();
        }

        [Fact]
        public void CircuitBreakerMiddleware_GetCircuitState_ShouldReturnClosedByDefault()
        {
            // Arrange
            var middleware = new CircuitBreakerMiddleware();
            
            // Act
            var state = middleware.GetCircuitState("AnyServiceKey");
            
            // Assert
            state.Should().Be(CircuitBreakerMiddleware.CircuitState.Closed);
        }

        [Fact]
        public void CircuitBreakerMiddleware_ResetCircuit_ShouldNotThrow()
        {
            // Arrange
            var middleware = new CircuitBreakerMiddleware();
            
            // Act & Assert
            var action = () => middleware.ResetCircuit("AnyServiceKey");
            action.Should().NotThrow();
        }

        [Fact]
        public void CircuitState_ShouldHaveExpectedValues()
        {
            // Assert - Enum should have the expected values
            ((int)CircuitBreakerMiddleware.CircuitState.Closed).Should().Be(0);
            ((int)CircuitBreakerMiddleware.CircuitState.Open).Should().Be(1);
            ((int)CircuitBreakerMiddleware.CircuitState.HalfOpen).Should().Be(2);
        }
    }
}
