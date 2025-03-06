using HydroGarden.Foundation.Abstractions.Interfaces;
using HydroGarden.Foundation.Abstractions.Interfaces.Components;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Foundation.Abstractions.Interfaces.Logging;
using HydroGarden.Foundation.Common.Events;
using HydroGarden.Foundation.Common.PropertyMetadata;
using System.Collections.Concurrent;
using HydroGarden.Foundation.Common.ErrorHandling;
using HydroGarden.Foundation.Common.Extensions;

namespace HydroGarden.Foundation.Core.Components.Devices
{
    public abstract class IoTDeviceBase(Guid id, string name, IErrorMonitor errorMonitor, ILogger? logger = null)
        : ComponentBase(id, name, errorMonitor, logger), IIoTDevice
    {
        private readonly CancellationTokenSource _executionCts = new();
        private int _consecutiveRecoveryFailures;
        private readonly int _maxRecoveryAttempts = 3;
        private readonly SemaphoreSlim _recoverySemaphore = new(1, 1);
        private readonly ConcurrentDictionary<string, DateTimeOffset> _lastRecoveryAttempts = new();

        #region Device Operations
        public virtual async Task InitializeAsync(CancellationToken ct = default)
        {
            if (State != ComponentState.Created)
                throw new InvalidOperationException($"Cannot initialize device in state {State}");

            await this.ExecuteWithErrorHandlingAsync(
                ErrorMonitor,
                async () =>
                {
                    var stateMetadata = ConstructDefaultPropertyMetadata(nameof(State));
                    await SetPropertyAsync(nameof(State), ComponentState.Initializing, stateMetadata);
                    await SetPropertyAsync("Id", Id);
                    await SetPropertyAsync("Name", Name);
                    await SetPropertyAsync("AssemblyType", AssemblyType);

                    await OnInitializeAsync(ct);

                    await SetPropertyAsync(nameof(State), ComponentState.Ready, stateMetadata);
                },
                "DEVICE_INIT_FAILED",
                $"Failed to initialize device {Id}",
                ErrorSource.Device, ct: ct);
        }

        protected virtual Task OnInitializeAsync(CancellationToken ct) => Task.CompletedTask;

        public virtual async Task StartAsync(CancellationToken ct = default)
        {
            if (State != ComponentState.Ready)
                throw new InvalidOperationException($"Cannot start device in state {State}");

            // Use ConstructDefaultPropertyMetadata
            await SetPropertyAsync(nameof(State), ComponentState.Running,
                ConstructDefaultPropertyMetadata(nameof(State)));

            try
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _executionCts.Token);
                await OnStartAsync(linkedCts.Token);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
            }
            catch (Exception)
            {
                // Use ConstructDefaultPropertyMetadata
                await SetPropertyAsync(nameof(State), ComponentState.Error,
                    ConstructDefaultPropertyMetadata(nameof(State)));
                throw;
            }
        }

        protected virtual Task OnStartAsync(CancellationToken ct) => Task.CompletedTask;

        public virtual async Task StopAsync(CancellationToken ct = default)
        {
            if (State != ComponentState.Running)
                return;

            // Use ConstructDefaultPropertyMetadata
            await SetPropertyAsync(nameof(State), ComponentState.Stopping,
                ConstructDefaultPropertyMetadata(nameof(State)));

            await _executionCts.CancelAsync();
            try
            {
                await OnStopAsync(ct);

                // Use ConstructDefaultPropertyMetadata
                await SetPropertyAsync(nameof(State), ComponentState.Ready,
                    ConstructDefaultPropertyMetadata(nameof(State)));
            }
            catch (Exception)
            {
                // Use ConstructDefaultPropertyMetadata
                await SetPropertyAsync(nameof(State), ComponentState.Error,
                    ConstructDefaultPropertyMetadata(nameof(State)));
                throw;
            }
        }

        protected virtual Task OnStopAsync(CancellationToken ct) => Task.CompletedTask;
        #endregion

        #region Error Handling
        public virtual async Task ReportErrorAsync(IApplicationError error, CancellationToken ct = default)
        {
            // Create enhanced error if needed
            var enhancedError = error as ComponentError ?? new ComponentError(
                error.DeviceId,
                error.ErrorCode,
                error.Message,
                error.Severity,
                error.Severity < ErrorSeverity.Critical, 
                ErrorSource.Communication,
                true,
                null,
                error.Exception);

            // Report through the error monitor
            await ErrorMonitor.ReportErrorAsync(enhancedError, ct);

            // Set component state to Error if severe enough
            if (error.Severity >= ErrorSeverity.Error)
            {
                await SetPropertyAsync(nameof(State), ComponentState.Error, ConstructDefaultPropertyMetadata(nameof(State)));
            }

            var alertEvent = MapToAlertEvent(error);
            if (PropertyChangedEventHandler != null)
            {
                await PropertyChangedEventHandler.HandleEventAsync(this, alertEvent, ct);
            }

            // Trigger automatic recovery attempt for recoverable errors if needed
            if (enhancedError is { IsRecoverable: true, ErrorCode: not null } &&
                enhancedError.CanAttemptRecovery())
            {
                // Use a fire-and-forget task for auto-recovery to avoid blocking
                // We're not awaiting this to prevent long delays in the error reporting flow
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await AttemptRecoveryForErrorAsync(enhancedError.ErrorCode, ct);
                    }
                    catch (Exception ex)
                    {
                        Logger.Log(ex, $"Auto-recovery attempt failed for error {enhancedError.ErrorCode}");
                    }
                }, ct);
            }
        }

        public async Task<bool> TryRecoverAsync(CancellationToken ct = default)
        {
            if (State != ComponentState.Error)
                return true; 

            if (_consecutiveRecoveryFailures >= _maxRecoveryAttempts)
            {
                await ReportErrorAsync(new ComponentError(
                        Id,
                        "RECOVERY_LIMIT_EXCEEDED",
                        $"Device recovery failed after {_maxRecoveryAttempts} attempts",
                        ErrorSeverity.Critical,
                        false, // Not recoverable
                        ErrorSource.Device, // Add the missing 'source' parameter
                        false, // Add the missing 'isTransient' parameter
                        null, // Add the missing 'context' parameter
                        null), // Add the missing 'exception' parameter
                    ct);

                return false;
            }

            try
            {
                Logger.Log($"Attempting recovery for device {Id} ({Name})");

                // First, try to stop any running operations
                if (State == ComponentState.Running)
                {
                    await StopAsync(ct);
                }

                // Try device-specific recovery
                if (await OnTryRecoverAsync(ct))
                {
                    _consecutiveRecoveryFailures = 0;
                    await SetPropertyAsync(nameof(State), ComponentState.Ready,
                        ConstructDefaultPropertyMetadata(nameof(State)));

                    Logger.Log($"Recovery successful for device {Id} ({Name})");
                    return true;
                }

                _consecutiveRecoveryFailures++;
                return false;
            }
            catch (Exception ex)
            {
                _consecutiveRecoveryFailures++;
                await ReportErrorAsync(new ComponentError(
                    Id,
                    "RECOVERY_EXCEPTION",
                    $"Exception during recovery attempt: {ex.Message}",
                    ErrorSeverity.Error,
                    true, // Still recoverable
                    ErrorSource.Communication,
                    true,
                    null,
                    ex), ct);

                return false;
            }
        }

        // New method for error-specific recovery
        protected virtual async Task<bool> AttemptRecoveryForErrorAsync(string errorCode, CancellationToken ct = default)
        {
            // Implement basic throttling for recovery attempts
            if (!await ThrottleRecoveryAttemptsAsync(errorCode, ct))
            {
                return false;
            }

            Logger.Log($"Attempting recovery for device {Id} for error: {errorCode}");

            // Perform recovery - we'll use the existing TryRecoverAsync
            var success = await TryRecoverAsync(ct);

            // Register the recovery attempt with the error monitor
            await ErrorMonitor.RegisterRecoveryAttemptAsync(Id, errorCode, success, ct);

            Logger.Log(success
                ? $"Recovery successful for device {Id} (error: {errorCode})"
                : $"Recovery failed for device {Id} (error: {errorCode})");

            return success;
        }

        private async Task<bool> ThrottleRecoveryAttemptsAsync(string errorCode, CancellationToken ct)
        {
            try
            {
                await _recoverySemaphore.WaitAsync(ct);

                // Get last recovery time for this error
                if (_lastRecoveryAttempts.TryGetValue(errorCode, out var lastAttempt))
                {
                    // Determine backoff interval based on error frequency
                    // Start with 5 seconds, double each time up to 5 minutes
                    var consecutiveFailures = 0;
                    {
                        var activeErrors = await ErrorMonitor.GetActiveErrorsForDeviceAsync(Id, ct);
                        if (activeErrors.FirstOrDefault(e => e is ComponentError ee &&
                                                             ee.ErrorCode == errorCode) is ComponentError targetError)
                        {
                            consecutiveFailures = targetError.RecoveryAttemptCount;
                        }
                    }

                    // Calculate backoff time: 5s, 10s, 20s, 40s... up to 5 minutes
                    var backoffTime = TimeSpan.FromSeconds(
                        Math.Min(300, 5 * Math.Pow(2, consecutiveFailures)));

                    // If not enough time has passed, don't attempt recovery
                    if (DateTimeOffset.UtcNow - lastAttempt < backoffTime)
                    {
                        return false;
                    }
                }

                // Update last recovery time
                _lastRecoveryAttempts[errorCode] = DateTimeOffset.UtcNow;
                return true;
            }
            finally
            {
                _recoverySemaphore.Release();
            }
        }

        /// <summary>
        /// Device-specific recovery logic to be implemented by derived classes
        /// </summary>
        /// <param name="ct"></param>
        /// <returns>bool indicating recovery was successful otherwise false</returns>>
        protected virtual Task<bool> OnTryRecoverAsync(CancellationToken ct)
        {
            return Task.FromResult(true);
        }

        private IAlertEvent MapToAlertEvent(IApplicationError error)
        {
            var severity = MapErrorSeverityToAlertSeverity(error.Severity);

            return new AlertEvent(
                error.DeviceId,
                severity,
                error.Message,
                error.Context);
        }

        private AlertSeverity MapErrorSeverityToAlertSeverity(ErrorSeverity severity)
        {
            return severity switch
            {
                ErrorSeverity.Warning => AlertSeverity.Warning,
                ErrorSeverity.Error => AlertSeverity.Error,
                ErrorSeverity.Critical => AlertSeverity.Critical,
                ErrorSeverity.Catastrophic => AlertSeverity.Critical,
                _ => AlertSeverity.Warning
            };
        }
        #endregion

        #region Helpers
        public override IPropertyMetadata ConstructDefaultPropertyMetadata(string name, bool isEditable = true, bool isVisible = true)
        {
            // Add device-specific property defaults
            var devicePropertyDefaults = new Dictionary<string, (bool IsEditable, bool IsVisible, string DisplayName, string Description)>
            {
                { "State", (false, true, "Device State", "The current state of the IoT device") },
                { "Id", (false, true, "Device ID", "The unique identifier of the IoT device") },
                { "Name", (true, true, "Device Name", "The name of the IoT device") }
            };

            // Try device-specific properties first
            if (devicePropertyDefaults.TryGetValue(name, out var defaults))
            {
                return new PropertyMetadata(
                    defaults.IsEditable,
                    defaults.IsVisible,
                    defaults.DisplayName,
                    defaults.Description);
            }

            // Otherwise, fall back to base implementation
            return base.ConstructDefaultPropertyMetadata(name, isEditable, isVisible);
        }

        public override void Dispose()
        {
            // Update State property with correct metadata
            SetPropertyAsync(nameof(State), ComponentState.Disposed,
                ConstructDefaultPropertyMetadata(nameof(State))).ConfigureAwait(false).GetAwaiter().GetResult();

            _executionCts.Cancel();
            _executionCts.Dispose();
            _recoverySemaphore.Dispose();
            base.Dispose();
        }
        #endregion
    }
}