using FluentAssertions;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.ErrorHandling;
using HydroGarden.Foundation.ErrorHandling.Common;
using Xunit;

namespace HydroGarden.Foundation.Tests.ErrorHandling.Components
{
    /// <summary>
    /// Unit tests for the ComponentError class.
    /// </summary>
    public class ComponentErrorTests
    {
        private readonly Guid _deviceId = Guid.NewGuid();
        private readonly string _errorCode = ErrorCodes.Device.SENSOR_MALFUNCTION;
        private readonly string _errorMessage = "Sensor reading anomaly detected";

        [Fact]
        public void Constructor_ShouldInitializePropertiesCorrectly()
        {
            // Arrange
            var context = new Dictionary<string, object>
            {
                ["TestKey"] = "TestValue"
            };
            var exception = new InvalidOperationException("Test exception");

            // Act
            var error = new ComponentError(
                _deviceId,
                _errorCode,
                _errorMessage,
                ErrorSeverity.Error,
                true,
                ErrorSource.Device,
                false,
                context,
                exception);

            // Assert
            error.DeviceId.Should().Be(_deviceId);
            error.ErrorCode.Should().Be(_errorCode);
            error.Message.Should().Be(_errorMessage);
            error.Severity.Should().Be(ErrorSeverity.Error);
            error.IsRecoverable.Should().BeTrue();
            error.Source.Should().Be(ErrorSource.Device);
            error.IsTransient.Should().BeFalse();
            error.Context.Should().ContainKey("TestKey");
            error.Context["TestKey"].Should().Be("TestValue");
            error.Exception.Should().BeSameAs(exception);
            error.RecoveryAttemptCount.Should().Be(0);
            error.LastRecoveryAttempt.Should().BeNull();
        }

        [Fact]
        public void CreateNonRecoverable_ShouldCreateErrorWithCorrectProperties()
        {
            // Act
            var error = ComponentError.CreateNonRecoverable(
                _deviceId,
                _errorCode,
                _errorMessage);

            // Assert
            error.DeviceId.Should().Be(_deviceId);
            error.ErrorCode.Should().Be(_errorCode);
            error.Message.Should().Be(_errorMessage);
            error.Severity.Should().Be(ErrorSeverity.Critical);
            error.IsRecoverable.Should().BeFalse();
            error.IsTransient.Should().BeFalse();
        }

        [Fact]
        public void CreateTransient_ShouldCreateErrorWithCorrectProperties()
        {
            // Act
            var error = ComponentError.CreateTransient(
                _deviceId,
                _errorCode,
                _errorMessage);

            // Assert
            error.DeviceId.Should().Be(_deviceId);
            error.ErrorCode.Should().Be(_errorCode);
            error.Message.Should().Be(_errorMessage);
            error.Severity.Should().Be(ErrorSeverity.Error);
            error.IsRecoverable.Should().BeTrue();
            error.IsTransient.Should().BeTrue();
        }

        [Fact]
        public void RecordRecoveryAttempt_ShouldIncrementCountAndUpdateTimestamp()
        {
            // Arrange
            var error = ComponentError.CreateTransient(
                _deviceId,
                _errorCode,
                _errorMessage);

            // Act - record multiple attempts
            error.RecordRecoveryAttempt();
            error.RecordRecoveryAttempt();

            // Assert
            error.RecoveryAttemptCount.Should().Be(2);
            error.LastRecoveryAttempt.Should().NotBeNull();
            error.LastRecoveryAttempt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
        }

        [Fact]
        public void CanAttemptRecovery_ShouldReturnFalseWhenNotRecoverable()
        {
            // Arrange
            var error = ComponentError.CreateNonRecoverable(
                _deviceId,
                _errorCode,
                _errorMessage);

            // Act
            var canRecover = error.CanAttemptRecovery();

            // Assert
            canRecover.Should().BeFalse();
        }

        [Fact]
        public void CanAttemptRecovery_ShouldReturnFalseWhenMaxAttemptsReached()
        {
            // Arrange
            var error = ComponentError.CreateTransient(
                _deviceId,
                _errorCode,
                _errorMessage);

            // Act - exceed max recovery attempts
            for (int i = 0; i < error.MaxRecoveryAttempts; i++)
            {
                error.RecordRecoveryAttempt();
            }

            // Assert
            error.CanAttemptRecovery().Should().BeFalse();
        }

        [Fact]
        public void CanAttemptRecovery_ShouldReturnFalseWhenBackoffPeriodNotElapsed()
        {
            // Arrange
            var error = ComponentError.CreateTransient(
                _deviceId,
                _errorCode,
                _errorMessage);

            // Act - make one recovery attempt
            error.RecordRecoveryAttempt();

            // Assert - should not be able to recover immediately due to backoff
            error.CanAttemptRecovery().Should().BeFalse();
        }

        [Fact]
        public void IsUnrecoverable_ShouldReturnTrueForNonRecoverableError()
        {
            // Arrange
            var error = ComponentError.CreateNonRecoverable(
                _deviceId,
                _errorCode,
                _errorMessage);

            // Assert
            error.IsUnrecoverable.Should().BeTrue();
        }

