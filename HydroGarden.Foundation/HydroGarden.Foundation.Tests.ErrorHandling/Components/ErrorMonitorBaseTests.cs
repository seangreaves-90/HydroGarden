using FluentAssertions;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.ErrorHandling;
using HydroGarden.Foundation.ErrorHandling.Common;
using HydroGarden.Logger.Abstractions;
using Moq;
using Xunit;

namespace HydroGarden.Foundation.Tests.ErrorHandling.Components
{
    /// <summary>
    /// Unit tests for the ErrorMonitorBase class.
    /// </summary>
    public class ErrorMonitorBaseTests
    {
        private readonly Guid _deviceId = Guid.NewGuid();
        private readonly string _errorCode = ErrorCodes.Device.SENSOR_MALFUNCTION;
        private readonly Mock<ILogger> _mockLogger;
        private readonly TestErrorMonitor _monitor;

        public ErrorMonitorBaseTests()
        {
            _mockLogger = new Mock<ILogger>();
            _monitor = new TestErrorMonitor(_mockLogger.Object);
        }

        [Fact]
        public async Task ReportErrorAsync_ShouldLogErrorAndAddToQueue()
        {
            // Arrange
            var error = new ComponentError(
                _deviceId,
                _errorCode,
                "Test error message",
                ErrorSeverity.Error,
                true,
                ErrorSource.Device,
                false);

            // Act
            await _monitor.ReportErrorAsync(error);

            // Assert
            _mockLogger.Verify(l => l.Log(error.Exception, It.Is<string>(s => 
                s.Contains(_errorCode) && s.Contains("Test error message"))));

            var recentErrors = await _monitor.GetRecentErrorsAsync();
            recentErrors.Should().ContainSingle();
            recentErrors[0].Should().BeSameAs(error);
        }

        [Fact]
        public async Task ReportErrorAsync_WithComponentError_ShouldTrackDetailedInfo()
        {
            // Arrange
            var error = new ComponentError(
                _deviceId,
                _errorCode,
                "Test error message",
                ErrorSeverity.Error,
                true,
                ErrorSource.Device,
                false);

            // Act
            await _monitor.ReportErrorAsync(error);

            // Assert
            var deviceErrors = await _monitor.GetActiveErrorsForDeviceAsync(_deviceId);
            deviceErrors.Should().ContainSingle();
            deviceErrors[0].Should().BeSameAs(error);
            
            var stats = await _monitor.GetErrorStatisticsAsync(DateTimeOffset.MinValue);
            stats.Should().ContainKey(_errorCode);
            stats[_errorCode].Should().Be(1);
        }

        [Fact]
        public async Task GetRecentErrorsAsync_WithMultipleErrors_ShouldReturnMostRecent()
        {
            // Arrange
            var errors = new List<IApplicationError>();
            for (int i = 0; i < 15; i++) // More than default count (10)
            {
                var error = new ComponentError(
                    _deviceId,
                    $"{_errorCode}_{i}",
                    $"Test error message {i}",
                    ErrorSeverity.Error,
                    true,
                    ErrorSource.Device,
                    false);
                
                errors.Add(error);
                await _monitor.ReportErrorAsync(error);
            }

            // Act - Get default count (10)
            var recentErrors = await _monitor.GetRecentErrorsAsync();

            // Assert - Should get the 10 most recent errors
            recentErrors.Should().HaveCount(10);
            for (int i = 5; i < 15; i++) // The last 10 errors
            {
                recentErrors.Should().Contain(errors[i]);
            }
        }

