using System.Collections.Concurrent;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling.RecoveryStrategy;
using HydroGarden.Foundation.Abstractions.Interfaces.Logging;

using HydroGarden.Foundation.Common.ErrorHandling;


namespace HydroGarden.Foundation.Core.Services
{
    /// <summary>
    /// Centralizes error recovery management across the system.
    /// Coordinates between the RecoveryOrchestrator and ComponentRecoveryService.
    /// </summary>
    public class RecoveryManagerService : IRecoveryManager, IDisposable
    {
        private readonly ILogger _logger;
        private readonly IErrorMonitor _errorMonitor;
        private readonly RecoveryOrchestrator _recoveryOrchestrator;
        private readonly IComponentRecoveryService _componentRecoveryService;
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, RecoveryStatus>> _recoveryHistory = new();
        private readonly SemaphoreSlim _statisticsLock = new(1, 1);
        private bool _isDisposed;

        /// <summary>
        /// Creates a new recovery manager service.
        /// </summary>
        public RecoveryManagerService(
            ILogger logger,
            IErrorMonitor errorMonitor,
            RecoveryOrchestrator recoveryOrchestrator,
            IComponentRecoveryService componentRecoveryService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _errorMonitor = errorMonitor ?? throw new ArgumentNullException(nameof(errorMonitor));
            _recoveryOrchestrator = recoveryOrchestrator ?? throw new ArgumentNullException(nameof(recoveryOrchestrator));
            _componentRecoveryService = componentRecoveryService ?? throw new ArgumentNullException(nameof(componentRecoveryService));

            _logger.Log("Recovery Manager Service initialized");
        }

        /// <summary>
        /// Attempts to recover from an error using appropriate strategies.
        /// </summary>
        public async Task<RecoveryStatus> AttemptRecoveryAsync(IApplicationError error, CancellationToken ct = default)
        {
            if (error == null)
                throw new ArgumentNullException(nameof(error));

            _logger.Log($"Attempting recovery for error: {error.ErrorCode}, Device: {error.DeviceId}");

            // Create recovery status result
            var result = new RecoveryStatus
            {
                IsSuccessful = false,
                AttemptCount = 1,  // Start with attempt 1
                ErrorCodes = error.ErrorCode != null ? new[] { error.ErrorCode } : Array.Empty<string>(),
                Timestamp = DateTimeOffset.UtcNow
            };

            try
            {
                // First try recovery via the orchestrator
                bool success = await _recoveryOrchestrator.AttemptRecoveryAsync(error, ct);

                if (success)
                {
                    result.IsSuccessful = true;
                    result.SuccessfulStrategy = "Orchestrated Recovery";

                    // Record successful recovery
                    RecordRecoveryAttempt(error.ErrorCode, error.DeviceId, true);

                    _logger.Log($"Successfully recovered from error {error.ErrorCode} for device {error.DeviceId}");
                    return result;
                }

                // If orchestrator fails, try component recovery service as fallback
                success = await _componentRecoveryService.RecoverDeviceAsync(error.DeviceId, ct);

                result.IsSuccessful = success;
                result.SuccessfulStrategy = success ? "Component Recovery Service" : null;

                // Record recovery attempt
                RecordRecoveryAttempt(error.ErrorCode, error.DeviceId, success);

                return result;
            }
            catch (Exception ex)
            {
                _logger.Log(ex, $"Exception during recovery attempt for error {error.ErrorCode}, device {error.DeviceId}");

                // Record failed recovery attempt
                RecordRecoveryAttempt(error.ErrorCode, error.DeviceId, false);

                return result;
            }
        }

