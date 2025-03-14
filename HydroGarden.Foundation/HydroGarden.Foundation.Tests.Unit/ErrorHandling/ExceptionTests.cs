using FluentAssertions;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.ErrorHandling.Common;
using HydroGarden.Foundation.ErrorHandling.Exceptions;
using Xunit;

namespace HydroGarden.Foundation.Tests.Unit.ErrorHandling
{
    public class ExceptionTests
    {
        [Fact]
        public void DeviceExceptions_ShouldHaveCorrectProperties()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var message = "Test device initialization failed";
            var innerException = new TimeoutException("Connection timed out");
            var context = new Dictionary<string, object>
            {
                { "TestKey", "TestValue" }
            };

            // Act
            var exception = new DeviceInitializationException(
                message,
                deviceId,
                innerException,
                context);

            // Assert
            exception.Message.Should().Be(message);
            exception.DeviceId.Should().Be(deviceId);
            exception.InnerException.Should().BeSameAs(innerException);
            exception.ErrorCode.Should().Be(ErrorCodes.Device.INITIALIZATION_FAILED);
            exception.Severity.Should().Be(ErrorSeverity.Error);
            exception.Source.Should().Be(ErrorSource.Device);
            exception.Category.Should().Be(ErrorCategory.Device);
            exception.Context.Should().ContainKey("TestKey");
            exception.Context["TestKey"].Should().Be("TestValue");
        }

        [Fact]
        public void DeviceCommunicationException_ShouldHaveCorrectProperties()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var message = "Communication lost with device";

            // Act
            var exception = new DeviceCommunicationException(message, deviceId);

