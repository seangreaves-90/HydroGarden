using FluentAssertions;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.ErrorHandling;
using HydroGarden.Foundation.ErrorHandling.Common;
using HydroGarden.Foundation.ErrorHandling.Exceptions;
using Xunit;

namespace HydroGarden.Foundation.Tests.Unit.ErrorHandling
{
    public class ErrorFactoryTests
    {
        [Fact]
        public void CreateDeviceError_ShouldCreateErrorWithDeviceProperties()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var errorCode = ErrorCodes.Device.SENSOR_MALFUNCTION;
            var message = "Temperature sensor malfunction";
            var exception = new InvalidOperationException("Sensor reading out of range");
            var context = new Dictionary<string, object>
            {
                { "SensorType", "Temperature" },
                { "Reading", -50.0 }
            };

            // Act
            var error = ErrorFactory.CreateDeviceError(
                deviceId,
                errorCode,
                message,
                ErrorSeverity.Error,
                exception,
                context);

            // Assert
            error.Should().NotBeNull();
            error.DeviceId.Should().Be(deviceId);
            error.ErrorCode.Should().Be(errorCode);
            error.Message.Should().Be(message);
            error.Severity.Should().Be(ErrorSeverity.Error);
            error.Source.Should().Be(ErrorSource.Device);
            error.Category.Should().Be(ErrorCategory.Device);
            error.Exception.Should().BeSameAs(exception);
            error.Context.Should().ContainKey("SensorType");
            error.Context["SensorType"].Should().Be("Temperature");
            error.Context.Should().ContainKey("Reading");
            error.Context["Reading"].Should().Be(-50.0);
        }

        [Fact]
        public void CreateServiceError_ShouldCreateErrorWithServiceProperties()
        {
            // Arrange
            var serviceName = "TemperatureSensorService";
            var errorCode = ErrorCodes.Service.INITIALIZATION_FAILED;
            var message = "Failed to initialize sensor service";
            var severity = ErrorSeverity.Critical;

            // Act
            var error = ErrorFactory.CreateServiceError(
                serviceName,
                errorCode,
                message,
                severity);

            // Assert
            error.Should().NotBeNull();
            error.DeviceId.Should().Be(Guid.Empty);
            error.ErrorCode.Should().Be(errorCode);
            error.Message.Should().Be(message);
            error.Severity.Should().Be(severity);
            error.Source.Should().Be(ErrorSource.Service);
            error.Category.Should().Be(ErrorCategory.Service);
            error.Context.Should().ContainKey("ServiceName");
            error.Context["ServiceName"].Should().Be(serviceName);
        }

        [Fact]
        public void CreateCommunicationError_ShouldCreateErrorWithCommunicationProperties()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var errorCode = ErrorCodes.Communication.CONNECTION_FAILED;
            var message = "Failed to connect to device";
            var context = new Dictionary<string, object>
            {
                { "Endpoint", "tcp://device:1234" }
            };

            // Act
            var error = ErrorFactory.CreateCommunicationError(
                deviceId,
                errorCode,
                message,
                ErrorSeverity.Critical,
                null,
                context);

