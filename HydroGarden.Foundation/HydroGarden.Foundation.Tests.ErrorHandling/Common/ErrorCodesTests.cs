using FluentAssertions;
using HydroGarden.Foundation.ErrorHandling.Common;
using Xunit;

namespace HydroGarden.Foundation.Tests.ErrorHandling.Common
{
    /// <summary>
    /// Unit tests for the ErrorCodes class.
    /// </summary>
    public class ErrorCodesTests
    {
        [Fact]
        public void IsUnrecoverable_WithHardwareFailure_ShouldReturnTrue()
        {
            // Arrange & Act & Assert
            ErrorCodes.IsUnrecoverable(ErrorCodes.Device.HARDWARE_FAILURE).Should().BeTrue();
        }

        [Fact]
        public void IsUnrecoverable_WithConfigurationInvalid_ShouldReturnTrue()
        {
            // Arrange & Act & Assert
            ErrorCodes.IsUnrecoverable(ErrorCodes.Device.CONFIGURATION_INVALID).Should().BeTrue();
        }

        [Fact]
        public void IsUnrecoverable_WithAttemptLimitReached_ShouldReturnTrue()
        {
            // Arrange & Act & Assert
            ErrorCodes.IsUnrecoverable(ErrorCodes.Recovery.ATTEMPT_LIMIT_REACHED).Should().BeTrue();
        }

        [Fact]
        public void IsUnrecoverable_WithDependencyUnrecoverable_ShouldReturnTrue()
        {
            // Arrange & Act & Assert
            ErrorCodes.IsUnrecoverable(ErrorCodes.Recovery.DEPENDENCY_UNRECOVERABLE).Should().BeTrue();
        }

        [Fact]
        public void IsUnrecoverable_WithDataCorruption_ShouldReturnTrue()
        {
            // Arrange & Act & Assert
            ErrorCodes.IsUnrecoverable(ErrorCodes.Storage.DATA_CORRUPTION).Should().BeTrue();
        }

        [Fact]
        public void IsUnrecoverable_WithRecoverableErrorCode_ShouldReturnFalse()
        {
            // Arrange & Act & Assert
            ErrorCodes.IsUnrecoverable(ErrorCodes.Device.SENSOR_MALFUNCTION).Should().BeFalse();
            ErrorCodes.IsUnrecoverable(ErrorCodes.Communication.CONNECTION_FAILED).Should().BeFalse();
            ErrorCodes.IsUnrecoverable(ErrorCodes.Service.OPERATION_TIMEOUT).Should().BeFalse();
            ErrorCodes.IsUnrecoverable(ErrorCodes.Event.HANDLER_EXCEPTION).Should().BeFalse();
            ErrorCodes.IsUnrecoverable(ErrorCodes.Storage.READ_FAILED).Should().BeFalse();
        }

        [Fact]
        public void IsUnrecoverable_WithNullOrEmptyErrorCode_ShouldReturnFalse()
        {
            // Arrange & Act & Assert
            ErrorCodes.IsUnrecoverable(null).Should().BeFalse();
            ErrorCodes.IsUnrecoverable(string.Empty).Should().BeFalse();
        }

        [Fact]
        public void IsUnrecoverable_WithUnknownErrorCode_ShouldReturnFalse()
        {
            // Arrange & Act & Assert
            ErrorCodes.IsUnrecoverable("UNKNOWN_ERROR_CODE").Should().BeFalse();
        }

        [Fact]
        public void DeviceErrorCodes_ShouldBeDefinedWithCorrectPrefix()
        {
            // Arrange & Act & Assert
            ErrorCodes.Device.INITIALIZATION_FAILED.Should().StartWith("DEVICE_");
            ErrorCodes.Device.COMMUNICATION_LOST.Should().StartWith("DEVICE_");
            ErrorCodes.Device.CALIBRATION_ERROR.Should().StartWith("DEVICE_");
            ErrorCodes.Device.HARDWARE_FAILURE.Should().StartWith("DEVICE_");
            ErrorCodes.Device.SENSOR_MALFUNCTION.Should().StartWith("DEVICE_");
            ErrorCodes.Device.CONFIGURATION_INVALID.Should().StartWith("DEVICE_");
            ErrorCodes.Device.STATE_TRANSITION_FAILED.Should().StartWith("DEVICE_");
            ErrorCodes.Device.RESOURCE_EXHAUSTED.Should().StartWith("DEVICE_");
        }