            // Assert
            exception.Message.Should().Be(message);
            exception.DeviceId.Should().Be(deviceId);
            exception.ErrorCode.Should().Be(ErrorCodes.Device.COMMUNICATION_LOST);
            exception.Severity.Should().Be(ErrorSeverity.Critical);
            exception.Source.Should().Be(ErrorSource.Communication);
            exception.Category.Should().Be(ErrorCategory.Communication);
        }

        [Fact]
        public void DeviceStateException_ShouldStoreStateTransitionInfo()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var message = "Failed to transition device state";
            var fromState = "Idle";
            var toState = "Running";

            // Act
            var exception = new DeviceStateException(
                message,
                deviceId,
                fromState,
                toState);

            // Assert
            exception.Message.Should().Be(message);
            exception.DeviceId.Should().Be(deviceId);
            exception.ErrorCode.Should().Be(ErrorCodes.Device.STATE_TRANSITION_FAILED);
            exception.Context.Should().ContainKey("FromState");
            exception.Context["FromState"].Should().Be(fromState);
            exception.Context.Should().ContainKey("ToState");
            exception.Context["ToState"].Should().Be(toState);
        }

        [Fact]
        public void ServiceExceptions_ShouldHaveCorrectProperties()
        {
            // Arrange
            var serviceName = "TestService";
            var message = "Service initialization failed";
            var innerException = new InvalidOperationException("Required dependency not found");

            // Act
            var exception = new ServiceInitializationException(
                message,
                serviceName,
                innerException);

            // Assert
            exception.Message.Should().Be(message);
            exception.ErrorCode.Should().Be(ErrorCodes.Service.INITIALIZATION_FAILED);
            exception.Severity.Should().Be(ErrorSeverity.Critical);
            exception.Source.Should().Be(ErrorSource.Service);
            exception.Category.Should().Be(ErrorCategory.Service);
            exception.Context.Should().ContainKey("ServiceName");
            exception.Context["ServiceName"].Should().Be(serviceName);
        }

        [Fact]
        public void ServiceTimeoutException_ShouldStoreTimeoutInfo()
        {
            // Arrange
            var serviceName = "TestService";
            var operationName = "DataProcessing";
            var timeout = 5000;
            var message = "Service operation timed out";

            // Act
            var exception = new ServiceTimeoutException(
                message,
                serviceName,
                operationName,
                timeout);

            // Assert
            exception.Message.Should().Be(message);
            exception.ErrorCode.Should().Be(ErrorCodes.Service.OPERATION_TIMEOUT);
            exception.Context.Should().ContainKey("ServiceName");
            exception.Context["ServiceName"].Should().Be(serviceName);
            exception.Context.Should().ContainKey("OperationName");
            exception.Context["OperationName"].Should().Be(operationName);
            exception.Context.Should().ContainKey("TimeoutMs");
            exception.Context["TimeoutMs"].Should().Be(timeout);
        }

        [Fact]
        public void CommunicationExceptions_ShouldHaveCorrectProperties()
        {
            // Arrange
            var endpoint = "tcp://server:1234";
            var message = "Connection failed to endpoint";
            var deviceId = Guid.NewGuid();

            // Act
            var exception = new ConnectionFailedException(
                message,
                endpoint,
                deviceId);

            // Assert
            exception.Message.Should().Be(message);
            exception.DeviceId.Should().Be(deviceId);
            exception.ErrorCode.Should().Be(ErrorCodes.Communication.CONNECTION_FAILED);
            exception.Severity.Should().Be(ErrorSeverity.Critical);
            exception.Source.Should().Be(ErrorSource.Communication);
            exception.Category.Should().Be(ErrorCategory.Communication);
            exception.Context.Should().ContainKey("Endpoint");
            exception.Context["Endpoint"].Should().Be(endpoint);
        }

        [Fact]
        public void CommunicationTimeoutException_ShouldStoreTimeoutInfo()
        {
            // Arrange
            var endpoint = "tcp://server:1234";
            var timeout = 3000;
            var message = "Communication timed out";

            // Act
            var exception = new CommunicationTimeoutException(
                message,
                endpoint,
                timeout);

            // Assert
            exception.Message.Should().Be(message);
            exception.ErrorCode.Should().Be(ErrorCodes.Communication.TIMEOUT);
            exception.Context.Should().ContainKey("Endpoint");
            exception.Context["Endpoint"].Should().Be(endpoint);
            exception.Context.Should().ContainKey("TimeoutMs");
            exception.Context["TimeoutMs"].Should().Be(timeout);
        }

        [Fact]
        public void StorageExceptions_ShouldHaveCorrectProperties()
        {
            // Arrange
            var storageName = "ConfigStorage";
            var resourcePath = "/devices/config.json";
            var message = "Failed to read configuration";

            // Act
            var exception = new StorageReadException(
                message,
                storageName,
                resourcePath);

            // Assert
            exception.Message.Should().Be(message);
            exception.ErrorCode.Should().Be(ErrorCodes.Storage.READ_FAILED);
            exception.Severity.Should().Be(ErrorSeverity.Error);
            exception.Source.Should().Be(ErrorSource.Database);
            exception.Category.Should().Be(ErrorCategory.Storage);
            exception.Context.Should().ContainKey("StorageName");
            exception.Context["StorageName"].Should().Be(storageName);
            exception.Context.Should().ContainKey("ResourcePath");
            exception.Context["ResourcePath"].Should().Be(resourcePath);
        }

        [Fact]
        public void DataValidationException_ShouldStoreValidationErrors()
        {
            // Arrange
            var message = "Data validation failed";
            var dataType = "DeviceConfiguration";
            var validationErrors = new[] { "Name is required", "Port must be between 1024 and 65535" };

            // Act
            var exception = new DataValidationException(
                message,
                validationErrors,
                dataType);

            // Assert
            exception.Message.Should().Be(message);
            exception.Source.Should().Be(ErrorSource.Service);
            exception.Category.Should().Be(ErrorCategory.Storage);
            exception.Context.Should().ContainKey("DataType");
            exception.Context["DataType"].Should().Be(dataType);
            exception.Context.Should().ContainKey("ValidationErrors");
            exception.Context["ValidationErrors"].Should().Be(string.Join(", ", validationErrors));
        }

        [Fact]
        public void EventExceptions_ShouldHaveCorrectProperties()
        {
            // Arrange
            var eventType = "DeviceStatusChanged";
            var eventId = Guid.NewGuid();
            var message = "Failed to publish event";

            // Act
            var exception = new EventPublicationException(
                message,
                eventType,
                eventId);

            // Assert
            exception.Message.Should().Be(message);
            exception.ErrorCode.Should().Be(ErrorCodes.Event.PUBLICATION_FAILED);
            exception.Severity.Should().Be(ErrorSeverity.Error);
            exception.Source.Should().Be(ErrorSource.Service);
            exception.Category.Should().Be(ErrorCategory.EventSystem);
            exception.Context.Should().ContainKey("EventType");
            exception.Context["EventType"].Should().Be(eventType);
            exception.Context.Should().ContainKey("EventId");
            exception.Context["EventId"].Should().Be(eventId.ToString());
        }

        [Fact]
        public void ApplicationException_ToApplicationError_ShouldConvertCorrectly()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var message = "Test device initialization failed";
            var exception = new DeviceInitializationException(message, deviceId);

            // Act
            var error = exception.ToApplicationError();

            // Assert
            error.Should().NotBeNull();
            error.DeviceId.Should().Be(deviceId);
            error.Message.Should().Be(message);
            error.ErrorCode.Should().Be(ErrorCodes.Device.INITIALIZATION_FAILED);
            error.Severity.Should().Be(ErrorSeverity.Error);
            error.Source.Should().Be(ErrorSource.Device);
            error.Category.Should().Be(ErrorCategory.Device);
            error.Exception.Should().BeSameAs(exception);
        }
    }
}