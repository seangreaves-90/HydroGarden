using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling.RecoveryStrategy;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling.Taxonomy;
using HydroGarden.Foundation.ErrorHandling.Common;
using HydroGarden.Logger.Abstractions;

namespace HydroGarden.Foundation.ErrorHandling.RecoveryStrategy;

/// <summary>
/// Base class for error recovery strategies with common functionality.
/// </summary>
public abstract class RecoveryStrategyBase(ILogger logger) : IRecoveryStrategy
{
    protected readonly ILogger Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly Dictionary<Guid, RecoveryAttemptTracker> _deviceRecoveryStatus = [];

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
    /// Determines if this strategy supports the error type (capability check).
    /// </summary>
    /// <param name="error">The error to check.</param>
    /// <returns>True if this strategy supports this error type.</returns>
    public virtual bool SupportsErrorType(IApplicationError? error)
    {
        // Check for null errors
        if (error == null)
            return false;
            
        // Get the root cause
        var rootCause = ErrorTaxonomyExtensions.AnalyzeRootCause(error.ErrorCode);

        // Check if this strategy supports the root cause
        return SupportedRootCauses.Contains(rootCause) || 
               SupportedRootCauses.Contains(ErrorTaxonomy.RootCause.Unknown);
    }
    
    /// <summary>
    /// Legacy method maintained for backward compatibility.
    /// Determines if this strategy can recover from the specified error.
    /// </summary>
    /// <param name="error">The error to check.</param>
    /// <returns>True if this strategy can recover from the error, false otherwise.</returns>
    public virtual bool CanRecover(IApplicationError? error)
    {
        // Check for null or unrecoverable errors
        if (error == null)
            return false;
            
        if (error is ComponentError componentError && componentError.IsUnrecoverable)
            return false;
            
        // Use the new method but maintain behavior for backward compatibility
        return SupportsErrorType(error);
    }

    /// <summary>
    /// Attempts to recover from the error.
    /// </summary>
    public async Task<bool> AttemptRecoveryAsync(IApplicationError? error, CancellationToken ct = default)
    {
        // Check if error is null
        if (error == null)
        {
            Logger.Log("Cannot attempt recovery: Error is null");
            Logger.Log("Cannot attempt recovery");
            return false;
        }
        
        // 1. Check if this strategy supports this error type (capability check)
        if (!SupportsErrorType(error))
        {
            Logger.Log($"Strategy '{Name}' does not support error type {error.ErrorCode}");
            Logger.Log("Cannot attempt recovery");
            return false;
        }
        
        // 2. Check if the error is marked as unrecoverable (compatibility check)
        if (error is ComponentError componentError && componentError.IsUnrecoverable)
        {
            Logger.Log($"Strategy '{Name}' cannot recover from unrecoverable error {error.ErrorCode}");
            Logger.Log("Cannot attempt recovery");
            return false;
        }

        // 3. Check if we can attempt recovery based on state
        TimeSpan backoffPeriod = CalculateBackoffPeriod(error);
        bool canAttemptNow = true;
        
        // Use the specific method if available
        if (error is ComponentError errorComponent)
        {
            canAttemptNow = errorComponent.CanAttemptRecovery();
        }
        else if (error.CanAttemptRecoveryNow != null)
        {
            canAttemptNow = error.CanAttemptRecoveryNow(backoffPeriod, MaxRecoveryAttempts);
        }
        
        if (!canAttemptNow)
        {
            // Check if it's because of max attempts
            if (error.RecoveryAttemptCount >= MaxRecoveryAttempts)
            {
                Logger.Log($"Cannot attempt recovery for error {error.ErrorCode}: Maximum attempts exceeded ({error.RecoveryAttemptCount}/{MaxRecoveryAttempts})");
            }
            else if (error.LastRecoveryAttempt.HasValue)
            {
                // It's because of backoff period
                var remainingTime = backoffPeriod - (DateTimeOffset.UtcNow - error.LastRecoveryAttempt.Value);
                if (remainingTime > TimeSpan.Zero)
                {
                    Logger.Log($"Cannot attempt recovery for error {error.ErrorCode}: Backoff period not elapsed (try again in {remainingTime.TotalSeconds:F1}s)");
                }
            }
            
            Logger.Log("Cannot attempt recovery");
            return false;
        }

        // Record the recovery attempt directly on the error
        error.RecordRecoveryAttempt();
        Logger.Log($"Attempting recovery for device {error.DeviceId} using strategy '{Name}' (attempt {error.RecoveryAttemptCount})");
        
        try
        {
            var status = GetRecoveryStatus(error.DeviceId);

            // Check if we've exceeded max attempts for this device with this strategy
            if (status.AttemptCount >= MaxRecoveryAttempts)
            {
                Logger.Log($"Cannot attempt recovery for error {error.ErrorCode} - backoff period not elapsed or max attempts reached");
                Logger.Log("Cannot attempt recovery");
                return false;
            }

            // Apply exponential backoff
            if (status.LastAttempt.HasValue)
            {
                var backoffTime = TimeSpan.FromSeconds(Math.Min(300, Math.Pow(2, status.AttemptCount)));
                if (DateTimeOffset.UtcNow - status.LastAttempt.Value < backoffTime)
                {
                    Logger.Log($"Cannot attempt recovery for error {error.ErrorCode} - backoff period not elapsed or max attempts reached");
                    Logger.Log("Cannot attempt recovery");
                    return false;
                }
            }

            // Update status and record attempt on the error
            status.AttemptCount++;
            status.LastAttempt = DateTimeOffset.UtcNow;

            if (error is ComponentError errorComp)
            {
                errorComp.RecordRecoveryAttempt();
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
                Logger.Log(ex, $"Exception during recovery attempt");
                return false;
            }
        }
        catch (Exception ex)
        {
            Logger.Log(ex, $"Exception during recovery setup for device {error.DeviceId}");
            return false;
        }
    }

    /// <summary>
    /// Calculates the appropriate backoff period for an error based on attempt count.
    /// </summary>
    protected virtual TimeSpan CalculateBackoffPeriod(IApplicationError error)
    {
        // Exponential backoff with a cap of 5 minutes
        double seconds = Math.Min(300, Math.Pow(2, error.RecoveryAttemptCount));
        return TimeSpan.FromSeconds(seconds);
    }
    
    /// <summary>
    /// Executes the recovery logic specific to this strategy.
    /// </summary>
    protected abstract Task<bool> ExecuteRecoveryAsync(IApplicationError? error, CancellationToken ct);

    /// <summary>
    /// Gets the recovery status for a device.
    /// </summary>
    protected virtual RecoveryAttemptTracker GetRecoveryStatus(Guid deviceId)
    {
        if (!_deviceRecoveryStatus.TryGetValue(deviceId, out var status))
        {
            status = new RecoveryAttemptTracker();
            _deviceRecoveryStatus[deviceId] = status;
        }
        return status;
    }

    /// <summary>
    /// Tracks recovery attempts for a device for internal tracking purposes.
    /// This is separate from the public RecoveryStatus model used for reporting.
    /// </summary>
    public class RecoveryAttemptTracker
    {
        /// <summary>
        /// Gets or sets the number of recovery attempts.
        /// </summary>
        public int AttemptCount { get; set; }
        
        /// <summary>
        /// Gets or sets the timestamp of the last recovery attempt.
        /// </summary>
        public DateTimeOffset? LastAttempt { get; set; }
        
        /// <summary>
        /// Creates a new instance of the RecoveryAttemptTracker class.
        /// </summary>
        public RecoveryAttemptTracker()
        {
            AttemptCount = 0;
            LastAttempt = null;
        }
    }
}