            // Assert
            error.Should().NotBeNull();
            error.DeviceId.Should().Be(deviceId);
            error.ErrorCode.Should().Be(errorCode);
            error.Message.Should().Be(message);
            error.Severity.Should().Be(ErrorSeverity.Critical);
            error.Source.Should().Be(ErrorSource.Communication);
            error.Category.Should().Be(ErrorCategory.Communication);
            error.Context.Should().ContainKey("Endpoint");
            error.Context["Endpoint"].Should().Be("tcp://device:1234");
        }

        [Fact]
        public void CreateStorageError_ShouldCreateErrorWithStorageProperties()
        {
            // Arrange
            var errorCode = ErrorCodes.Storage.READ_FAILED;
            var message = "Failed to read configuration data";
            var context = new Dictionary<string, object>
            {
                { "FilePath", "/config/device.json" }
            };

            // Act
            var error = ErrorFactory.CreateStorageError(
                errorCode,
                message,
                ErrorSeverity.Error,
                null,
                context);

            // Assert
            error.Should().NotBeNull();
            error.DeviceId.Should().Be(Guid.Empty);
            error.ErrorCode.Should().Be(errorCode);
            error.Message.Should().Be(message);
            error.Severity.Should().Be(ErrorSeverity.Error);
            error.Source.Should().Be(ErrorSource.Database);
            error.Category.Should().Be(ErrorCategory.Storage);
            error.Context.Should().ContainKey("FilePath");
            error.Context["FilePath"].Should().Be("/config/device.json");
        }

        [Fact]
        public void CreateEventSystemError_ShouldCreateErrorWithEventSystemProperties()
        {
            // Arrange
            var errorCode = ErrorCodes.Event.PUBLICATION_FAILED;
            var message = "Failed to publish event";
            var context = new Dictionary<string, object>
            {
                { "EventType", "DeviceStatusChanged" },
                { "EventId", Guid.NewGuid().ToString() }
            };

            // Act
            var error = ErrorFactory.CreateEventSystemError(
                errorCode,
                message,
                ErrorSeverity.Error,
                null,
                context);

            // Assert
            error.Should().NotBeNull();
            error.DeviceId.Should().Be(Guid.Empty);
            error.ErrorCode.Should().Be(errorCode);
            error.Message.Should().Be(message);
            error.Severity.Should().Be(ErrorSeverity.Error);
            error.Source.Should().Be(ErrorSource.Service);
            error.Category.Should().Be(ErrorCategory.EventSystem);
            error.Context.Should().ContainKey("EventType");
            error.Context["EventType"].Should().Be("DeviceStatusChanged");
        }

        [Fact]
        public void FromException_WithApplicationException_ShouldPreserveProperties()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var message = "Device initialization failed";
            var exception = new DeviceInitializationException(message, deviceId);

            // Act
            var error = ErrorFactory.FromException(exception);

            // Assert
            error.Should().NotBeNull();
            error.DeviceId.Should().Be(deviceId);
            error.ErrorCode.Should().Be(ErrorCodes.Device.INITIALIZATION_FAILED);
            error.Message.Should().Be(message);
            error.Severity.Should().Be(ErrorSeverity.Error);
            error.Source.Should().Be(ErrorSource.Device);
            error.Category.Should().Be(ErrorCategory.Device);
            error.Exception.Should().BeSameAs(exception);
        }

        [Fact]
        public void FromException_WithStandardException_ShouldDeriveProperties()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var message = "Operation timeout";
            var exception = new TimeoutException(message);

            // Act
            var error = ErrorFactory.FromException(exception, deviceId);

            // Assert
            error.Should().NotBeNull();
            error.DeviceId.Should().Be(deviceId);
            error.ErrorCode.Should().Be("SERVICE_OP_TIMEOUT"); // Derived from exception type
            error.Message.Should().Be(message);
            error.Exception.Should().BeSameAs(exception);
            error.Context.Should().ContainKey("ExceptionType");
            error.Context["ExceptionType"].Should().Be("TimeoutException");
        }

        [Fact]
        public void FromException_WithExplicitErrorCode_ShouldUseProvidedCode()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var message = "Database connection failed";
            var exception = new InvalidOperationException(message);
            var errorCode = "CUSTOM_ERROR_CODE";

            // Act
            var error = ErrorFactory.FromException(exception, deviceId, errorCode);

            // Assert
            error.Should().NotBeNull();
            error.DeviceId.Should().Be(deviceId);
            error.ErrorCode.Should().Be(errorCode);
            error.Message.Should().Be(message);
            error.Exception.Should().BeSameAs(exception);
        }

        [Fact]
        public void FromException_WithContext_ShouldMergeContext()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var message = "Failed to read sensor data";
            var exception = new IOException(message);
            var context = new Dictionary<string, object>
            {
                { "SensorId", "TEMP001" },
                { "LastReading", DateTime.UtcNow }
            };

            // Act
            var error = ErrorFactory.FromException(exception, deviceId, null, context);

            // Assert
            error.Should().NotBeNull();
            error.DeviceId.Should().Be(deviceId);
            error.Message.Should().Be(message);
            error.Exception.Should().BeSameAs(exception);
            error.Context.Should().ContainKey("SensorId");
            error.Context["SensorId"].Should().Be("TEMP001");
            error.Context.Should().ContainKey("LastReading");
        }

        [Fact]
        public void DeriveErrorCodeFromException_ShouldReturnAppropriateErrorCodes()
        {
            // Act & Assert
            ErrorFactory.DeriveErrorCodeFromException(new TimeoutException()).Should().Be("SERVICE_OP_TIMEOUT");
            ErrorFactory.DeriveErrorCodeFromException(new ArgumentNullException()).Should().Be("SERVICE_INVALID_ARGUMENT");
            ErrorFactory.DeriveErrorCodeFromException(new IOException()).Should().Be("STORAGE_IO_ERROR");
            ErrorFactory.DeriveErrorCodeFromException(new FormatException()).Should().Be("SERVICE_DATA_FORMAT_ERROR");
            ErrorFactory.DeriveErrorCodeFromException(new NotSupportedException()).Should().Be("SERVICE_NOT_SUPPORTED");
            ErrorFactory.DeriveErrorCodeFromException(new UnauthorizedAccessException()).Should().Be("SECURITY_ERROR");
            ErrorFactory.DeriveErrorCodeFromException(new ObjectDisposedException("")).Should().Be("SERVICE_DISPOSED_ERROR");
            ErrorFactory.DeriveErrorCodeFromException(new Exception()).Should().Be("SERVICE_UNHANDLED_EXCEPTION");
        }
    }
}