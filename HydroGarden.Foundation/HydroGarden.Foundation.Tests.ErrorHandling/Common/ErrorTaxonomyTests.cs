using FluentAssertions;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.ErrorHandling;
using HydroGarden.Foundation.ErrorHandling.Common;
using Xunit;

namespace HydroGarden.Foundation.Tests.ErrorHandling.Common
{
    /// <summary>
    /// Unit tests for the ErrorTaxonomy class.
    /// </summary>
    public class ErrorTaxonomyTests
    {
        private readonly Guid _deviceId = Guid.NewGuid();

        [Fact]
        public void AnalyzeRootCause_WithValidErrorCode_ShouldReturnCorrectCause()
        {
            // Arrange & Act & Assert - Test various error codes
            ErrorTaxonomy.AnalyzeRootCause(ErrorCodes.Device.HARDWARE_FAILURE)
                .Should().Be(ErrorTaxonomy.RootCause.HardwareFailure);

            ErrorTaxonomy.AnalyzeRootCause(ErrorCodes.Device.SENSOR_MALFUNCTION)
                .Should().Be(ErrorTaxonomy.RootCause.SensorMalfunction);

            ErrorTaxonomy.AnalyzeRootCause(ErrorCodes.Communication.CONNECTION_FAILED)
                .Should().Be(ErrorTaxonomy.RootCause.NetworkFailure);

            ErrorTaxonomy.AnalyzeRootCause(ErrorCodes.Communication.TIMEOUT)
                .Should().Be(ErrorTaxonomy.RootCause.ConnectionTimeout);
        }

        [Fact]
        public void AnalyzeRootCause_WithNullOrEmptyErrorCode_ShouldReturnUnknown()
        {
            // Arrange & Act & Assert
            ErrorTaxonomy.AnalyzeRootCause(null).Should().Be(ErrorTaxonomy.RootCause.Unknown);
            ErrorTaxonomy.AnalyzeRootCause(string.Empty).Should().Be(ErrorTaxonomy.RootCause.Unknown);
        }

        [Fact]
        public void AnalyzeRootCause_WithUnrecognizedErrorCode_ShouldReturnUnknown()
        {
            // Arrange & Act & Assert
            ErrorTaxonomy.AnalyzeRootCause("UNRECOGNIZED_ERROR_CODE").Should().Be(ErrorTaxonomy.RootCause.Unknown);
        }

        [Fact]
        public void DetermineSystemImpact_ShouldConsiderSeverityAndErrorCode()
        {
            // Arrange & Act & Assert - Catastrophic severity always returns Complete impact
            ErrorTaxonomy.DetermineSystemImpact("ANY_CODE", ErrorSeverity.Catastrophic)
                .Should().Be(ErrorTaxonomy.SystemImpact.Complete);

            // Critical severity returns Significant impact
            ErrorTaxonomy.DetermineSystemImpact("ANY_CODE", ErrorSeverity.Critical)
                .Should().Be(ErrorTaxonomy.SystemImpact.Significant);

            // Hardware failure has Complete impact regardless of severity (except already tested Catastrophic)
            ErrorTaxonomy.DetermineSystemImpact(ErrorCodes.Device.HARDWARE_FAILURE, ErrorSeverity.Error)
                .Should().Be(ErrorTaxonomy.SystemImpact.Complete);

            // Connection failure has Significant impact
            ErrorTaxonomy.DetermineSystemImpact(ErrorCodes.Communication.CONNECTION_FAILED, ErrorSeverity.Error)
                .Should().Be(ErrorTaxonomy.SystemImpact.Significant);

            // Sensor malfunction has Partial impact
            ErrorTaxonomy.DetermineSystemImpact(ErrorCodes.Device.SENSOR_MALFUNCTION, ErrorSeverity.Error)
                .Should().Be(ErrorTaxonomy.SystemImpact.Partial);

            // Error severity with unrecognized code has Partial impact
            ErrorTaxonomy.DetermineSystemImpact("UNRECOGNIZED_CODE", ErrorSeverity.Error)
                .Should().Be(ErrorTaxonomy.SystemImpact.Partial);

            // Warning severity with unrecognized code has Minimal impact
            ErrorTaxonomy.DetermineSystemImpact("UNRECOGNIZED_CODE", ErrorSeverity.Warning)
                .Should().Be(ErrorTaxonomy.SystemImpact.Minimal);
        }