        [Fact]
        public void ServiceErrorCodes_ShouldBeDefinedWithCorrectPrefix()
        {
            // Arrange & Act & Assert
            ErrorCodes.Service.INITIALIZATION_FAILED.Should().StartWith("SERVICE_");
            ErrorCodes.Service.OPERATION_TIMEOUT.Should().StartWith("SERVICE_");
            ErrorCodes.Service.DEPENDENCY_UNAVAILABLE.Should().StartWith("SERVICE_");
            ErrorCodes.Service.RESOURCE_EXHAUSTED.Should().StartWith("SERVICE_");
            ErrorCodes.Service.CONCURRENT_ACCESS_CONFLICT.Should().StartWith("SERVICE_");
            ErrorCodes.Service.CONFIGURATION_INVALID.Should().StartWith("SERVICE_");
        }

        [Fact]
        public void CommunicationErrorCodes_ShouldBeDefinedWithCorrectPrefix()
        {
            // Arrange & Act & Assert
            ErrorCodes.Communication.MESSAGE_DELIVERY_FAILED.Should().StartWith("COMM_");
            ErrorCodes.Communication.CONNECTION_FAILED.Should().StartWith("COMM_");
            ErrorCodes.Communication.PROTOCOL_ERROR.Should().StartWith("COMM_");
            ErrorCodes.Communication.TIMEOUT.Should().StartWith("COMM_");
            ErrorCodes.Communication.SERIALIZATION_ERROR.Should().StartWith("COMM_");
        }

        [Fact]
        public void EventErrorCodes_ShouldBeDefinedWithCorrectPrefix()
        {
            // Arrange & Act & Assert
            ErrorCodes.Event.PUBLICATION_FAILED.Should().StartWith("EVENT_");
            ErrorCodes.Event.SUBSCRIPTION_ERROR.Should().StartWith("EVENT_");
            ErrorCodes.Event.HANDLER_EXCEPTION.Should().StartWith("EVENT_");
            ErrorCodes.Event.ROUTING_ERROR.Should().StartWith("EVENT_");
            ErrorCodes.Event.PROCESSING_TIMEOUT.Should().StartWith("EVENT_");
        }

        [Fact]
        public void StorageErrorCodes_ShouldBeDefinedWithCorrectPrefix()
        {
            // Arrange & Act & Assert
            ErrorCodes.Storage.READ_FAILED.Should().StartWith("STORAGE_");
            ErrorCodes.Storage.WRITE_FAILED.Should().StartWith("STORAGE_");
            ErrorCodes.Storage.TRANSACTION_FAILED.Should().StartWith("STORAGE_");
            ErrorCodes.Storage.DATA_CORRUPTION.Should().StartWith("STORAGE_");
            ErrorCodes.Storage.SERIALIZATION_ERROR.Should().StartWith("STORAGE_");
        }

        [Fact]
        public void RecoveryErrorCodes_ShouldBeDefinedWithCorrectPrefix()
        {
            // Arrange & Act & Assert
            ErrorCodes.Recovery.STRATEGY_FAILED.Should().StartWith("RECOVERY_");
            ErrorCodes.Recovery.ATTEMPT_LIMIT_REACHED.Should().StartWith("RECOVERY_");
            ErrorCodes.Recovery.CIRCUIT_OPEN.Should().StartWith("RECOVERY_");
            ErrorCodes.Recovery.DEPENDENCY_UNRECOVERABLE.Should().StartWith("RECOVERY_");
        }
    }
}