        [Fact]
        public async Task GetRecentErrorsAsync_WithSpecifiedCount_ShouldReturnRequestedNumber()
        {
            // Arrange
            var errors = new List<IApplicationError>();
            for (int i = 0; i < 5; i++)
            {
                var error = new ComponentError(
                    _deviceId,
                    $"{_errorCode}_{i}",
                    $"Test error message {i}",
                    ErrorSeverity.Error,
                    true,
                    ErrorSource.Device,
                    false);
                
                errors.Add(error);
                await _monitor.ReportErrorAsync(error);
            }

            // Act - Get specific count
            var recentErrors = await _monitor.GetRecentErrorsAsync(3);

            // Assert - Should get the 3 most recent errors
            recentErrors.Should().HaveCount(3);
            for (int i = 2; i < 5; i++) // The last 3 errors
            {
                recentErrors.Should().Contain(errors[i]);
            }
        }

        [Fact]
        public async Task ErrorQueue_ShouldRespectMaxSize()
        {
            // Arrange - Create a monitor with small max queue size
            var smallMonitor = new TestErrorMonitor(_mockLogger.Object, 3);

            // Act - Report more errors than the max size
            for (int i = 0; i < 5; i++)
            {
                var error = new ComponentError(
                    _deviceId,
                    $"{_errorCode}_{i}",
                    $"Test error message {i}",
                    ErrorSeverity.Error,
                    true,
                    ErrorSource.Device,
                    false);
                
                await smallMonitor.ReportErrorAsync(error);
            }

            // Assert - Should only keep the latest errors up to max size
            var recentErrors = await smallMonitor.GetRecentErrorsAsync(10);
            recentErrors.Should().HaveCount(3);
            
            // The queue should contain errors 2, 3, and 4 (the most recent 3)
            recentErrors.Should().AllSatisfy(e => 
                e.ErrorCode.Should().Match(code => 
                    code == $"{_errorCode}_2" || 
                    code == $"{_errorCode}_3" || 
                    code == $"{_errorCode}_4"));
        }

        [Fact]
        public async Task HasActiveErrorsAsync_WithNoErrors_ShouldReturnFalse()
        {
            // Act
            var hasErrors = await _monitor.HasActiveErrorsAsync();

            // Assert
            hasErrors.Should().BeFalse();
        }

        [Fact]
        public async Task HasActiveErrorsAsync_WithErrorsBelowSeverity_ShouldReturnFalse()
        {
            // Arrange
            var error = new ComponentError(
                _deviceId,
                _errorCode,
                "Test error message",
                ErrorSeverity.Warning, // Below default minimum severity (Error)
                true,
                ErrorSource.Device,
                false);
            
            await _monitor.ReportErrorAsync(error);

            // Act
            var hasErrors = await _monitor.HasActiveErrorsAsync(ErrorSeverity.Error);

            // Assert
            hasErrors.Should().BeFalse();
        }

        [Fact]
        public async Task HasActiveErrorsAsync_WithErrorsAboveSeverity_ShouldReturnTrue()
        {
            // Arrange
            var error = new ComponentError(
                _deviceId,
                _errorCode,
                "Test error message",
                ErrorSeverity.Critical, // Above default minimum severity (Error)
                true,
                ErrorSource.Device,
                false);
            
            await _monitor.ReportErrorAsync(error);

            // Act
            var hasErrors = await _monitor.HasActiveErrorsAsync();

            // Assert
            hasErrors.Should().BeTrue();
        }

        [Fact]
        public async Task GetActiveErrorsForDeviceAsync_WithNoErrors_ShouldReturnEmptyList()
        {
            // Act
            var errors = await _monitor.GetActiveErrorsForDeviceAsync(Guid.NewGuid());

            // Assert
            errors.Should().BeEmpty();
        }

