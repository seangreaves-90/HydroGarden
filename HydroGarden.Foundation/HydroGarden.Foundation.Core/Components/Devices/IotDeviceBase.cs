
using System.Collections.Concurrent;
using HydroGarden.Foundation.Abstractions.Interfaces;
using HydroGarden.Foundation.Abstractions.Interfaces.Components;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Foundation.Abstractions.Interfaces.Logging;
using HydroGarden.Foundation.Common.ErrorHandling;
using HydroGarden.Foundation.Common.ErrorHandling.Constants;
using HydroGarden.Foundation.Common.Events;
using HydroGarden.Foundation.Common.Extensions;
using HydroGarden.Foundation.Common.PropertyMetadata;

namespace HydroGarden.Foundation.Core.Components.Devices
{
    /// <summary>
    /// Enhanced base class for IoT devices with improved error handling and recovery.
    /// </summary>
    public abstract class IoTDeviceBase : ComponentBase, IIoTDevice
    {
        private readonly CancellationTokenSource _executionCts = new();
        private int _consecutiveRecoveryFailures;
        private readonly int _maxRecoveryAttempts;
        private readonly SemaphoreSlim _recoverySemaphore = new(1, 1);
        private readonly ConcurrentDictionary<string, DateTimeOffset> _lastRecoveryAttempts = new();
        private readonly RecoveryOrchestrator? _recoveryOrchestrator;

        /// <summary>
        /// Creates a new IoT device base with enhanced error handling.
        /// </summary>
        protected IoTDeviceBase(
            Guid id,
            string name,
            IErrorMonitor errorMonitor,
            ILogger? logger = null,
            RecoveryOrchestrator? recoveryOrchestrator = null,
            int maxRecoveryAttempts = 3)
            : base(id, name, errorMonitor, logger)
        {
            _maxRecoveryAttempts = maxRecoveryAttempts;
            _recoveryOrchestrator = recoveryOrchestrator;
        }

        #region Device Operations

