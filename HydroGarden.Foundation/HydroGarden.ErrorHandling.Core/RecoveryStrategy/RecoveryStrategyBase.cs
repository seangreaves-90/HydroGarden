using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling.RecoveryStrategy;
using HydroGarden.Foundation.ErrorHandling.Common;
using HydroGarden.Logger.Abstractions;

namespace HydroGarden.Foundation.ErrorHandling.RecoveryStrategy
{
    /// <summary>
    /// Base class for error recovery strategies with common functionality.
    /// </summary>
    public abstract class RecoveryStrategyBase(ILogger logger) : IRecoveryStrategy
    {
        protected readonly ILogger Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private readonly Dictionary<Guid, RecoveryStatus> _deviceRecoveryStatus = new();

        /// <summary>
        /// Gets the name of this recovery strategy.
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// Gets the maximum number of recovery attempts.
        /// </summary>
        protected virtual int MaxRecoveryAttempts => 3;

        /// <summary>
        /// Gets the priority of this strategy (lower numbers run first).
        /// </summary>
        public virtual int Priority => 100;
        
        /// <summary>
        /// Gets the recovery complexity this strategy can handle.
        /// </summary>
        public virtual ErrorTaxonomy.RecoveryComplexity ComplexityLevel => ErrorTaxonomy.RecoveryComplexity.Moderate;
        
        /// <summary>
        /// Gets types of root causes this strategy can address.
        /// </summary>
        public virtual ErrorTaxonomy.RootCause[] SupportedRootCauses => [ErrorTaxonomy.RootCause.Unknown];

        /// <summary>
        /// Determines if this strategy can recover from the specified error.
        /// </summary>
        /// <summary>
        /// Determines if this strategy can recover from the specified error.
        /// The base implementation checks if the error's root cause is supported by this strategy.
        /// </summary>
        /// <param name="error">The error to check.</param>
        /// <returns>True if this strategy can recover from the error, false otherwise.</returns>
        public virtual bool CanRecover(IApplicationError? error)
        {
            switch (error)
            {
                case null:
                // Check if it's a non-recoverable error
                case ComponentError { IsUnrecoverable: true }:
                    return false;
                default:
                {
                    // Get the root cause
                    var rootCause = ErrorTaxonomy.AnalyzeRootCause(error.ErrorCode);
            
                    // Check if this strategy supports the root cause
                    return SupportedRootCauses.Contains(rootCause) || SupportedRootCauses.Contains(ErrorTaxonomy.RootCause.Unknown);
                }
            }
        }

        /// <summary>
        /// Attempts to recover from the error.
        /// </summary>
        public async Task<bool> AttemptRecoveryAsync(IApplicationError? error, CancellationToken ct = default)
        {
            if (!CanRecover(error))
            {
                Logger.Log($"Strategy '{Name}' cannot recover from error {error?.ErrorCode}");
                return false;
            }

            if (error is ComponentError componentError && !componentError.CanAttemptRecovery())
            {
                Logger.Log($"Cannot attempt recovery for error {error.ErrorCode} - backoff period not elapsed or max attempts reached");
                return false;
            }

            // Get or create recovery status
            if (error != null)
            {
                var status = GetRecoveryStatus(error.DeviceId);

                // Check if we've exceeded max attempts for this device with this strategy
                if (status.AttemptCount >= MaxRecoveryAttempts)
                {
                    Logger.Log($"Max recovery attempts ({MaxRecoveryAttempts}) exceeded for device {error.DeviceId} with strategy '{Name}'");
                    return false;
                }

                // Apply exponential backoff
                if (status.LastAttempt.HasValue)
                {
                    var backoffTime = TimeSpan.FromSeconds(Math.Min(300, Math.Pow(2, status.AttemptCount)));
                    if (DateTimeOffset.UtcNow - status.LastAttempt.Value < backoffTime)
                    {
                        Logger.Log($"Backoff period not elapsed for device {error.DeviceId} with strategy '{Name}'");
                        return false;
                    }
                }

                // Update status and record attempt on the error
                status.AttemptCount++;
                status.LastAttempt = DateTimeOffset.UtcNow;

                if (error is ComponentError compError)
                {
                    compError.RecordRecoveryAttempt();
                }

                Logger.Log($"Attempting recovery for device {error.DeviceId} using strategy '{Name}' (attempt {status.AttemptCount})");

                try
                {
                    // Perform the actual recovery
                    bool result = await ExecuteRecoveryAsync(error, ct);

                    // Reset attempts on success
                    if (result)
                    {
                        Logger.Log($"Recovery successful for device {error.DeviceId} using strategy '{Name}'");
                        status.AttemptCount = 0;
                    }
                    else
                    {
                        Logger.Log($"Recovery failed for device {error.DeviceId} using strategy '{Name}'");
                    }

                    return result;
                }
                catch (Exception ex)
                {
                    Logger.Log(ex, $"Exception during recovery for device {error.DeviceId} using strategy '{Name}'");
                    return false;
                }
            }

            return false;
        }

        /// <summary>
        /// Executes the recovery logic specific to this strategy.
        /// </summary>
        protected abstract Task<bool> ExecuteRecoveryAsync(IApplicationError? error, CancellationToken ct);

        /// <summary>
        /// Gets the recovery status for a device.
        /// </summary>
        private RecoveryStatus GetRecoveryStatus(Guid deviceId)
        {
            if (!_deviceRecoveryStatus.TryGetValue(deviceId, out var status))
            {
                status = new RecoveryStatus();
                _deviceRecoveryStatus[deviceId] = status;
            }
            return status;
        }

        /// <summary>
        /// Tracks recovery status for a device.
        /// </summary>
        private class RecoveryStatus
        {
            public int AttemptCount { get; set; }
            public DateTimeOffset? LastAttempt { get; set; }
        }
    }
}