        [Fact]
        public async Task GetActiveErrorsForDeviceAsync_WithMultipleDevices_ShouldReturnOnlyRequestedDevice()
        {
            // Arrange
            var deviceId1 = Guid.NewGuid();
            var deviceId2 = Guid.NewGuid();
            
            var error1 = new ComponentError(
                deviceId1,
                _errorCode,
                "Device 1 error",
                ErrorSeverity.Error,
                true,
                ErrorSource.Device,
                false);
            
            var error2 = new ComponentError(
                deviceId2,
                _errorCode,
                "Device 2 error",
                ErrorSeverity.Error,
                true,
                ErrorSource.Device,
                false);
            
            await _monitor.ReportErrorAsync(error1);
            await _monitor.ReportErrorAsync(error2);

            // Act
            var device1Errors = await _monitor.GetActiveErrorsForDeviceAsync(deviceId1);
            var device2Errors = await _monitor.GetActiveErrorsForDeviceAsync(deviceId2);

            // Assert
            device1Errors.Should().ContainSingle();
            device1Errors[0].Should().BeSameAs(error1);
            
            device2Errors.Should().ContainSingle();
            device2Errors[0].Should().BeSameAs(error2);
        }

        [Fact]
        public async Task MarkErrorHandledAsync_ShouldRemoveErrorFromDevice()
        {
            // Arrange
            var error = new ComponentError(
                _deviceId,
                _errorCode,
                "Test error message",
                ErrorSeverity.Error,
                true,
                ErrorSource.Device,
                false);
            
            await _monitor.ReportErrorAsync(error);
            
            // Verify error is tracked
            var beforeErrors = await _monitor.GetActiveErrorsForDeviceAsync(_deviceId);
            beforeErrors.Should().ContainSingle();

            // Act
            await _monitor.MarkErrorHandledAsync(_deviceId, _errorCode);

            // Assert
            var afterErrors = await _monitor.GetActiveErrorsForDeviceAsync(_deviceId);
            afterErrors.Should().BeEmpty();
        }

        [Fact]
        public async Task MarkErrorHandledAsync_WithMultipleErrors_ShouldRemoveOnlySpecifiedError()
        {
            // Arrange
            var errorCode1 = ErrorCodes.Device.SENSOR_MALFUNCTION;
            var errorCode2 = ErrorCodes.Device.COMMUNICATION_LOST;
            
            var error1 = new ComponentError(
                _deviceId,
                errorCode1,
                "Error 1",
                ErrorSeverity.Error,
                true,
                ErrorSource.Device,
                false);
            
            var error2 = new ComponentError(
                _deviceId,
                errorCode2,
                "Error 2",
                ErrorSeverity.Error,
                true,
                ErrorSource.Device,
                false);
            
            await _monitor.ReportErrorAsync(error1);
            await _monitor.ReportErrorAsync(error2);
            
            // Verify both errors are tracked
            var beforeErrors = await _monitor.GetActiveErrorsForDeviceAsync(_deviceId);
            beforeErrors.Should().HaveCount(2);

            // Act - Mark only one error as handled
            await _monitor.MarkErrorHandledAsync(_deviceId, errorCode1);

            // Assert - Only the specified error should be removed
            var afterErrors = await _monitor.GetActiveErrorsForDeviceAsync(_deviceId);
            afterErrors.Should().ContainSingle();
            afterErrors[0].ErrorCode.Should().Be(errorCode2);
        }

        [Fact]
        public async Task MarkErrorHandledAsync_WithUnknownDeviceOrErrorCode_ShouldNotFail()
        {
            // Act & Assert - Should not throw
            await _monitor.MarkErrorHandledAsync(Guid.NewGuid(), "UNKNOWN_ERROR_CODE");
        }

        [Fact]
        public async Task RegisterRecoveryAttemptAsync_WithSuccessfulRecovery_ShouldRemoveError()
        {
            // Arrange
            var error = new ComponentError(
                _deviceId,
                _errorCode,
                "Test error message",
                ErrorSeverity.Error,
                true,
                ErrorSource.Device,
                false);
            
            await _monitor.ReportErrorAsync(error);
            
            // Verify error is tracked
            var beforeErrors = await _monitor.GetActiveErrorsForDeviceAsync(_deviceId);
            beforeErrors.Should().ContainSingle();

            // Act - Register successful recovery
            await _monitor.RegisterRecoveryAttemptAsync(_deviceId, _errorCode, true);

            // Assert - Error should be removed
            var afterErrors = await _monitor.GetActiveErrorsForDeviceAsync(_deviceId);
            afterErrors.Should().BeEmpty();
        }

