using FluentAssertions;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.ErrorHandling;
using HydroGarden.Foundation.ErrorHandling.Common;
using Xunit;

namespace HydroGarden.Foundation.Tests.Unit.ErrorHandling
{
    public class ComponentErrorTests
    {
        [Fact]
        public void Constructor_ShouldInitializePropertiesCorrectly()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var errorCode = "DEVICE_TEST_ERROR";
            var message = "Test error message";
            var severity = ErrorSeverity.Critical;
            var source = ErrorSource.Device;
            var category = ErrorCategory.Device;
            var exception = new InvalidOperationException("Test exception");
            var context = new Dictionary<string, object>
            {
                { "TestKey", "TestValue" }
            };

            // Act
            var error = new ComponentError(
                deviceId,
                errorCode,
                message,
                severity,
                source,
                context,
                exception,
                category);

            // Assert
            error.DeviceId.Should().Be(deviceId);
            error.ErrorCode.Should().Be(errorCode);
            error.Message.Should().Be(message);
            error.Severity.Should().Be(severity);
            error.Source.Should().Be(source);
            error.Category.Should().Be(category);
            error.Exception.Should().BeSameAs(exception);
            error.Context.Should().ContainKey("TestKey");
            error.Context["TestKey"].Should().Be("TestValue");
            error.CorrelationId.Should().NotBeEmpty();
            error.Timestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, precision: TimeSpan.FromSeconds(1));
        }

        [Fact]
        public void Constructor_WithNullContext_ShouldCreateEmptyContext()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var errorCode = "DEVICE_TEST_ERROR";
            var message = "Test error message";

            // Act
            var error = new ComponentError(
                deviceId,
                errorCode,
                message,
                ErrorSeverity.Error,
                ErrorSource.Device,
                null,
                null);

            // Assert
            error.Context.Should().NotBeNull();
            error.Context.Should().BeEmpty();
        }

        [Fact]
        public void CreateDeviceError_ShouldCreateErrorWithDeviceProperties()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var errorCode = ErrorCodes.Device.SENSOR_MALFUNCTION;
            var message = "Temperature sensor malfunction";
            var exception = new InvalidOperationException("Sensor reading out of range");

            // Act
            var error = ComponentError.CreateDeviceError(
                deviceId,
                errorCode,
                message,
                ErrorSeverity.Error,
                null,
                exception);

            // Assert
            error.DeviceId.Should().Be(deviceId);
            error.ErrorCode.Should().Be(errorCode);
            error.Message.Should().Be(message);
            error.Severity.Should().Be(ErrorSeverity.Error);
            error.Source.Should().Be(ErrorSource.Device);
            error.Category.Should().Be(ErrorCategory.Device);
            error.Exception.Should().BeSameAs(exception);
        }

        [Fact]
        public void CreateServiceError_ShouldCreateErrorWithServiceProperties()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var errorCode = ErrorCodes.Service.INITIALIZATION_FAILED;
            var message = "Failed to initialize sensor service";

            // Act
            var error = ComponentError.CreateServiceError(
                deviceId,
                errorCode,
                message);

            // Assert
            error.DeviceId.Should().Be(deviceId);
            error.ErrorCode.Should().Be(errorCode);
            error.Message.Should().Be(message);
            error.Severity.Should().Be(ErrorSeverity.Error);
            error.Source.Should().Be(ErrorSource.Service);
            error.Category.Should().Be(ErrorCategory.Service);
        }

        [Fact]
        public void CreateCommunicationError_ShouldCreateErrorWithCommunicationProperties()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var errorCode = ErrorCodes.Communication.CONNECTION_FAILED;
            var message = "Failed to connect to device";

            // Act
            var error = ComponentError.CreateCommunicationError(
                deviceId,
                errorCode,
                message,
                ErrorSeverity.Critical);

            // Assert
            error.DeviceId.Should().Be(deviceId);
            error.ErrorCode.Should().Be(errorCode);
            error.Message.Should().Be(message);
            error.Severity.Should().Be(ErrorSeverity.Critical);
            error.Source.Should().Be(ErrorSource.Communication);
            error.Category.Should().Be(ErrorCategory.Communication);
        }

        [Fact]
        public void DeriveCategory_ShouldDeriveCorrectCategory()
        {
            // Arrange & Act & Assert
            var deviceError = new ComponentError(
                Guid.NewGuid(),
                "DEVICE_TEST_ERROR",
                "Test message",
                ErrorSeverity.Error,
                ErrorSource.Device);
            deviceError.Category.Should().Be(ErrorCategory.Device);

            var serviceError = new ComponentError(
                Guid.NewGuid(),
                "SERVICE_TEST_ERROR",
                "Test message",
                ErrorSeverity.Error,
                ErrorSource.Service);
            serviceError.Category.Should().Be(ErrorCategory.Service);

            var commError = new ComponentError(
                Guid.NewGuid(),
                "COMM_TEST_ERROR",
                "Test message",
                ErrorSeverity.Error,
                ErrorSource.Communication);
            commError.Category.Should().Be(ErrorCategory.Communication);

            var eventError = new ComponentError(
                Guid.NewGuid(),
                "EVENT_TEST_ERROR",
                "Test message",
                ErrorSeverity.Error,
                ErrorSource.Service);
            eventError.Category.Should().Be(ErrorCategory.EventSystem);

            var storageError = new ComponentError(
                Guid.NewGuid(),
                "STORAGE_TEST_ERROR",
                "Test message",
                ErrorSeverity.Error,
                ErrorSource.Database);
            storageError.Category.Should().Be(ErrorCategory.Storage);

            var unknownError = new ComponentError(
                Guid.NewGuid(),
                "UNKNOWN_TEST_ERROR",
                "Test message",
                ErrorSeverity.Error,
                ErrorSource.Unknown);
            unknownError.Category.Should().Be(ErrorCategory.Unknown);
        }

        [Fact]
        public void EnrichContext_ShouldAddDefaultFields()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var errorCode = "DEVICE_TEST_ERROR";
            var message = "Test error message";
            var exception = new InvalidOperationException("Test exception", 
                new ArgumentException("Inner test exception"));

            // Act
            var error = new ComponentError(
                deviceId,
                errorCode,
                message,
                ErrorSeverity.Error,
                ErrorSource.Device,
                null,
                exception);

            // Assert
            error.Context.Should().ContainKey("Timestamp");
            error.Context.Should().ContainKey("ErrorId");
            error.Context.Should().ContainKey("DeviceId");
            error.Context.Should().ContainKey("ErrorCode");
            error.Context.Should().ContainKey("ErrorCategory");
            error.Context.Should().ContainKey("ExceptionType");
            error.Context.Should().ContainKey("InnerExceptionType");
            
            error.Context["DeviceId"].Should().Be(deviceId.ToString());
            error.Context["ErrorCode"].Should().Be(errorCode);
            error.Context["ErrorCategory"].Should().Be(ErrorCategory.Device.ToString());
            error.Context["ExceptionType"].Should().Be("InvalidOperationException");
            error.Context["InnerExceptionType"].Should().Be("ArgumentException");
        }

        [Fact]
        public void ToString_ShouldReturnFormattedString()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var errorCode = "DEVICE_TEST_ERROR";
            var message = "Test error message";
            var error = new ComponentError(
                deviceId,
                errorCode,
                message,
                ErrorSeverity.Critical,
                ErrorSource.Device);

            // Act
            var result = error.ToString();

            // Assert
            result.Should().Contain("[Critical]");
            result.Should().Contain("[DEVICE_TEST_ERROR]");
            result.Should().Contain("Test error message");
            result.Should().Contain($"DeviceId: {deviceId}");
            result.Should().Contain("Timestamp:");
        }
    }
}