using HydroGarden.Foundation.Abstractions.Interfaces;
using HydroGarden.Foundation.Abstractions.Interfaces.Components;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Foundation.Abstractions.Interfaces.Logging;
using HydroGarden.Foundation.Common.Events;
using HydroGarden.Foundation.Common.PropertyMetadata;
using System.Collections.Concurrent;
using HydroGarden.Foundation.Common.ErrorHandling;

namespace HydroGarden.Foundation.Core.Components.Devices
{
    public abstract class IoTDeviceBase(Guid id, string name, ILogger? logger = null)
        : ComponentBase(id, name, logger), IIoTDevice
    {
        private readonly CancellationTokenSource _executionCts = new();
        private readonly ConcurrentQueue<IComponentError> _recentErrors = new();
        private readonly int _maxErrorsToTrack = 10;
        private int _consecutiveRecoveryFailures;
        private readonly int _maxRecoveryAttempts = 3;

        #region Device Operations
        public virtual async Task InitializeAsync(CancellationToken ct = default)
        {
            if (State != ComponentState.Created)
                throw new InvalidOperationException($"Cannot initialize device in state {State}");
            try
            {
                // Use ConstructDefaultPropertyMetadata to get correct metadata for State
                var stateMetadata = ConstructDefaultPropertyMetadata(nameof(State));
                await SetPropertyAsync(nameof(State), ComponentState.Initializing, stateMetadata);

                await SetPropertyAsync("Id", Id);
                await SetPropertyAsync("Name", Name);
                await SetPropertyAsync("AssemblyType", AssemblyType);
                await OnInitializeAsync(ct);

                // Use ConstructDefaultPropertyMetadata again
                await SetPropertyAsync(nameof(State), ComponentState.Ready, stateMetadata);
            }
            catch (Exception ex)
            {
                _logger.Log(ex, "Failed to initialize device");

                // Use ConstructDefaultPropertyMetadata here too
                await SetPropertyAsync(nameof(State), ComponentState.Error,
                    ConstructDefaultPropertyMetadata(nameof(State)));
                throw;
            }
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
        public async Task ReportErrorAsync(IComponentError error, CancellationToken ct = default)
        {
            // Add to recent errors, maintaining max size
            _recentErrors.Enqueue(error);
            while (_recentErrors.Count > _maxErrorsToTrack && _recentErrors.TryDequeue(out _)) { }

            // Log the error
            if (error.Exception != null)
                _logger.Log(error.Exception, $"[{error.Severity}] {error.ErrorCode}: {error.Message}");

            // Update state based on severity
            if (error.Severity >= ErrorSeverity.Error)
            {
                await SetPropertyAsync(nameof(State), ComponentState.Error,
                    ConstructDefaultPropertyMetadata(nameof(State)));
            }

            // Publish error as an alert event
            var metadata = new Dictionary<string, object>
            {
                ["ErrorCode"] = error.ErrorCode,
                ["Severity"] = error.Severity.ToString(),
                ["Context"] = error.Context
            };

            // Create and publish alert event
            var alertEvent = new AlertEvent(
                Id,
                MapErrorSeverityToAlertSeverity(error.Severity),
                error.Message,
                metadata);

            // If we have an event handler, publish the event
            if (_eventHandler != null)
            {
                await _eventHandler.HandleEventAsync(this, alertEvent, ct);
            }
        }

        public async Task<bool> TryRecoverAsync(CancellationToken ct = default)
        {
            if (State != ComponentState.Error)
                return true; // Already in good state

            if (_consecutiveRecoveryFailures >= _maxRecoveryAttempts)
            {
                await ReportErrorAsync(new ComponentError(
                    Id,
                    "RECOVERY_LIMIT_EXCEEDED",
                    $"Device recovery failed after {_maxRecoveryAttempts} attempts",
                    ErrorSeverity.Critical), ct);

                return false;
            }

            try
            {
                _logger.Log($"Attempting recovery for device {Id} ({Name})");

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

                    _logger.Log($"Recovery successful for device {Id} ({Name})");
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
                    null,
                    ex), ct);

                return false;
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
            base.Dispose();
        }


        #endregion

    }
}