        [Fact]
        public async Task RegisterRecoveryAttemptAsync_WithFailedRecovery_ShouldRecordAttempt()
        {
            // Arrange
            var error = new ComponentError(
                _deviceId,
                _errorCode,
                "Test error message",
                ErrorSeverity.Error,
                true,
                ErrorSource.Device,
                false);
            
            await _monitor.ReportErrorAsync(error);
            
            // Initial recovery attempt count should be 0
            error.RecoveryAttemptCount.Should().Be(0);

            // Act - Register failed recovery
            await _monitor.RegisterRecoveryAttemptAsync(_deviceId, _errorCode, false);

            // Assert - Recovery attempt should be recorded
            error.RecoveryAttemptCount.Should().Be(1);
            
            // Error should still be active
            var afterErrors = await _monitor.GetActiveErrorsForDeviceAsync(_deviceId);
            afterErrors.Should().ContainSingle();
        }

        [Fact]
        public async Task GetErrorStatisticsAsync_ShouldTrackErrorCounts()
        {
            // Arrange
            var errorCode1 = ErrorCodes.Device.SENSOR_MALFUNCTION;
            var errorCode2 = ErrorCodes.Device.COMMUNICATION_LOST;
            
            // Report error1 twice and error2 once
            for (int i = 0; i < 2; i++)
            {
                var error = new ComponentError(
                    _deviceId,
                    errorCode1,
                    "Error 1",
                    ErrorSeverity.Error,
                    true,
                    ErrorSource.Device,
                    false);
                
                await _monitor.ReportErrorAsync(error);
            }
            
            var error2 = new ComponentError(
                _deviceId,
                errorCode2,
                "Error 2",
                ErrorSeverity.Error,
                true,
                ErrorSource.Device,
                false);
            
            await _monitor.ReportErrorAsync(error2);

            // Act
            var stats = await _monitor.GetErrorStatisticsAsync(DateTimeOffset.MinValue);

            // Assert
            stats.Should().ContainKey(errorCode1);
            stats.Should().ContainKey(errorCode2);
            stats[errorCode1].Should().Be(2);
            stats[errorCode2].Should().Be(1);
        }

        /// <summary>
        /// Test implementation of ErrorMonitorBase for testing purposes.
        /// </summary>
        private class TestErrorMonitor : ErrorMonitorBase, IErrorMonitor
        {
            public TestErrorMonitor(ILogger logger, int maxErrorQueueSize = 1000) 
                : base(logger, maxErrorQueueSize)
            {
            }

            public Task<IReadOnlyList<IApplicationError>> GetErrorsByCorrelationIdAsync(Guid correlationId, CancellationToken ct = default)
            {
                var result = ErrorQueue
                    .Where(e => e.CorrelationId == correlationId)
                    .ToList();
                
                return Task.FromResult<IReadOnlyList<IApplicationError>>(result);
            }

            public Task<IReadOnlyList<IApplicationError>> GetErrorsByCodeAsync(string errorCode, DateTimeOffset since, CancellationToken ct = default)
            {
                var result = ErrorQueue
                    .Where(e => e.ErrorCode == errorCode && e.Timestamp >= since)
                    .ToList();
                
                return Task.FromResult<IReadOnlyList<IApplicationError>>(result);
            }

            public Task<Guid> SubscribeToErrorsAsync(Func<IApplicationError, Task> handler, Func<IApplicationError, bool>? filter = null, CancellationToken ct = default)
            {
                // Not implemented in the test monitor
                return Task.FromResult(Guid.NewGuid());
            }

            public Task UnsubscribeFromErrorsAsync(Guid subscriptionId, CancellationToken ct = default)
            {
                // Not implemented in the test monitor
                return Task.CompletedTask;
            }
        }
    }
}
