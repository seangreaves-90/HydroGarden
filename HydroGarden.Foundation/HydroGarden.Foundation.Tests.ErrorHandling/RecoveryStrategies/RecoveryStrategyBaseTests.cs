using FluentAssertions;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.ErrorHandling;
using HydroGarden.Foundation.ErrorHandling.Common;
using HydroGarden.Foundation.ErrorHandling.RecoveryStrategy;
using HydroGarden.Foundation.Tests.ErrorHandling.Mocks;
using HydroGarden.Logger.Abstractions;
using Moq;
using Xunit;

namespace HydroGarden.Foundation.Tests.ErrorHandling.RecoveryStrategies
{
    /// <summary>
    /// Unit tests for the RecoveryStrategyBase class.
    /// </summary>
    public class RecoveryStrategyBaseTests
    {
        private readonly Guid _deviceId = Guid.NewGuid();
        private readonly Mock<ILogger> _mockLogger;
        private readonly TestRecoveryStrategy _strategy;

        public RecoveryStrategyBaseTests()
        {
            _mockLogger = new Mock<ILogger>();
            _strategy = new TestRecoveryStrategy(_mockLogger.Object);
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => new TestRecoveryStrategy(null!));
            exception.ParamName.Should().Be("logger");
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
        public void CanRecover_WithUnrecoverableError_ShouldReturnFalse()
        {
            // Arrange
            var error = ComponentError.CreateNonRecoverable(
                _deviceId,
                ErrorCodes.Device.HARDWARE_FAILURE,
                "Unrecoverable hardware failure");

            // Act
            var canRecover = _strategy.CanRecover(error);

            // Assert
            canRecover.Should().BeFalse();
        }

        [Fact]
        public void CanRecover_WithSupportedRootCause_ShouldReturnTrue()
        {
            // Arrange
            _strategy.TestSupportedRootCauses = new[] { ErrorTaxonomy.RootCause.NetworkFailure };
            var error = new ComponentError(
                _deviceId,
                ErrorCodes.Communication.CONNECTION_FAILED, // Maps to NetworkFailure
                "Connection failed",
                ErrorSeverity.Error,
                true,
                ErrorSource.Communication,
                true);

            // Act
            var canRecover = _strategy.CanRecover(error);

            // Assert
            canRecover.Should().BeTrue();
        }

        [Fact]
        public void CanRecover_WithUnsupportedRootCause_ShouldReturnFalse()
        {
            // Arrange
            _strategy.TestSupportedRootCauses = new[] { ErrorTaxonomy.RootCause.NetworkFailure };
            var error = new ComponentError(
                _deviceId,
                ErrorCodes.Device.HARDWARE_FAILURE, // Maps to HardwareFailure
                "Hardware failure",
                ErrorSeverity.Critical,
                false,
                ErrorSource.Device,
                false);

            // Act
            var canRecover = _strategy.CanRecover(error);

            // Assert
            canRecover.Should().BeFalse();
        }

        [Fact]
        public void CanRecover_WithUnknownInSupportedRootCauses_ShouldReturnTrue()
        {
            // Arrange
            _strategy.TestSupportedRootCauses = new[] { ErrorTaxonomy.RootCause.Unknown };
            var error = new ComponentError(
                _deviceId,
                "UNKNOWN_ERROR_CODE", // Will map to Unknown root cause
                "Unknown error",
                ErrorSeverity.Error,
                true,
                ErrorSource.Unknown,
                false);

            // Act
            var canRecover = _strategy.CanRecover(error);

            // Assert
            canRecover.Should().BeTrue();
        }

        [Fact]
        public async Task AttemptRecoveryAsync_WhenCannotRecover_ShouldReturnFalse()
        {
            // Arrange
            _strategy.TestSupportedRootCauses = new[] { ErrorTaxonomy.RootCause.NetworkFailure };
            var error = new ComponentError(
                _deviceId,
                ErrorCodes.Device.HARDWARE_FAILURE, // Not supported
                "Hardware failure",
                ErrorSeverity.Critical,
                false,
                ErrorSource.Device,
                false);

            // Act
            var result = await _strategy.AttemptRecoveryAsync(error);

            // Assert
            result.Should().BeFalse();
            _mockLogger.Verify(l => l.Log(It.Is<string>(s => 
                s.Contains("cannot recover") && s.Contains(_strategy.Name))));
            _strategy.ExecuteRecoveryCalled.Should().BeFalse();
        }

