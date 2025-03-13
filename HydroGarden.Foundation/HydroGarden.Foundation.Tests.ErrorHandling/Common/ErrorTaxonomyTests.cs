using FluentAssertions;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling.Taxonomy;
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
            ErrorTaxonomyExtensions.AnalyzeRootCause(ErrorCodes.Device.HARDWARE_FAILURE)
                .Should().Be(ErrorTaxonomy.RootCause.HardwareFailure);

            ErrorTaxonomyExtensions.AnalyzeRootCause(ErrorCodes.Device.SENSOR_MALFUNCTION)
                .Should().Be(ErrorTaxonomy.RootCause.SensorMalfunction);

            ErrorTaxonomyExtensions.AnalyzeRootCause(ErrorCodes.Communication.CONNECTION_FAILED)
                .Should().Be(ErrorTaxonomy.RootCause.NetworkFailure);

            ErrorTaxonomyExtensions.AnalyzeRootCause(ErrorCodes.Communication.TIMEOUT)
                .Should().Be(ErrorTaxonomy.RootCause.ConnectionTimeout);
        }

        [Fact]
        public void AnalyzeRootCause_WithNullOrEmptyErrorCode_ShouldReturnUnknown()
        {
            // Arrange & Act & Assert
            ErrorTaxonomyExtensions.AnalyzeRootCause(null).Should().Be(ErrorTaxonomy.RootCause.Unknown);
            ErrorTaxonomyExtensions.AnalyzeRootCause(string.Empty).Should().Be(ErrorTaxonomy.RootCause.Unknown);
        }

        [Fact]
        public void AnalyzeRootCause_WithUnrecognizedErrorCode_ShouldReturnUnknown()
        {
            // Arrange & Act & Assert
            ErrorTaxonomyExtensions.AnalyzeRootCause("UNRECOGNIZED_ERROR_CODE").Should().Be(ErrorTaxonomy.RootCause.Unknown);
        }

        [Fact]
        public void DetermineSystemImpact_ShouldConsiderSeverityAndErrorCode()
        {
            // Arrange & Act & Assert - Catastrophic severity always returns Complete impact
            ErrorTaxonomyExtensions.DetermineSystemImpact("ANY_CODE", ErrorSeverity.Catastrophic)
                .Should().Be(ErrorTaxonomyExtensions.SystemImpact.Complete);

            // Critical severity returns Significant impact
            ErrorTaxonomyExtensions.DetermineSystemImpact("ANY_CODE", ErrorSeverity.Critical)
                .Should().Be(ErrorTaxonomyExtensions.SystemImpact.Significant);

            // Hardware failure has Complete impact regardless of severity (except already tested Catastrophic)
            ErrorTaxonomyExtensions.DetermineSystemImpact(ErrorCodes.Device.HARDWARE_FAILURE, ErrorSeverity.Error)
                .Should().Be(ErrorTaxonomyExtensions.SystemImpact.Complete);

            // Connection failure has Significant impact
            ErrorTaxonomyExtensions.DetermineSystemImpact(ErrorCodes.Communication.CONNECTION_FAILED, ErrorSeverity.Error)
                .Should().Be(ErrorTaxonomyExtensions.SystemImpact.Significant);

            // Sensor malfunction has Partial impact
            ErrorTaxonomyExtensions.DetermineSystemImpact(ErrorCodes.Device.SENSOR_MALFUNCTION, ErrorSeverity.Error)
                .Should().Be(ErrorTaxonomyExtensions.SystemImpact.Partial);

            // Error severity with unrecognized code has Partial impact
            ErrorTaxonomyExtensions.DetermineSystemImpact("UNRECOGNIZED_CODE", ErrorSeverity.Error)
                .Should().Be(ErrorTaxonomyExtensions.SystemImpact.Partial);

            // Warning severity with unrecognized code has Minimal impact
            ErrorTaxonomyExtensions.DetermineSystemImpact("UNRECOGNIZED_CODE", ErrorSeverity.Warning)
                .Should().Be(ErrorTaxonomyExtensions.SystemImpact.Minimal);
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
            var complexity = ErrorTaxonomyExtensions.AssessRecoveryComplexity(error);

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
            ErrorTaxonomyExtensions.AssessRecoveryComplexity(hardwareFailureError).Should().Be(ErrorTaxonomy.RecoveryComplexity.Manual);
            ErrorTaxonomyExtensions.AssessRecoveryComplexity(configError).Should().Be(ErrorTaxonomy.RecoveryComplexity.Manual);
            ErrorTaxonomyExtensions.AssessRecoveryComplexity(networkError).Should().Be(ErrorTaxonomy.RecoveryComplexity.Moderate);
            ErrorTaxonomyExtensions.AssessRecoveryComplexity(timeoutError).Should().Be(ErrorTaxonomy.RecoveryComplexity.Simple);
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
            ErrorTaxonomyExtensions.AssessTimeSensitivity(catastrophicError).Should().Be(ErrorTaxonomyExtensions.TimeSensitivity.Realtime);
            ErrorTaxonomyExtensions.AssessTimeSensitivity(criticalError).Should().Be(ErrorTaxonomyExtensions.TimeSensitivity.Critical);
            ErrorTaxonomyExtensions.AssessTimeSensitivity(sensingError).Should().Be(ErrorTaxonomyExtensions.TimeSensitivity.High);
            ErrorTaxonomyExtensions.AssessTimeSensitivity(communicationError).Should().Be(ErrorTaxonomyExtensions.TimeSensitivity.High);
        }

        [Fact]
        public void AssessDataIntegrity_ShouldReturnCorrectStatusBasedOnErrorCode()
        {
            // Act & Assert
            ErrorTaxonomyExtensions.AssessDataIntegrity(ErrorCodes.Storage.DATA_CORRUPTION)
                .Should().Be(ErrorTaxonomyExtensions.DataIntegrityStatus.RecoverableCorruption);

            ErrorTaxonomyExtensions.AssessDataIntegrity(ErrorCodes.Storage.TRANSACTION_FAILED)
                .Should().Be(ErrorTaxonomyExtensions.DataIntegrityStatus.PartialLoss);

            ErrorTaxonomyExtensions.AssessDataIntegrity(ErrorCodes.Storage.SERIALIZATION_ERROR)
                .Should().Be(ErrorTaxonomyExtensions.DataIntegrityStatus.RecoverableCorruption);

            ErrorTaxonomyExtensions.AssessDataIntegrity(ErrorCodes.Device.SENSOR_MALFUNCTION)
                .Should().Be(ErrorTaxonomyExtensions.DataIntegrityStatus.Intact);

            ErrorTaxonomyExtensions.AssessDataIntegrity(null)
                .Should().Be(ErrorTaxonomyExtensions.DataIntegrityStatus.Unknown);

            ErrorTaxonomyExtensions.AssessDataIntegrity(string.Empty)
                .Should().Be(ErrorTaxonomyExtensions.DataIntegrityStatus.Unknown);
        }

        [Fact]
        public void DetermineAffectedOperation_ShouldReturnCorrectOperationBasedOnErrorCode()
        {
            // Act & Assert
            ErrorTaxonomyExtensions.DetermineAffectedOperation(ErrorCodes.Communication.CONNECTION_FAILED)
                .Should().Be(ErrorTaxonomyExtensions.AffectedOperation.Communication);

            ErrorTaxonomyExtensions.DetermineAffectedOperation(ErrorCodes.Storage.READ_FAILED)
                .Should().Be(ErrorTaxonomyExtensions.AffectedOperation.Storage);

            ErrorTaxonomyExtensions.DetermineAffectedOperation(ErrorCodes.Event.HANDLER_EXCEPTION)
                .Should().Be(ErrorTaxonomyExtensions.AffectedOperation.DataProcessing);

            ErrorTaxonomyExtensions.DetermineAffectedOperation(ErrorCodes.Device.SENSOR_MALFUNCTION)
                .Should().Be(ErrorTaxonomyExtensions.AffectedOperation.Sensing);

            ErrorTaxonomyExtensions.DetermineAffectedOperation(ErrorCodes.Device.HARDWARE_FAILURE)
                .Should().Be(ErrorTaxonomyExtensions.AffectedOperation.Actuation);

            ErrorTaxonomyExtensions.DetermineAffectedOperation("DEVICE_SENSOR_CUSTOM")
                .Should().Be(ErrorTaxonomyExtensions.AffectedOperation.Sensing);

            ErrorTaxonomyExtensions.DetermineAffectedOperation(null)
                .Should().Be(ErrorTaxonomyExtensions.AffectedOperation.Unknown);

            ErrorTaxonomyExtensions.DetermineAffectedOperation(string.Empty)
                .Should().Be(ErrorTaxonomyExtensions.AffectedOperation.Unknown);
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
            var profile = ErrorTaxonomyExtensions.CreateErrorProfile(error);

            // Assert
            profile.Should().ContainKey("RootCause");
            profile.Should().ContainKey("SystemImpact");
            profile.Should().ContainKey("RecoveryComplexity");
            profile.Should().ContainKey("DataIntegrityStatus");
            profile.Should().ContainKey("TimeSensitivity");
            profile.Should().ContainKey("AffectedOperation");
            profile.Should().ContainKey("ErrorCategory");

            profile["RootCause"].Should().Be(ErrorTaxonomy.RootCause.SensorMalfunction);
            profile["SystemImpact"].Should().Be(ErrorTaxonomyExtensions.SystemImpact.Partial);
            profile["RecoveryComplexity"].Should().Be(ErrorTaxonomy.RecoveryComplexity.Moderate);
            profile["DataIntegrityStatus"].Should().Be(ErrorTaxonomyExtensions.DataIntegrityStatus.Intact);
            profile["TimeSensitivity"].Should().Be(ErrorTaxonomyExtensions.TimeSensitivity.High);
            profile["AffectedOperation"].Should().Be(ErrorTaxonomyExtensions.AffectedOperation.Sensing);
            profile["ErrorCategory"].Should().Be(ErrorCategory.Device);
        }
    }
}
