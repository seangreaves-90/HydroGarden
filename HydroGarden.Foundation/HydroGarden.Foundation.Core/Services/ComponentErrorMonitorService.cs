
using System.Collections.Concurrent;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.Abstractions.Interfaces.Logging;
using HydroGarden.Foundation.Common.ErrorHandling;
using HydroGarden.Foundation.Common.Extensions;

namespace HydroGarden.Foundation.Core.Services
{
    /// <summary>
    /// Monitors and manages component errors throughout the system.
    /// </summary>
    /// <remarks>
    /// Creates a new component error monitor.
    /// </remarks>
    public class ComponentErrorMonitorService(ILogger logger, int maxErrorQueueSize = 1000) : IErrorMonitor, IDisposable
    {
        private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private readonly int _maxErrorQueueSize = maxErrorQueueSize;
        private readonly ConcurrentQueue<IApplicationError> _errorQueue = new();
        private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, ComponentError>> _deviceErrors = new();
        private readonly ConcurrentDictionary<string, int> _errorStatistics = new();
        private readonly ConcurrentDictionary<Guid, List<IApplicationError>> _errorsByCorrelation = new();
        private readonly ConcurrentDictionary<Guid, Func<IApplicationError, Task>> _subscriptions = new();
        private readonly ConcurrentDictionary<Guid, Func<IApplicationError, bool>> _subscriptionFilters = new();
        private readonly SemaphoreSlim _statisticsLock = new(1, 1);
        private bool _isDisposed;

        /// <summary>
        /// Reports an error to the error monitor.
        /// </summary>
        public async Task ReportErrorAsync(IApplicationError error, CancellationToken ct = default)
        {
            if (error == null)
                throw new ArgumentNullException(nameof(error));

            await this.ExecuteWithErrorHandlingAsync(
                this,
                async () =>
                {
                    // Log the error
                    _logger.Log(error.Exception,
                        $"[{error.Severity}] [{error.ErrorCode}] {error.Message} - DeviceId: {error.DeviceId}");

                    // Add to error queue
                    _errorQueue.Enqueue(error);

                    // Ensure queue doesn't exceed max size
                    while (_errorQueue.Count > _maxErrorQueueSize && _errorQueue.TryDequeue(out _)) { }

                    // Track error by device and code if it's a ComponentError
                    if (error is ComponentError componentError && error.ErrorCode != null)
                    {
                        TrackComponentError(componentError);

                        // Update statistics
                        await _statisticsLock.WaitAsync(ct);
                        try
                        {
                            _errorStatistics.AddOrUpdate(
                                error.ErrorCode,
                                1,
                                (_, count) => count + 1);
                        }
                        finally
                        {
                            _statisticsLock.Release();
                        }

                        // Track by correlation ID for related errors
                        if (!_errorsByCorrelation.TryGetValue(error.CorrelationId, out var correlatedErrors))
                        {
                            correlatedErrors = new List<IApplicationError>();
                            _errorsByCorrelation[error.CorrelationId] = correlatedErrors;
                        }

                        lock (correlatedErrors)
                        {
                            correlatedErrors.Add(error);
                        }
                    }

                    // Notify subscribers
                    foreach (var (subscriptionId, handler) in _subscriptions)
                    {
                        if (_subscriptionFilters.TryGetValue(subscriptionId, out var filter) &&
                            (filter(error)))
                        {
                            try
                            {
                                await handler(error);
                            }
                            catch (Exception ex)
                            {
                                _logger.Log(ex, $"Error notifying subscriber {subscriptionId} about error {error.ErrorCode}");
                            }
                        }
                    }

                    return Task.CompletedTask;
                },
                "ERROR_MONITOR_FAILED",
                $"Failed to process error {error.ErrorCode}",
                ErrorSource.Service, ct: ct);
        }

        /// <summary>
        /// Gets the most recent errors.
        /// </summary>
        public Task<IReadOnlyList<IApplicationError>> GetRecentErrorsAsync(int count = 10, CancellationToken ct = default)
        {
            var result = _errorQueue.TakeLast(Math.Min(count, _errorQueue.Count)).ToList();
            return Task.FromResult<IReadOnlyList<IApplicationError>>(result);
        }

