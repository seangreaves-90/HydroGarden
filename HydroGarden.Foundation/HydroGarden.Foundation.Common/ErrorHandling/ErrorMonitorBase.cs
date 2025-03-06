using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.Abstractions.Interfaces.Logging;
using System.Collections.Concurrent;

namespace HydroGarden.Foundation.Common.ErrorHandling
{
    public abstract class ErrorMonitorBase(ILogger logger, int maxErrorQueueSize = 1000)
    {
        protected readonly ILogger Logger = logger;
        protected readonly ConcurrentQueue<IApplicationError> ErrorQueue = new();
        protected readonly int MaxErrorQueueSize = maxErrorQueueSize;
        protected readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, ComponentError>> DeviceErrors = new();
        protected readonly ConcurrentDictionary<string, int> ErrorStatistics = new();

        public virtual Task ReportErrorAsync(IApplicationError error, CancellationToken ct = default)
        {
            Logger.Log(error.Exception, $"[{error.Severity}] {error.ErrorCode}: {error.Message}");

            ErrorQueue.Enqueue(error);
            while (ErrorQueue.Count > MaxErrorQueueSize && ErrorQueue.TryDequeue(out _)) { }

            // Track detailed error information
            if (error is ComponentError componentError && error.ErrorCode != null)
            {
                TrackComponentError(componentError);

                // Update error statistics
                ErrorStatistics.AddOrUpdate(
                    error.ErrorCode,
                    1,
                    (_, count) => count + 1);
            }

            return Task.CompletedTask;
        }

        public virtual Task<IReadOnlyList<IApplicationError>> GetRecentErrorsAsync(int count = 10, CancellationToken ct = default)
        {
            var result = ErrorQueue.TakeLast(Math.Min(count, ErrorQueue.Count)).ToList();
            return Task.FromResult<IReadOnlyList<IApplicationError>>(result);
        }

        public virtual Task<bool> HasActiveErrorsAsync(ErrorSeverity minimumSeverity = ErrorSeverity.Error, CancellationToken ct = default)
        {
            return Task.FromResult(ErrorQueue.Any(e => e.Severity >= minimumSeverity));
        }

        // New methods for enhanced error tracking

        public virtual Task<IReadOnlyList<IApplicationError>> GetActiveErrorsForDeviceAsync(
            Guid deviceId,
            CancellationToken ct = default)
        {
            if (DeviceErrors.TryGetValue(deviceId, out var deviceErrorMap))
            {
                return Task.FromResult<IReadOnlyList<IApplicationError>>(
                    deviceErrorMap.Values.Cast<IApplicationError>().ToList().AsReadOnly());
            }

            return Task.FromResult<IReadOnlyList<IApplicationError>>(
                new List<IApplicationError>().AsReadOnly());
        }

        public virtual Task MarkErrorHandledAsync(
            Guid deviceId,
            string errorCode,
            CancellationToken ct = default)
        {
            if (DeviceErrors.TryGetValue(deviceId, out var deviceErrorMap))
            {
                deviceErrorMap.TryRemove(errorCode, out _);

                // If no more errors for this device, remove the device entry
                if (deviceErrorMap.IsEmpty)
                {
                    DeviceErrors.TryRemove(deviceId, out _);
                }
            }

            return Task.CompletedTask;
        }

        public virtual Task<IDictionary<string, int>> GetErrorStatisticsAsync(
            DateTimeOffset since,
            CancellationToken ct = default)
        {
            return Task.FromResult<IDictionary<string, int>>(
                new Dictionary<string, int>(ErrorStatistics));
        }

        public virtual Task RegisterRecoveryAttemptAsync(
            Guid deviceId,
            string errorCode,
            bool successful,
            CancellationToken ct = default)
        {
            if (DeviceErrors.TryGetValue(deviceId, out var deviceErrorMap) &&
                deviceErrorMap.TryGetValue(errorCode, out var error))
            {
                if (successful)
                {
                    // Remove the error if recovery was successful
                    deviceErrorMap.TryRemove(errorCode, out _);

                    // If no more errors for this device, remove the device entry
                    if (deviceErrorMap.IsEmpty)
                    {
                        DeviceErrors.TryRemove(deviceId, out _);
                    }
                }
                else
                {
                    // Record the failed recovery attempt
                    error.RecordRecoveryAttempt();
                }
            }

            return Task.CompletedTask;
        }

        private void TrackComponentError(ComponentError error)
        {
            if (string.IsNullOrEmpty(error.ErrorCode))
                return;

            var deviceErrorMap = DeviceErrors.GetOrAdd(
                error.DeviceId,
                _ => new ConcurrentDictionary<string, ComponentError>());

            deviceErrorMap[error.ErrorCode] = error;
        }
    }
}