        /// <summary>
        /// Attempts to recover a device.
        /// </summary>
        public async Task<RecoveryStatus> RecoverDeviceAsync(Guid deviceId, CancellationToken ct = default)
        {
            _logger.Log($"Attempting to recover device {deviceId}");

            // Create recovery status result
            var result = new RecoveryStatus
            {
                IsSuccessful = false,
                AttemptCount = 1,
                ErrorCodes = Array.Empty<string>(),
                Timestamp = DateTimeOffset.UtcNow
            };

            try
            {
                // Attempt recovery via component recovery service
                bool success = await _componentRecoveryService.RecoverDeviceAsync(deviceId, ct);

                result.IsSuccessful = success;
                result.SuccessfulStrategy = success ? "Component Recovery Service" : null;

                // Get active errors for this device to record in the result
                var activeErrors = await _errorMonitor.GetActiveErrorsForDeviceAsync(deviceId, ct);
                var errorCodes = activeErrors
                    .Where(e => e.ErrorCode != null)
                    .Select(e => e.ErrorCode!)
                    .ToArray();

                result.ErrorCodes = errorCodes;

                // Record recovery attempts for each error
                foreach (var errorCode in errorCodes.Where(c => !string.IsNullOrEmpty(c)))
                {
                    RecordRecoveryAttempt(errorCode, deviceId, success);
                }

                _logger.Log(success
                    ? $"Successfully recovered device {deviceId}"
                    : $"Failed to recover device {deviceId}");

                return result;
            }
            catch (Exception ex)
            {
                _logger.Log(ex, $"Exception during device recovery for {deviceId}");
                return result;
            }
        }

        /// <summary>
        /// Gets recovery statistics for a time period.
        /// </summary>
        public async Task<IDictionary<string, int>> GetRecoveryStatisticsAsync(DateTimeOffset since, CancellationToken ct = default)
        {
            await _statisticsLock.WaitAsync(ct);
            try
            {
                var statistics = new Dictionary<string, int>();

                // Calculate statistics from recovery history
                foreach (var (errorCode, deviceRecoveries) in _recoveryHistory)
                {
                    int totalAttempts = 0;
                    int successfulAttempts = 0;

                    foreach (var (_, recoveryStatus) in deviceRecoveries)
                    {
                        if (recoveryStatus.LastAttempt >= since)
                        {
                            totalAttempts += recoveryStatus.AttemptCount;
                            if (recoveryStatus.SuccessCount > 0)
                            {
                                successfulAttempts += recoveryStatus.SuccessCount;
                            }
                        }
                    }

                    statistics[$"{errorCode}_TotalAttempts"] = totalAttempts;
                    statistics[$"{errorCode}_SuccessfulAttempts"] = successfulAttempts;

                    // Calculate success rate if attempts > 0
                    if (totalAttempts > 0)
                    {
                        int successRate = (int)((double)successfulAttempts / totalAttempts * 100);
                        statistics[$"{errorCode}_SuccessRate"] = successRate;
                    }
                }

                // Add overall statistics
                int overallAttempts = statistics.Where(kvp => kvp.Key.EndsWith("_TotalAttempts")).Sum(kvp => kvp.Value);
                int overallSuccesses = statistics.Where(kvp => kvp.Key.EndsWith("_SuccessfulAttempts")).Sum(kvp => kvp.Value);

                statistics["Overall_TotalAttempts"] = overallAttempts;
                statistics["Overall_SuccessfulAttempts"] = overallSuccesses;

                if (overallAttempts > 0)
                {
                    int overallSuccessRate = (int)((double)overallSuccesses / overallAttempts * 100);
                    statistics["Overall_SuccessRate"] = overallSuccessRate;
                }

                return statistics;
            }
            finally
            {
                _statisticsLock.Release();
            }
        }

        /// <summary>
        /// Records a recovery attempt in the history.
        /// </summary>
        private void RecordRecoveryAttempt(string? errorCode, Guid deviceId, bool successful)
        {
            if (string.IsNullOrEmpty(errorCode))
                return;

            var deviceMap = _recoveryHistory.GetOrAdd(
                errorCode,
                _ => new ConcurrentDictionary<Guid, RecoveryStatus>());

            var status = deviceMap.GetOrAdd(
                deviceId,
                _ => new RecoveryStatus { AttemptCount = 0, SuccessCount = 0 });

            status.AttemptCount++;
            if (successful)
            {
                status.SuccessCount++;
            }
            status.LastAttempt = DateTimeOffset.UtcNow;
        }

        /// <summary>
        /// Disposes resources.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;

            _statisticsLock.Dispose();
            _isDisposed = true;

            GC.SuppressFinalize(this);
        }
    }
}