        [Fact]
        public async Task AttemptRecoveryAsync_WithErrorThatCannotAttemptRecovery_ShouldReturnFalse()
        {
            // Arrange
            _strategy.TestSupportedRootCauses = new[] { ErrorTaxonomy.RootCause.ConnectionTimeout };
            var error = ComponentError.CreateTransient(
                _deviceId,
                ErrorCodes.Communication.TIMEOUT,
                "Connection timeout");

            // Make error exceed max recovery attempts
            for (int i = 0; i < error.MaxRecoveryAttempts; i++)
            {
                error.RecordRecoveryAttempt();
            }

            // Act
            var result = await _strategy.AttemptRecoveryAsync(error);

            // Assert
            result.Should().BeFalse();
            _mockLogger.Verify(l => l.Log(It.Is<string>(s => 
                s.Contains("Cannot attempt recovery") && s.Contains("backoff period"))));
            _strategy.ExecuteRecoveryCalled.Should().BeFalse();
        }

        [Fact]
        public async Task AttemptRecoveryAsync_WithValidError_ShouldExecuteRecovery()
        {
            // Arrange
            _strategy.TestSupportedRootCauses = new[] { ErrorTaxonomy.RootCause.ConnectionTimeout };
            _strategy.TestExecuteRecoveryResult = true;
            var error = ComponentError.CreateTransient(
                _deviceId,
                ErrorCodes.Communication.TIMEOUT,
                "Connection timeout");

            // Act
            var result = await _strategy.AttemptRecoveryAsync(error);

            // Assert
            result.Should().BeTrue();
            _strategy.ExecuteRecoveryCalled.Should().BeTrue();
            _strategy.ErrorPassedToExecuteRecovery.Should().BeSameAs(error);
            _mockLogger.Verify(l => l.Log(It.Is<string>(s => 
                s.Contains("Attempting recovery") && s.Contains(_strategy.Name))));
            _mockLogger.Verify(l => l.Log(It.Is<string>(s => 
                s.Contains("Recovery successful") && s.Contains(_strategy.Name))));
        }

        [Fact]
        public async Task AttemptRecoveryAsync_WhenExecuteRecoveryFails_ShouldReturnFalse()
        {
            // Arrange
            _strategy.TestSupportedRootCauses = new[] { ErrorTaxonomy.RootCause.ConnectionTimeout };
            _strategy.TestExecuteRecoveryResult = false;
            var error = ComponentError.CreateTransient(
                _deviceId,
                ErrorCodes.Communication.TIMEOUT,
                "Connection timeout");

            // Act
            var result = await _strategy.AttemptRecoveryAsync(error);

            // Assert
            result.Should().BeFalse();
            _strategy.ExecuteRecoveryCalled.Should().BeTrue();
            _mockLogger.Verify(l => l.Log(It.Is<string>(s => 
                s.Contains("Recovery failed") && s.Contains(_strategy.Name))));
        }

        [Fact]
        public async Task AttemptRecoveryAsync_WhenExecuteRecoveryThrowsException_ShouldCatchAndReturnFalse()
        {
            // Arrange
            _strategy.TestSupportedRootCauses = new[] { ErrorTaxonomy.RootCause.ConnectionTimeout };
            _strategy.TestThrowException = true;
            var error = ComponentError.CreateTransient(
                _deviceId,
                ErrorCodes.Communication.TIMEOUT,
                "Connection timeout");

            // Act
            var result = await _strategy.AttemptRecoveryAsync(error);

            // Assert
            result.Should().BeFalse();
            _strategy.ExecuteRecoveryCalled.Should().BeTrue();
            _mockLogger.Verify(l => l.Log(It.IsAny<Exception>(), It.Is<string>(s => 
                s.Contains("Exception during recovery") && s.Contains(_strategy.Name))));
        }

