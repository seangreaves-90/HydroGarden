using System.Collections.Concurrent;
using System.Text.Json;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.Abstractions.Interfaces.Logging;
using HydroGarden.Foundation.Common.ErrorHandling;
using HydroGarden.Foundation.Common.ErrorHandling.Constants;
using Microsoft.Extensions.Hosting;

namespace HydroGarden.Foundation.Core.Services
{
    /// <summary>
    /// Handles unrecoverable errors that require external intervention.
    /// </summary>
    public class UnrecoverableErrorHandler : IHostedService, IDisposable
    {
        private readonly IErrorMonitor _errorMonitor;
        private readonly ILogger _logger;
        private Guid _errorSubscriptionId;
        private readonly ConcurrentDictionary<string, DateTimeOffset> _notificationHistory = new();
        private readonly TimeSpan _notificationThrottleTime = TimeSpan.FromHours(4);
        private readonly SemaphoreSlim _notificationLock = new(1, 1);
        private readonly CancellationTokenSource _cts = new();
        private bool _isDisposed;

        /// <summary>
        /// Creates a new unrecoverable error handler.
        /// </summary>
        public UnrecoverableErrorHandler(IErrorMonitor errorMonitor, ILogger logger)
        {
            _errorMonitor = errorMonitor ?? throw new ArgumentNullException(nameof(errorMonitor));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Starts monitoring for unrecoverable errors.
        /// </summary>
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.Log("Unrecoverable error handler started");

            // Subscribe to error events
            _errorSubscriptionId = await _errorMonitor.SubscribeToErrorsAsync(
                HandleErrorAsync,
                IsUnrecoverableError,
                cancellationToken);

            // Start periodic check for existing unrecoverable errors
            _ = Task.Run(() => CheckPeriodicUnrecoverableErrors(_cts.Token), cancellationToken);
        }

        /// <summary>
        /// Stops monitoring for unrecoverable errors.
        /// </summary>
        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _cts.Cancel();

            await _errorMonitor.UnsubscribeFromErrorsAsync(_errorSubscriptionId, cancellationToken);

            _logger.Log("Unrecoverable error handler stopped");
        }

        /// <summary>
        /// Handles an error event from the monitor.
        /// </summary>
        private async Task HandleErrorAsync(IApplicationError error)
        {
            if (IsUnrecoverableError(error))
            {
                await NotifyUnrecoverableErrorAsync(error);
            }
        }

        /// <summary>
        /// Determines if an error is unrecoverable.
        /// </summary>
        private bool IsUnrecoverableError(IApplicationError error)
        {
            // Check if it's specifically marked as unrecoverable
            if (error is ComponentError { IsUnrecoverable: true })
            {
                return true;
            }

            // Check error code
            if (error.ErrorCode != null && ErrorCodes.IsUnrecoverable(error.ErrorCode))
            {
                return true;
            }

            // Check severity
            if (error.Severity >= ErrorSeverity.Critical)
            {
                return true;
            }

            // Attempt limit exceeded
            if (error.ErrorCode == ErrorCodes.Recovery.ATTEMPT_LIMIT_REACHED)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Notify about an unrecoverable error.
        /// </summary>
        private async Task NotifyUnrecoverableErrorAsync(IApplicationError error)
        {
            // Generate a unique key for this error type
            string errorKey = GenerateErrorKey(error);

            // Check if we've recently notified about this error
            if (!ShouldNotify(errorKey))
            {
                _logger.Log($"Throttling notification for error {errorKey}");
                return;
            }

            await _notificationLock.WaitAsync();
            try
            {
                // Double-check after acquiring lock
                if (!ShouldNotify(errorKey))
                {
                    return;
                }

                _logger.Log($"UNRECOVERABLE ERROR: {error.ErrorCode} - {error.Message}");
                _logger.Log($"Device: {error.DeviceId}, Severity: {error.Severity}");

                if (error.Context.Count > 0)
                {
                    _logger.Log($"Context: {JsonSerializer.Serialize(error.Context)}");
                }

                if (error.Exception != null)
                {
                    _logger.Log($"Exception: {error.Exception.GetType().Name} - {error.Exception.Message}");
                    _logger.Log($"Stack trace: {error.Exception.StackTrace}");
                }

                _logger.Log("THIS ERROR REQUIRES MANUAL INTERVENTION");

                // TODO: Send email notification when that feature is implemented

                // Update notification history
                _notificationHistory[errorKey] = DateTimeOffset.UtcNow;
            }
            finally
            {
                _notificationLock.Release();
            }
        }

        /// <summary>
        /// Checks if we should send a notification for this error.
        /// </summary>
        private bool ShouldNotify(string errorKey)
        {
            // Check if we've recently notified about this error
            if (_notificationHistory.TryGetValue(errorKey, out var lastNotification))
            {
                // Only notify again after the throttle time
                return (DateTimeOffset.UtcNow - lastNotification) > _notificationThrottleTime;
            }

            // No recent notification, so we should notify
            return true;
        }

        /// <summary>
        /// Generates a unique key for an error to prevent duplicate notifications.
        /// </summary>
        private string GenerateErrorKey(IApplicationError error)
        {
            return $"{error.DeviceId}:{error.ErrorCode ?? "UNKNOWN"}";
        }

        /// <summary>
        /// Periodically checks for unrecoverable errors already in the system.
        /// </summary>
        private async Task CheckPeriodicUnrecoverableErrors(CancellationToken ct)
        {
            // Check every hour
            var checkInterval = TimeSpan.FromHours(1);

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    // Get recent errors
                    var recentErrors = await _errorMonitor.GetRecentErrorsAsync(100, ct);

                    // Find unrecoverable errors
                    var unrecoverableErrors = recentErrors
                        .Where(IsUnrecoverable)
                        .ToList();

                    if (unrecoverableErrors.Count > 0)
                    {
                        _logger.Log($"Found {unrecoverableErrors.Count} unrecoverable errors during periodic check");

                        foreach (var error in unrecoverableErrors)
                        {
                            await NotifyUnrecoverableErrorAsync(error);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Log(ex, "Error during periodic unrecoverable error check");
                }

                try
                {
                    await Task.Delay(checkInterval, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Checks if an error should be considered unrecoverable.
        /// </summary>
        private bool IsUnrecoverable(IApplicationError error)
        {
            // Critical severity
            if (error.Severity >= ErrorSeverity.Critical)
                return true;

            // Recovery limit reached
            if (error.ErrorCode == ErrorCodes.Recovery.ATTEMPT_LIMIT_REACHED)
                return true;

            // Hardware failure
            if (error.ErrorCode == ErrorCodes.Device.HARDWARE_FAILURE)
                return true;

            // Invalid configuration
            if (error.ErrorCode == ErrorCodes.Device.CONFIGURATION_INVALID)
                return true;

            // Dependency unrecoverable
            if (error.ErrorCode == ErrorCodes.Recovery.DEPENDENCY_UNRECOVERABLE)
                return true;

            // Data corruption
            if (error.ErrorCode == ErrorCodes.Storage.DATA_CORRUPTION)
                return true;

            // ComponentError.IsUnrecoverable
            return error is ComponentError { IsUnrecoverable: true };
        }

        /// <summary>
        /// Disposes resources.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;

            _cts.Cancel();
            _cts.Dispose();
            _notificationLock.Dispose();

            _isDisposed = true;
            GC.SuppressFinalize(this);
        }
    }
}