        [Fact]
        public void IsUnrecoverable_ShouldReturnTrueWhenMaxAttemptsReached()
        {
            // Arrange
            var error = ComponentError.CreateTransient(
                _deviceId,
                _errorCode,
                _errorMessage);

            // Act - exceed max recovery attempts
            for (int i = 0; i < error.MaxRecoveryAttempts; i++)
            {
                error.RecordRecoveryAttempt();
            }

            // Assert
            error.IsUnrecoverable.Should().BeTrue();
        }

        [Fact]
        public void IsUnrecoverable_ShouldReturnTrueForUnrecoverableErrorCode()
        {
            // Arrange
            var error = new ComponentError(
                _deviceId,
                ErrorCodes.Device.HARDWARE_FAILURE, // This is defined as unrecoverable in ErrorCodes
                _errorMessage,
                ErrorSeverity.Critical,
                true, // Note: we say it's recoverable, but the error code should override
                ErrorSource.Device,
                false);

            // Assert
            error.IsUnrecoverable.Should().BeTrue();
        }

        [Fact]
        public void DeriveCategory_ShouldReturnAppropriateCategory()
        {
            // Arrange & Act
            var deviceError = new ComponentError(
                _deviceId,
                "DEVICE_TEST",
                _errorMessage,
                ErrorSeverity.Error,
                true,
                ErrorSource.Device,
                false);

            var serviceError = new ComponentError(
                _deviceId,
                "SERVICE_TEST",
                _errorMessage,
                ErrorSeverity.Error,
                true,
                ErrorSource.Service,
                false);

            var commError = new ComponentError(
                _deviceId,
                "COMM_TEST",
                _errorMessage,
                ErrorSeverity.Error,
                true,
                ErrorSource.Communication,
                false);

            var unknownError = new ComponentError(
                _deviceId,
                "UNKNOWN_TEST",
                _errorMessage,
                ErrorSeverity.Error,
                true,
                ErrorSource.Unknown,
                false);

            // Assert
            deviceError.Category.Should().Be(ErrorCategory.Device);
            serviceError.Category.Should().Be(ErrorCategory.Service);
            commError.Category.Should().Be(ErrorCategory.Communication);
            unknownError.Category.Should().Be(ErrorCategory.Unknown);
        }

        [Fact]
        public void ToString_ShouldReturnFormattedString()
        {
            // Arrange
            var error = new ComponentError(
                _deviceId,
                _errorCode,
                _errorMessage,
                ErrorSeverity.Error,
                true,
                ErrorSource.Device,
                false);

            // Act
            var result = error.ToString();

            // Assert
            result.Should().Contain(_deviceId.ToString());
            result.Should().Contain(_errorCode);
            result.Should().Contain(_errorMessage);
            result.Should().Contain("Error");
        }

        [Fact]
        public void EnrichContext_ShouldAddRequiredInformation()
        {
            // Arrange
            var exception = new InvalidOperationException("Test exception", new ArgumentException("Inner exception"));

            // Act
            var error = new ComponentError(
                _deviceId,
                _errorCode,
                _errorMessage,
                ErrorSeverity.Error,
                true,
                ErrorSource.Device,
                false,
                exception: exception);

            // Assert
            error.Context.Should().ContainKey("Timestamp");
            error.Context.Should().ContainKey("ErrorId");
            error.Context.Should().ContainKey("DeviceId");
            error.Context.Should().ContainKey("ErrorCode");
            error.Context.Should().ContainKey("ErrorCategory");
            error.Context.Should().ContainKey("ExceptionType");
            error.Context.Should().ContainKey("InnerExceptionType");
            error.Context["ExceptionType"].Should().Be("InvalidOperationException");
            error.Context["InnerExceptionType"].Should().Be("ArgumentException");
        }

        [Fact]
        public void RecoveryBackoffInterval_ShouldIncreaseExponentially()
        {
            // Arrange
            var error = ComponentError.CreateTransient(
                _deviceId,
                _errorCode,
                _errorMessage);

            // Act & Assert - Check exponential growth
            error.RecoveryBackoffInterval.Should().Be(TimeSpan.FromSeconds(1)); // 2^0 = 1

            error.RecordRecoveryAttempt();
            error.RecoveryBackoffInterval.Should().Be(TimeSpan.FromSeconds(2)); // 2^1 = 2

            error.RecordRecoveryAttempt();
            error.RecoveryBackoffInterval.Should().Be(TimeSpan.FromSeconds(4)); // 2^2 = 4

            error.RecordRecoveryAttempt();
            error.RecoveryBackoffInterval.Should().Be(TimeSpan.FromSeconds(8)); // 2^3 = 8

            // Act & Assert - Check it caps at the maximum (10 minutes = 600 seconds)
            for (int i = 0; i < 10; i++)
            {
                error.RecordRecoveryAttempt();
            }
            error.RecoveryBackoffInterval.Should().Be(TimeSpan.FromSeconds(600)); // Capped at 10 minutes
        }
    }
}