        [Fact]
        public async Task AttemptRecoveryAsync_WithMultipleAttempts_ShouldTrackAttemptCount()
        {
            // Arrange
            _strategy.TestSupportedRootCauses = new[] { ErrorTaxonomy.RootCause.ConnectionTimeout };
            _strategy.TestExecuteRecoveryResult = false; // Make it fail to test multiple attempts
            var error = ComponentError.CreateTransient(
                _deviceId,
                ErrorCodes.Communication.TIMEOUT,
                "Connection timeout");

            // Act - Make first attempt, which should fail
            await _strategy.AttemptRecoveryAsync(error);
            
            // Assert initial error information
            error.RecoveryAttemptCount.Should().Be(1);
            error.LastRecoveryAttempt.Should().NotBeNull();
            
            // Act - Make second attempt immediately, should respect backoff
            var secondResult = await _strategy.AttemptRecoveryAsync(error);
            
            // Assert
            secondResult.Should().BeFalse();
            _mockLogger.Verify(l => l.Log(It.Is<string>(s => 
                s.Contains("Backoff period not elapsed"))));
        }

        [Fact]
        public async Task AttemptRecoveryAsync_WithMaxAttemptsExceeded_ShouldReturnFalse()
        {
            // Arrange
            _strategy.TestSupportedRootCauses = new[] { ErrorTaxonomy.RootCause.ConnectionTimeout };
            _strategy.TestExecuteRecoveryResult = false; // Make it fail
            var error = ComponentError.CreateTransient(
                _deviceId,
                ErrorCodes.Communication.TIMEOUT,
                "Connection timeout");

            // Simulate the strategy already having max attempts for this device ID
            await SimulateMaxAttempts(error);

            // Act
            var result = await _strategy.AttemptRecoveryAsync(error);

            // Assert
            result.Should().BeFalse();
            _mockLogger.Verify(l => l.Log(It.Is<string>(s => 
                s.Contains("Max recovery attempts"))));
        }

        /// <summary>
        /// Simulate reaching max attempts by forcibly registering attempts in the strategy
        /// </summary>
        private async Task SimulateMaxAttempts(IApplicationError error)
        {
            // We need to make the recovery succeed once to reset the attempt counter, 
            // then make it fail for the remaining attempts
            _strategy.TestExecuteRecoveryResult = true;
            await _strategy.AttemptRecoveryAsync(error);
            
            _strategy.TestExecuteRecoveryResult = false;
            for (int i = 0; i < _strategy.TestMaxRecoveryAttempts; i++)
            {
                // Force the backoff period to be considered elapsed by manipulating the last recovery timestamp
                var field = typeof(TestRecoveryStrategy)
                    .GetField("_deviceRecoveryStatus", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var dict = field!.GetValue(_strategy) as Dictionary<Guid, object>;
                var status = dict![error.DeviceId];
                var lastAttemptProp = status.GetType().GetProperty("LastAttempt");
                lastAttemptProp!.SetValue(status, DateTimeOffset.UtcNow.AddHours(-1));
                
                await _strategy.AttemptRecoveryAsync(error);
            }
        }

        /// <summary>
        /// Test implementation of RecoveryStrategyBase for testing.
        /// </summary>
        private class TestRecoveryStrategy : RecoveryStrategyBase
        {
            public TestRecoveryStrategy(ILogger logger) : base(logger) { }

            public override string Name => "TestRecoveryStrategy";
            
            public int TestMaxRecoveryAttempts => MaxRecoveryAttempts;
            
            public ErrorTaxonomy.RootCause[] TestSupportedRootCauses { get; set; } = 
                new[] { ErrorTaxonomy.RootCause.Unknown };
            
            public override ErrorTaxonomy.RootCause[] SupportedRootCauses => TestSupportedRootCauses;
            
            public bool TestExecuteRecoveryResult { get; set; } = true;
            public bool TestThrowException { get; set; } = false;
            public bool ExecuteRecoveryCalled { get; private set; } = false;
            public IApplicationError? ErrorPassedToExecuteRecovery { get; private set; }

            protected override Task<bool> ExecuteRecoveryAsync(IApplicationError? error, CancellationToken ct)
            {
                ExecuteRecoveryCalled = true;
                ErrorPassedToExecuteRecovery = error;
                
                if (TestThrowException)
                {
                    throw new InvalidOperationException("Test exception");
                }
                
                return Task.FromResult(TestExecuteRecoveryResult);
            }
        }
    }
}