        [Fact]
        public void AssessRecoveryComplexity_WithUnrecoverableError_ShouldReturnManual()
        {
            // Arrange
            var error = ComponentError.CreateNonRecoverable(
                _deviceId,
                ErrorCodes.Device.HARDWARE_FAILURE,
                "Unrecoverable hardware failure");

            // Act
            var complexity = ErrorTaxonomy.AssessRecoveryComplexity(error);

            // Assert
            complexity.Should().Be(ErrorTaxonomy.RecoveryComplexity.Manual);
        }

        [Fact]
        public void AssessRecoveryComplexity_ShouldReturnAppropriateComplexityBasedOnRootCause()
        {
            // Arrange
            var hardwareFailureError = ComponentError.CreateNonRecoverable(
                _deviceId,
                ErrorCodes.Device.HARDWARE_FAILURE,
                "Hardware failure");

            var configError = new ComponentError(
                _deviceId,
                ErrorCodes.Device.CONFIGURATION_INVALID,
                "Configuration error",
                ErrorSeverity.Error,
                true,
                ErrorSource.Device,
                false);

            var networkError = new ComponentError(
                _deviceId,
                ErrorCodes.Communication.CONNECTION_FAILED,
                "Network failure",
                ErrorSeverity.Error,
                true,
                ErrorSource.Communication,
                false);

            var timeoutError = new ComponentError(
                _deviceId,
                ErrorCodes.Communication.TIMEOUT,
                "Connection timeout",
                ErrorSeverity.Error,
                true,
                ErrorSource.Communication,
                true);

            // Act & Assert
            ErrorTaxonomy.AssessRecoveryComplexity(hardwareFailureError).Should().Be(ErrorTaxonomy.RecoveryComplexity.Manual);
            ErrorTaxonomy.AssessRecoveryComplexity(configError).Should().Be(ErrorTaxonomy.RecoveryComplexity.Complex);
            ErrorTaxonomy.AssessRecoveryComplexity(networkError).Should().Be(ErrorTaxonomy.RecoveryComplexity.Moderate);
            ErrorTaxonomy.AssessRecoveryComplexity(timeoutError).Should().Be(ErrorTaxonomy.RecoveryComplexity.Simple);
        }

        [Fact]
        public void AssessTimeSensitivity_ShouldConsiderSeverityAndAffectedOperation()
        {
            // Arrange
            var catastrophicError = new ComponentError(
                _deviceId,
                "ANY_CODE",
                "Catastrophic error",
                ErrorSeverity.Catastrophic,
                false,
                ErrorSource.Device,
                false);

            var criticalError = new ComponentError(
                _deviceId,
                "ANY_CODE",
                "Critical error",
                ErrorSeverity.Critical,
                false,
                ErrorSource.Device,
                false);

            var sensingError = new ComponentError(
                _deviceId,
                ErrorCodes.Device.SENSOR_MALFUNCTION,
                "Sensing error",
                ErrorSeverity.Error,
                true,
                ErrorSource.Device,
                false);

            var communicationError = new ComponentError(
                _deviceId,
                ErrorCodes.Communication.CONNECTION_FAILED,
                "Communication error",
                ErrorSeverity.Error,
                true,
                ErrorSource.Communication,
                false);

            // Act & Assert
            ErrorTaxonomy.AssessTimeSensitivity(catastrophicError).Should().Be(ErrorTaxonomy.TimeSensitivity.Realtime);
            ErrorTaxonomy.AssessTimeSensitivity(criticalError).Should().Be(ErrorTaxonomy.TimeSensitivity.Critical);
            ErrorTaxonomy.AssessTimeSensitivity(sensingError).Should().Be(ErrorTaxonomy.TimeSensitivity.High);
            ErrorTaxonomy.AssessTimeSensitivity(communicationError).Should().Be(ErrorTaxonomy.TimeSensitivity.High);
        }

