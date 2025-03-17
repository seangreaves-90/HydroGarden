using FluentAssertions;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.ErrorHandling;
using HydroGarden.Logger.Abstractions;
using Moq;

namespace HydroGarden.Foundation.Tests.Integration.ErrorHandling
{
    /// <summary>
    /// Integration tests that verify error classification functionality.
    /// </summary>
    public class ErrorClassificationTests
    {
        private readonly Mock<ILogger> _mockLogger;
        
        public ErrorClassificationTests()
        {
            _mockLogger = new Mock<ILogger>();
        }

        [Fact]
        public void ErrorFactory_ShouldClassifyErrorsCorrectly()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var exception = new InvalidOperationException("Test exception");
            
            // Act - Create errors of different categories
            var deviceError = ErrorFactory.CreateDeviceError(
                deviceId,
                "DEVICE_ERROR",
                "Device error message",
                ErrorSeverity.Critical,
                exception);
            
            var serviceError = new ComponentError(
                deviceId,
                "SERVICE_ERROR",
                "Service error message",
                ErrorSeverity.Warning,
                ErrorSource.Service,
                null,
                exception);
                
            var systemError = new ComponentError(
                deviceId,
                "SYSTEM_ERROR",
                "System error message",
                ErrorSeverity.Error,
                ErrorSource.System,
                null,
                exception,
                ErrorCategory.System);
                
            var otherError = new ComponentError(
                deviceId,
                "OTHER_ERROR",
                "Other error message",
                ErrorSeverity.Warning, // Using Warning instead of Information
                ErrorSource.Device,
                null,
                exception,
                ErrorCategory.Device);
            
            // Assert - Each error should have the correct classification
            deviceError.Category.Should().Be(ErrorCategory.Device);
            deviceError.Source.Should().Be(ErrorSource.Device);
            deviceError.Severity.Should().Be(ErrorSeverity.Critical);
            
            serviceError.Category.Should().Be(ErrorCategory.Service);
            serviceError.Source.Should().Be(ErrorSource.Service);
            serviceError.Severity.Should().Be(ErrorSeverity.Warning);
            
            systemError.Category.Should().Be(ErrorCategory.System);
            systemError.Source.Should().Be(ErrorSource.System);
            systemError.Severity.Should().Be(ErrorSeverity.Error);
            
            otherError.Category.Should().Be(ErrorCategory.Device);
            otherError.Source.Should().Be(ErrorSource.Device);
            otherError.Severity.Should().Be(ErrorSeverity.Warning);
        }
        
        [Fact]
        public void ErrorContext_ShouldInheritFromParentError()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            
            // Create a parent error with context
            var parentContext = new Dictionary<string, object>
            {
                ["SystemName"] = "HydroGarden",
                ["Version"] = "1.0.0",
                ["Environment"] = "Test"
            };
            
            var parentError = new ComponentError(
                deviceId,
                "PARENT_ERROR",
                "Parent error message",
                ErrorSeverity.Error,
                ErrorSource.Device,
                parentContext);
            
            // Act - Create a child error that inherits context from parent by copying parent context
            var childContext = new Dictionary<string, object>(parentContext)
            {
                ["ChildKey"] = "ChildValue",
                ["Version"] = "1.0.1" // This should override the parent version
            };
            
            var childError = new ComponentError(
                deviceId,
                "CHILD_ERROR",
                "Child error message",
                ErrorSeverity.Warning,
                ErrorSource.Device,
                childContext,
                null,
                ErrorCategory.Device,
                parentError.CorrelationId); // Link to parent via correlation ID
            
            // Create another error with new context
            var siblingError = new ComponentError(
                deviceId,
                "SIBLING_ERROR",
                "Sibling error message",
                ErrorSeverity.Warning, // Using Warning instead of Information
                ErrorSource.Device,
                new Dictionary<string, object>(parentContext)); // Copy the parent context
            
            // Assert
            childError.CorrelationId.Should().Be(parentError.CorrelationId, "Child should have same correlation ID as parent");
            childError.Context.Should().ContainKey("SystemName");
            childError.Context.Should().ContainKey("Environment");
            childError.Context.Should().ContainKey("ChildKey");
            
            // Child-specific value should override parent value
            childError.Context["Version"].Should().Be("1.0.1");
            
            // Sibling should have different correlation ID
            siblingError.CorrelationId.Should().NotBe(parentError.CorrelationId);
        }
    }
}