        /// <summary>
        /// Checks if there are active errors above the specified severity.
        /// </summary>
        public Task<bool> HasActiveErrorsAsync(ErrorSeverity minimumSeverity = ErrorSeverity.Error, CancellationToken ct = default)
        {
            return Task.FromResult(_errorQueue.Any(e => e.Severity >= minimumSeverity));
        }

        /// <summary>
        /// Gets all active errors for a specific device.
        /// </summary>
        public Task<IReadOnlyList<IApplicationError>> GetActiveErrorsForDeviceAsync(
            Guid deviceId,
            CancellationToken ct = default)
        {
            if (_deviceErrors.TryGetValue(deviceId, out var deviceErrorMap))
            {
                return Task.FromResult<IReadOnlyList<IApplicationError>>(
                    deviceErrorMap.Values.Cast<IApplicationError>().ToList().AsReadOnly());
            }

            return Task.FromResult<IReadOnlyList<IApplicationError>>(
                new List<IApplicationError>().AsReadOnly());
        }

        /// <summary>
        /// Marks an error as handled.
        /// </summary>
        public Task MarkErrorHandledAsync(
            Guid deviceId,
            string errorCode,
            CancellationToken ct = default)
        {
            if (_deviceErrors.TryGetValue(deviceId, out var deviceErrorMap))
            {
                deviceErrorMap.TryRemove(errorCode, out _);

                if (deviceErrorMap.IsEmpty)
                {
                    _deviceErrors.TryRemove(deviceId, out _);
                }
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Gets error statistics since a specific time.
        /// </summary>
        public Task<IDictionary<string, int>> GetErrorStatisticsAsync(
            DateTimeOffset since,
            CancellationToken ct = default)
        {
            var filteredErrors = _errorQueue.Where(e => e.Timestamp >= since);

            var statistics = new Dictionary<string, int>();

            foreach (var errorCode in filteredErrors
                .Where(e => e.ErrorCode != null)
                .Select(e => e.ErrorCode!)
                .Distinct())
            {
                statistics[errorCode] = filteredErrors.Count(e => e.ErrorCode == errorCode);
            }

            return Task.FromResult<IDictionary<string, int>>(statistics);
        }

        /// <summary>
        /// Registers a recovery attempt for an error.
        /// </summary>
        public Task RegisterRecoveryAttemptAsync(
            Guid deviceId,
            string errorCode,
            bool successful,
            CancellationToken ct = default)
        {
            if (_deviceErrors.TryGetValue(deviceId, out var deviceErrorMap) &&
                deviceErrorMap.TryGetValue(errorCode, out var error))
            {
                if (successful)
                {
                    deviceErrorMap.TryRemove(errorCode, out _);

                    if (deviceErrorMap.IsEmpty)
                    {
                        _deviceErrors.TryRemove(deviceId, out _);
                    }

                    _logger.Log($"Successful recovery for device {deviceId}, error {errorCode}");
                }
                else
                {
                    error.RecordRecoveryAttempt();
                    _logger.Log($"Failed recovery attempt for device {deviceId}, error {errorCode} " +
                               $"(attempt {error.RecoveryAttemptCount})");
                }
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Gets errors by correlation ID.
        /// </summary>
        public Task<IReadOnlyList<IApplicationError>> GetErrorsByCorrelationIdAsync(
            Guid correlationId, CancellationToken ct = default)
        {
            if (_errorsByCorrelation.TryGetValue(correlationId, out var errors))
            {
                return Task.FromResult<IReadOnlyList<IApplicationError>>(errors.AsReadOnly());
            }

            return Task.FromResult<IReadOnlyList<IApplicationError>>(
                new List<IApplicationError>().AsReadOnly());
        }

        /// <summary>
        /// Gets errors by error code.
        /// </summary>
        public Task<IReadOnlyList<IApplicationError>> GetErrorsByCodeAsync(
            string errorCode, DateTimeOffset since, CancellationToken ct = default)
        {
            var result = _errorQueue
                .Where(e => e.ErrorCode == errorCode && e.Timestamp >= since)
                .ToList();

            return Task.FromResult<IReadOnlyList<IApplicationError>>(result.AsReadOnly());
        }

        /// <summary>
        /// Subscribes to error notifications.
        /// </summary>
        public Task<Guid> SubscribeToErrorsAsync(
            Func<IApplicationError, Task> handler,
            Func<IApplicationError, bool>? filter = null,
            CancellationToken ct = default)
        {
            var subscriptionId = Guid.NewGuid();
            _subscriptions[subscriptionId] = handler ?? throw new ArgumentNullException(nameof(handler));

            if (filter != null)
            {
                _subscriptionFilters[subscriptionId] = filter;
            }

            _logger.Log($"New error subscription registered with ID {subscriptionId}");
            return Task.FromResult(subscriptionId);
        }

        /// <summary>
        /// Unsubscribes from error notifications.
        /// </summary>
        public Task UnsubscribeFromErrorsAsync(Guid subscriptionId, CancellationToken ct = default)
        {
            _subscriptions.TryRemove(subscriptionId, out _);
            _subscriptionFilters.TryRemove(subscriptionId, out _);

            _logger.Log($"Subscription {subscriptionId} removed");
            return Task.CompletedTask;
        }

        /// <summary>
        /// Tracks a component error by device ID and error code.
        /// </summary>
        private void TrackComponentError(ComponentError error)
        {
            if (string.IsNullOrEmpty(error.ErrorCode))
                return;

            var deviceErrorMap = _deviceErrors.GetOrAdd(
                error.DeviceId,
                _ => new ConcurrentDictionary<string, ComponentError>());

            deviceErrorMap[error.ErrorCode] = error;
        }

        /// <summary>
        /// Reports an exception directly.
        /// </summary>
        public Task ReportExceptionAsync(
            object source,
            Exception exception,
            string errorCode,
            string errorMessage,
            ErrorSeverity severity = ErrorSeverity.Error,
            ErrorSource errorSource = ErrorSource.Unknown,
            IDictionary<string, object>? additionalContext = null,
            CancellationToken ct = default)
        {
            var context = additionalContext ?? new Dictionary<string, object>();

            if (source is IApplicationError)
            {
                // Prevent potential infinite recursion if reporting an error from the error monitor
                _logger.Log(exception, $"[CRITICAL] Error in error monitor: {errorMessage}");
                return Task.CompletedTask;
            }

            // Add source context
            var sourceType = source.GetType();
            context["SourceType"] = sourceType.FullName ?? "UnknownType";

            // Create the error
            var error = new ComponentError(
                Guid.Empty, // No device ID for general exceptions
                errorCode,
                errorMessage,
                severity,
                severity < ErrorSeverity.Critical,
                errorSource,
                severity < ErrorSeverity.Error,
                context,
                exception);

            return ReportErrorAsync(error, ct);
        }

        /// <summary>
        /// Analyzes error patterns for reporting.
        /// </summary>
        public Dictionary<string, int> AnalyzeErrorPatterns()
        {
            var results = new Dictionary<string, int>();

            // Add error frequency by code
            foreach (var (errorCode, count) in _errorStatistics)
            {
                results[$"ErrorCode_{errorCode}"] = count;
            }

            // Add error count by device
            foreach (var (deviceId, errorMap) in _deviceErrors)
            {
                results[$"Device_{deviceId}_ErrorCount"] = errorMap.Count;

                // Count by severity
                var severityCounts = errorMap.Values
                    .GroupBy(e => e.Severity)
                    .ToDictionary(g => g.Key, g => g.Count());

                foreach (var (severity, count) in severityCounts)
                {
                    results[$"Device_{deviceId}_{severity}Count"] = count;
                }
            }

            return results;
        }

        /// <summary>
        /// Disposes the error monitor.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;

            _statisticsLock.Dispose();
            _errorQueue.Clear();
            _deviceErrors.Clear();
            _errorStatistics.Clear();
            _errorsByCorrelation.Clear();
            _subscriptions.Clear();
            _subscriptionFilters.Clear();

            _isDisposed = true;
            GC.SuppressFinalize(this);
        }
    }
}