        [Fact]
        public void AssessDataIntegrity_ShouldReturnCorrectStatusBasedOnErrorCode()
        {
            // Act & Assert
            ErrorTaxonomy.AssessDataIntegrity(ErrorCodes.Storage.DATA_CORRUPTION)
                .Should().Be(ErrorTaxonomy.DataIntegrityStatus.RecoverableCorruption);

            ErrorTaxonomy.AssessDataIntegrity(ErrorCodes.Storage.TRANSACTION_FAILED)
                .Should().Be(ErrorTaxonomy.DataIntegrityStatus.PartialLoss);

            ErrorTaxonomy.AssessDataIntegrity(ErrorCodes.Storage.SERIALIZATION_ERROR)
                .Should().Be(ErrorTaxonomy.DataIntegrityStatus.RecoverableCorruption);

            ErrorTaxonomy.AssessDataIntegrity(ErrorCodes.Device.SENSOR_MALFUNCTION)
                .Should().Be(ErrorTaxonomy.DataIntegrityStatus.Intact);

            ErrorTaxonomy.AssessDataIntegrity(null)
                .Should().Be(ErrorTaxonomy.DataIntegrityStatus.Unknown);

            ErrorTaxonomy.AssessDataIntegrity(string.Empty)
                .Should().Be(ErrorTaxonomy.DataIntegrityStatus.Unknown);
        }

        [Fact]
        public void DetermineAffectedOperation_ShouldReturnCorrectOperationBasedOnErrorCode()
        {
            // Act & Assert
            ErrorTaxonomy.DetermineAffectedOperation(ErrorCodes.Communication.CONNECTION_FAILED)
                .Should().Be(ErrorTaxonomy.AffectedOperation.Communication);

            ErrorTaxonomy.DetermineAffectedOperation(ErrorCodes.Storage.READ_FAILED)
                .Should().Be(ErrorTaxonomy.AffectedOperation.Storage);

            ErrorTaxonomy.DetermineAffectedOperation(ErrorCodes.Event.HANDLER_EXCEPTION)
                .Should().Be(ErrorTaxonomy.AffectedOperation.DataProcessing);

            ErrorTaxonomy.DetermineAffectedOperation(ErrorCodes.Device.SENSOR_MALFUNCTION)
                .Should().Be(ErrorTaxonomy.AffectedOperation.Sensing);

            ErrorTaxonomy.DetermineAffectedOperation(ErrorCodes.Device.HARDWARE_FAILURE)
                .Should().Be(ErrorTaxonomy.AffectedOperation.Actuation);

            ErrorTaxonomy.DetermineAffectedOperation("DEVICE_SENSOR_CUSTOM")
                .Should().Be(ErrorTaxonomy.AffectedOperation.Sensing);

            ErrorTaxonomy.DetermineAffectedOperation(null)
                .Should().Be(ErrorTaxonomy.AffectedOperation.Unknown);

            ErrorTaxonomy.DetermineAffectedOperation(string.Empty)
                .Should().Be(ErrorTaxonomy.AffectedOperation.Unknown);
        }

        [Fact]
        public void CreateErrorProfile_ShouldIncludeAllTaxonomyDimensions()
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
            var profile = ErrorTaxonomy.CreateErrorProfile(error);

            // Assert
            profile.Should().ContainKey("RootCause");
            profile.Should().ContainKey("SystemImpact");
            profile.Should().ContainKey("RecoveryComplexity");
            profile.Should().ContainKey("DataIntegrityStatus");
            profile.Should().ContainKey("TimeSensitivity");
            profile.Should().ContainKey("AffectedOperation");
            profile.Should().ContainKey("ErrorCategory");

            profile["RootCause"].Should().Be(ErrorTaxonomy.RootCause.SensorMalfunction);
            profile["SystemImpact"].Should().Be(ErrorTaxonomy.SystemImpact.Partial);
            profile["RecoveryComplexity"].Should().Be(ErrorTaxonomy.RecoveryComplexity.Moderate);
            profile["DataIntegrityStatus"].Should().Be(ErrorTaxonomy.DataIntegrityStatus.Intact);
            profile["TimeSensitivity"].Should().Be(ErrorTaxonomy.TimeSensitivity.High);
            profile["AffectedOperation"].Should().Be(ErrorTaxonomy.AffectedOperation.Sensing);
            profile["ErrorCategory"].Should().Be(ErrorCategory.Device);
        }
    }
}