        /// <summary>
        /// Initializes the device.
        /// </summary>
        public virtual async Task InitializeAsync(CancellationToken ct = default)
        {
            if (State != ComponentState.Created && State != ComponentState.Error)
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
                    Logger.Log($"Device {Id} ({Name}) initialized successfully");
                },
                ErrorCodes.Device.INITIALIZATION_FAILED,
                $"Failed to initialize device {Id} ({Name})",
                ErrorSource.Device,
                ErrorContextBuilder.Create()
                    .WithSource(this)
                    .WithOperation("Initialize")
                    .WithLocation()
                    .Build(), ct: ct);
        }

        /// <summary>
        /// Override to implement device-specific initialization.
        /// </summary>
        protected virtual Task OnInitializeAsync(CancellationToken ct) => Task.CompletedTask;

        /// <summary>
        /// Starts the device.
        /// </summary>
        public virtual async Task StartAsync(CancellationToken ct = default)
        {
            if (State != ComponentState.Ready && State != ComponentState.Error)
                throw new InvalidOperationException($"Cannot start device in state {State}");

            await this.ExecuteWithErrorHandlingAsync(
                ErrorMonitor,
                async () =>
                {
                    await SetPropertyAsync(nameof(State), ComponentState.Running,
                        ConstructDefaultPropertyMetadata(nameof(State)));

                    // Execute device-specific start logic
                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _executionCts.Token);
                    await OnStartAsync(linkedCts.Token);

                    Logger.Log($"Device {Id} ({Name}) started successfully");
                },
                ErrorCodes.Device.STATE_TRANSITION_FAILED,
                $"Failed to start device {Id} ({Name})",
                ErrorSource.Device,
                ErrorContextBuilder.Create()
                    .WithSource(this)
                    .WithOperation("Start")
                    .WithLocation()
                    .Build(), ct: ct);
        }

        /// <summary>
        /// Override to implement device-specific start behavior.
        /// </summary>
        protected virtual Task OnStartAsync(CancellationToken ct) => Task.CompletedTask;

        /// <summary>
        /// Stops the device.
        /// </summary>
        public virtual async Task StopAsync(CancellationToken ct = default)
        {
            if (State != ComponentState.Running)
                return;

            await this.ExecuteWithErrorHandlingAsync(
                ErrorMonitor,
                async () =>
                {
                    await SetPropertyAsync(nameof(State), ComponentState.Stopping,
                        ConstructDefaultPropertyMetadata(nameof(State)));

                    await _executionCts.CancelAsync();

                    await OnStopAsync(ct);

                    await SetPropertyAsync(nameof(State), ComponentState.Ready,
                        ConstructDefaultPropertyMetadata(nameof(State)));

                    Logger.Log($"Device {Id} ({Name}) stopped successfully");
                },
                ErrorCodes.Device.STATE_TRANSITION_FAILED,
                $"Failed to stop device {Id} ({Name})",
                ErrorSource.Device,
                ErrorContextBuilder.Create()
                    .WithSource(this)
                    .WithOperation("Stop")
                    .WithLocation()
                    .Build(), ct: ct);
        }

        /// <summary>
        /// Override to implement device-specific stop behavior.
        /// </summary>
        protected virtual Task OnStopAsync(CancellationToken ct) => Task.CompletedTask;

        #endregion

        #region Error Handling

        /// <summary>
        /// Reports an error that occurred in the device.
        /// </summary>
        public virtual async Task ReportErrorAsync(IApplicationError error, CancellationToken ct = default)
        {
            // Enhance the error if needed
            var enhancedError = error as ComponentError ?? new ComponentError(
                error.DeviceId,
                error.ErrorCode,
                error.Message,
                error.Severity,
                error.Severity < ErrorSeverity.Critical,
                ErrorSource.Device,
                error.Severity < ErrorSeverity.Error,
                null,
                error.Exception);

            // Report to error monitor
            await ErrorMonitor.ReportErrorAsync(enhancedError, ct);

            // Set device state to error if severe enough
            if (error.Severity >= ErrorSeverity.Error)
            {
                await SetPropertyAsync(nameof(State), ComponentState.Error, ConstructDefaultPropertyMetadata(nameof(State)));
            }

            // Create and publish alert event
            var alertEvent = MapToAlertEvent(error);
            if (PropertyChangedEventHandler != null)
            {
                await PropertyChangedEventHandler.HandleEventAsync(this, alertEvent, ct);
            }

            // Attempt recovery if applicable
            if (enhancedError.IsRecoverable &&
                !string.IsNullOrEmpty(enhancedError.ErrorCode) &&
                enhancedError.CanAttemptRecovery())
            {
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

        /// <summary>
        /// Attempts to recover the device from an error state.
        /// </summary>
        public async Task<bool> TryRecoverAsync(CancellationToken ct = default)
        {
            if (State != ComponentState.Error)
                return true;

            Logger.Log($"Attempting recovery for device {Id} ({Name})");

            // Check if we've exceeded the maximum number of consecutive recovery attempts
            if (_consecutiveRecoveryFailures >= _maxRecoveryAttempts)
            {
                await ReportErrorAsync(new ComponentError(
                    Id,
                    ErrorCodes.Recovery.ATTEMPT_LIMIT_REACHED,
                    $"Device recovery failed after {_maxRecoveryAttempts} attempts",
                    ErrorSeverity.Critical,
                    false,
                    ErrorSource.Device,
                    false),
                    ct);

                return false;
            }

            // Try to recover using the orchestrator if available
            if (_recoveryOrchestrator != null)
            {
                // Create a recovery error to pass to the orchestrator
                var recoveryError = new ComponentError(
                    Id,
                    ErrorCodes.Device.STATE_TRANSITION_FAILED,
                    $"Device {Id} ({Name}) in error state, attempting recovery",
                    ErrorSeverity.Error,
                    true,
                    ErrorSource.Device,
                    true);

                bool orchestratorSuccess = await _recoveryOrchestrator.AttemptRecoveryAsync(recoveryError, ct);
                if (orchestratorSuccess)
                {
                    _consecutiveRecoveryFailures = 0;
                    return true;
                }
            }

            // Fall back to direct recovery if no orchestrator or orchestrator failed
            try
            {
                // Stop if running
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

                // Device-specific recovery failed
                _consecutiveRecoveryFailures++;
                Logger.Log($"Recovery failed for device {Id} ({Name})");
                return false;
            }
            catch (Exception ex)
            {
                _consecutiveRecoveryFailures++;

                await ReportErrorAsync(new ComponentError(
                    Id,
                    ErrorCodes.Recovery.STRATEGY_FAILED,
                    $"Exception during recovery attempt: {ex.Message}",
                    ErrorSeverity.Error,
                    true,
                    ErrorSource.Device,
                    true,
                    ErrorContextBuilder.Create()
                        .WithOperation("TryRecover")
                        .WithException(ex)
                        .Build(),
                    ex),
                    ct);

                return false;
            }
        }

        /// <summary>
        /// Attempts recovery specifically for the given error code.
        /// </summary>
        protected virtual async Task<bool> AttemptRecoveryForErrorAsync(string errorCode, CancellationToken ct = default)
        {
            if (!await ThrottleRecoveryAttemptsAsync(errorCode, ct))
            {
                return false;
            }

            Logger.Log($"Attempting recovery for device {Id} for error: {errorCode}");

            bool success = await TryRecoverAsync(ct);

            await ErrorMonitor.RegisterRecoveryAttemptAsync(Id, errorCode, success, ct);

            Logger.Log(success
                ? $"Recovery successful for device {Id} (error: {errorCode})"
                : $"Recovery failed for device {Id} (error: {errorCode})");

            return success;
        }

        /// <summary>
        /// Throttles recovery attempts to prevent too frequent retries.
        /// </summary>
        private async Task<bool> ThrottleRecoveryAttemptsAsync(string errorCode, CancellationToken ct)
        {
            try
            {
                await _recoverySemaphore.WaitAsync(ct);

                if (_lastRecoveryAttempts.TryGetValue(errorCode, out var lastAttempt))
                {
                    // Get the number of previous recovery attempts for this error
                    var consecutiveFailures = 0;
                    {
                        var activeErrors = await ErrorMonitor.GetActiveErrorsForDeviceAsync(Id, ct);
                        if (activeErrors.FirstOrDefault(e => e is ComponentError ee &&
                                                             ee.ErrorCode == errorCode) is ComponentError targetError)
                        {
                            consecutiveFailures = targetError.RecoveryAttemptCount;
                        }
                    }

                    // Calculate backoff time based on consecutive failures
                    var backoffTime = TimeSpan.FromSeconds(
                        Math.Min(300, 5 * Math.Pow(2, consecutiveFailures)));

                    // Check if enough time has passed since last attempt
                    if (DateTimeOffset.UtcNow - lastAttempt < backoffTime)
                    {
                        Logger.Log($"Throttling recovery for error {errorCode} - " +
                                  $"next attempt in {(backoffTime - (DateTimeOffset.UtcNow - lastAttempt)).TotalSeconds:0.0} seconds");
                        return false;
                    }
                }

                // Update last attempt time
                _lastRecoveryAttempts[errorCode] = DateTimeOffset.UtcNow;
                return true;
            }
            finally
            {
                _recoverySemaphore.Release();
            }
        }

        /// <summary>
        /// Override to implement device-specific recovery.
        /// </summary>
        protected virtual Task<bool> OnTryRecoverAsync(CancellationToken ct)
        {
            return Task.FromResult(true);
        }

        /// <summary>
        /// Maps an error to an alert event for broadcasting.
        /// </summary>
        private IAlertEvent MapToAlertEvent(IApplicationError error)
        {
            var severity = MapErrorSeverityToAlertSeverity(error.Severity);

            return new AlertEvent(
                error.DeviceId,
                severity,
                error.Message,
                error.Context);
        }

        /// <summary>
        /// Maps error severity to alert severity.
        /// </summary>
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

        /// <summary>
        /// Gets or creates default property metadata for this device.
        /// </summary>
        public override IPropertyMetadata ConstructDefaultPropertyMetadata(string name, bool isEditable = true, bool isVisible = true)
        {
            var devicePropertyDefaults = new Dictionary<string, (bool IsEditable, bool IsVisible, string DisplayName, string Description)>
            {
                { "State", (false, true, "Device State", "The current state of the IoT device") },
                { "Id", (false, true, "Device ID", "The unique identifier of the IoT device") },
                { "Name", (true, true, "Device Name", "The name of the IoT device") }
            };

            if (devicePropertyDefaults.TryGetValue(name, out var defaults))
            {
                return new PropertyMetadata(
                    defaults.IsEditable,
                    defaults.IsVisible,
                    defaults.DisplayName,
                    defaults.Description);
            }

            return base.ConstructDefaultPropertyMetadata(name, isEditable, isVisible);
        }

        /// <summary>
        /// Disposes the device.
        /// </summary>
        public override void Dispose()
        {
            SetPropertyAsync(nameof(State), ComponentState.Disposed,
                ConstructDefaultPropertyMetadata(nameof(State)))
                .ConfigureAwait(false).GetAwaiter().GetResult();

            _executionCts.Cancel();
            _executionCts.Dispose();
            _recoverySemaphore.Dispose();

            base.Dispose();
        }

        #endregion
    }
}