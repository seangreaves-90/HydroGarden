using System.Collections.Concurrent;
using HydroGarden.Foundation.Abstractions.Interfaces.Components;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Foundation.Common.Events;
using HydroGarden.Foundation.ErrorHandling.Extensions;
using HydroGarden.Logger.Abstractions;

namespace HydroGarden.Foundation.Core.Components.Devices
{
    /// <summary>
    /// Enhanced base class for IoT devices with improved error handling, state management,
    /// and hardware integration support.
    /// </summary>
    public abstract class IoTDeviceBase : ComponentBase, IIoTDevice
    {
        private readonly ConcurrentDictionary<string, DateTimeOffset> _lastRecoveryAttempts = new();
        private readonly SemaphoreSlim _recoverySemaphore = new(1, 1);
        private int _consecutiveRecoveryFailures;
        private readonly int _maxRecoveryAttempts;
        
        /// <summary>
        /// Cancellation token source for ongoing device operations.
        /// </summary>
        protected CancellationTokenSource OperationsCts = new();

        /// <summary>
        /// Creates a new IoT device base with enhanced error handling and state management.
        /// </summary>
        /// <param name="id">The unique identifier of the device.</param>
        /// <param name="name">The name of the device.</param>
        /// <param name="errorMonitor">The error monitoring component.</param>
        /// <param name="eventBus">Optional event bus for event publishing.</param>
        /// <param name="logger">Optional logger instance.</param>
        /// <param name="maxRecoveryAttempts">Maximum number of consecutive recovery attempts before requiring manual intervention.</param>
        protected IoTDeviceBase(
            Guid id,
            string? name,
            IErrorMonitor errorMonitor,
            IEventBus? eventBus = null,
            ILogger? logger = null,
            int maxRecoveryAttempts = 3)
            : base(id, name, errorMonitor, eventBus, logger)
        {
            _maxRecoveryAttempts = maxRecoveryAttempts;
            
            // Register device-specific property validators
            RegisterCommonValidators();
        }

        /// <summary>
        /// Registers common validators for IoT device properties.
        /// </summary>
        private void RegisterCommonValidators()
        {
            // Example: Add specific validation for common IoT device properties
            RegisterPropertyValidator("ConnectionStatus", (value, _) => 
                value is string status && 
                (status == "Connected" || status == "Disconnected" || status == "Connecting" || status == "Error"));
                
            RegisterPropertyValidator("NetworkStrength", (value, _) => 
                value is int strength && strength >= 0 && strength <= 100);
                
            // Add validators for device-type specific ranges as needed
        }

        #region IIoTDevice Interface Implementation

        /// <summary>
        /// Initializes the IoT device asynchronously.
        /// Explicit implementation of IIoTDevice.InitializeAsync
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        async Task IIoTDevice.InitializeAsync(CancellationToken ct)
        {
            await InitializeAsync(ct);
        }

        /// <summary>
        /// Starts the IoT device asynchronously.
        /// Explicit implementation of IIoTDevice.StartAsync
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        async Task IIoTDevice.StartAsync(CancellationToken ct)
        {
            await StartAsync(ct);
        }

        /// <summary>
        /// Stops the IoT device asynchronously.
        /// Explicit implementation of IIoTDevice.StopAsync
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        async Task IIoTDevice.StopAsync(CancellationToken ct)
        {
            await StopAsync(ct);
        }
        
        #endregion

        /// <summary>
        /// Reports an error that occurred in the device and optionally publishes an alert.
        /// </summary>
        /// <param name="error">The error that occurred.</param>
        /// <param name="ct">Optional cancellation token.</param>
        public virtual async Task ReportErrorAsync(IApplicationError error, CancellationToken ct = default)
        {
            // Report to error monitor
            await ErrorMonitor.ReportErrorAsync(error, ct);

            // Set device state to error if severe enough
            if (error.Severity >= ErrorSeverity.Error)
            {
                await HandleErrorAsync(error, ct);
            }

            // Create and publish alert event
            await PublishAlertForErrorAsync(error, ct);

            // If error is severe enough, attempt recovery
            if (error.Severity >= ErrorSeverity.Error && State == ComponentState.Error)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await AttemptRecoveryForErrorAsync(error.ErrorCode ?? "UNKNOWN_ERROR", ct);
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"Auto-recovery attempt failed: {ex.Message}");
                    }
                }, ct);
            }
        }

        /// <summary>
        /// Publishes an alert event for an error.
        /// </summary>
        /// <param name="error">The error to publish as an alert.</param>
        /// <param name="ct">Optional cancellation token.</param>
        protected virtual async Task PublishAlertForErrorAsync(IApplicationError error, CancellationToken ct = default)
        {
            var alertEvent = MapToAlertEvent(error);
            
            try
            {
                // Try EventBus first if available
                if (EventBus != null)
                {
                    await EventBus.PublishAsync(this, alertEvent, ct);
                    return;
                }
                // Fall back to property changed event handler if available
                else if (PropertyChangedEventHandler != null)
                {
                    await PropertyChangedEventHandler.HandleEventAsync(this, alertEvent, ct);
                    return;
                }
                else
                {
                    Logger.Log($"Unable to publish alert for error: {error.ErrorCode} - No event handlers available");
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to publish alert for error: {ex.Message}");
            }
        }

        /// <summary>
        /// Maps an error to an alert event.
        /// </summary>
        /// <param name="error">The error to map.</param>
        /// <returns>The mapped alert event.</returns>
        protected virtual IAlertEvent MapToAlertEvent(IApplicationError error)
        {
            var severity = MapErrorSeverityToAlertSeverity(error.Severity);

            return new AlertEvent(
                error.DeviceId,
                severity,
                error.Message,
                new Dictionary<string, object>(error.Context ?? new Dictionary<string, object>())
                {
                    ["ErrorCode"] = error.ErrorCode ?? "UNKNOWN_ERROR",
                    ["ErrorSource"] = error.Source.ToString(),
                    ["ErrorSeverity"] = error.Severity.ToString()
                });
        }

        /// <summary>
        /// Maps error severity to alert severity.
        /// </summary>
        /// <param name="severity">The error severity.</param>
        /// <returns>The corresponding alert severity.</returns>
        protected virtual AlertSeverity MapErrorSeverityToAlertSeverity(ErrorSeverity severity)
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

        /// <summary>
        /// Attempts to recover from an error state.
        /// </summary>
        /// <param name="ct">Optional cancellation token.</param>
        /// <returns>True if recovery was successful; otherwise, false.</returns>
        public virtual async Task<bool> TryRecoverAsync(CancellationToken ct = default)
        {
            if (State != ComponentState.Error)
            {
                return true;
            }

            Logger.Log($"Attempting recovery for device {Id} ({Name})");

            // Check if we've exceeded the maximum number of consecutive recovery attempts
            if (_consecutiveRecoveryFailures >= _maxRecoveryAttempts)
            {
                await ErrorMonitor.ReportDeviceErrorAsync(
                    Id,
                    "DEVICE_RECOVERY_LIMIT_REACHED",
                    $"Device recovery failed after {_maxRecoveryAttempts} attempts",
                    ErrorSeverity.Critical,
                    null,
                    new Dictionary<string, object>
                    {
                        ["MaxAttempts"] = _maxRecoveryAttempts,
                        ["ConsecutiveFailures"] = _consecutiveRecoveryFailures,
                        ["DeviceId"] = Id,
                        ["DeviceName"] = Name
                    },
                    ct);

                return false;
            }

            // Leverage the RecoverFromErrorAsync method in the new ComponentBase
            return await RecoverFromErrorAsync(ct);
        }

        /// <summary>
        /// Called during error recovery process. Device-specific recovery logic should be implemented here.
        /// </summary>
        /// <param name="ct">Optional cancellation token.</param>
        /// <returns>True if recovery preparation was successful; otherwise, false.</returns>
        protected override async Task<bool> OnRecoverFromErrorAsync(CancellationToken ct = default)
        {
            try
            {
                // Implement device-specific recovery here
                bool success = await OnTryRecoverAsync(ct);
                
                if (success)
                {
                    _consecutiveRecoveryFailures = 0;
                    Logger.Log($"Recovery successful for device {Id} ({Name})");
                }
                else
                {
                    _consecutiveRecoveryFailures++;
                    Logger.Log($"Recovery failed for device {Id} ({Name})");
                }
                
                return success;
            }
            catch (Exception ex)
            {
                _consecutiveRecoveryFailures++;
                
                await ErrorMonitor.ReportExceptionAsync(
                    this,
                    ex,
                    "DEVICE_RECOVERY_ERROR",
                    $"Exception during recovery attempt: {ex.Message}",
                    ErrorSeverity.Error,
                    ErrorSource.Device,
                    new Dictionary<string, object>
                    {
                        ["DeviceId"] = Id,
                        ["DeviceName"] = Name,
                        ["ConsecutiveFailures"] = _consecutiveRecoveryFailures
                    },
                    ct);
                
                return false;
            }
        }
        
        /// <summary>
        /// Device-specific recovery implementation - override in derived classes.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>True if recovery was successful; otherwise, false.</returns>
        protected virtual Task<bool> OnTryRecoverAsync(CancellationToken ct)
        {
            return Task.FromResult(true);
        }

        /// <summary>
        /// Attempts recovery specifically for the given error code with throttling
        /// to prevent excessive recovery attempts.
        /// </summary>
        /// <param name="errorCode">The error code to recover from.</param>
        /// <param name="ct">Optional cancellation token.</param>
        /// <returns>True if recovery was successful; otherwise, false.</returns>
        protected virtual async Task<bool> AttemptRecoveryForErrorAsync(string errorCode, CancellationToken ct = default)
        {
            if (!await ThrottleRecoveryAttemptsAsync(errorCode, ct))
            {
                return false;
            }

            Logger.Log($"Attempting recovery for device {Id} for error: {errorCode}");

            bool success = await TryRecoverAsync(ct);

            Logger.Log(success
                ? $"Recovery successful for device {Id} (error: {errorCode})"
                : $"Recovery failed for device {Id} (error: {errorCode})");

            return success;
        }

        /// <summary>
        /// Throttles recovery attempts to prevent excessive retries.
        /// </summary>
        /// <param name="errorCode">The error code to throttle.</param>
        /// <param name="ct">Optional cancellation token.</param>
        /// <returns>True if the recovery attempt should proceed; otherwise, false.</returns>
        protected virtual async Task<bool> ThrottleRecoveryAttemptsAsync(string errorCode, CancellationToken ct)
        {
            try
            {
                await _recoverySemaphore.WaitAsync(ct);

                if (_lastRecoveryAttempts.TryGetValue(errorCode, out var lastAttempt))
                {
                    // Calculate backoff time based on consecutive failures using exponential backoff
                    var backoffTime = TimeSpan.FromSeconds(
                        Math.Min(300, 5 * Math.Pow(2, _consecutiveRecoveryFailures)));

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
        /// Handles an error in the device by transitioning to the Error state
        /// and performing any device-specific error handling.
        /// </summary>
        /// <param name="error">The error to handle.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>True if the error was handled successfully, false otherwise.</returns>
        public override async Task<bool> HandleErrorAsync(IApplicationError error, CancellationToken ct = default)
        {
            // Call base HandleErrorAsync implementation to transition to Error state
            bool result = await base.HandleErrorAsync(error, ct);
            
            if (result)
            {
                // Cancel ongoing operations if needed
                if (State == ComponentState.Error)
                {
                    try
                    {
                        // Cancel any ongoing operations
                        if (!OperationsCts.IsCancellationRequested)
                        {
                            await OperationsCts.CancelAsync();
                        }
                        
                        // Allow derived classes to perform device-specific error handling
                        await OnDeviceErrorAsync(error, ct);
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"Error during device error handling: {ex.Message}");
                    }
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Device-specific error handling logic - override in derived classes.
        /// </summary>
        /// <param name="error">The error that occurred.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        protected virtual Task OnDeviceErrorAsync(IApplicationError error, CancellationToken ct)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Disposes of the device resources, cancelling any ongoing operations
        /// and cleanup device-specific resources.
        /// </summary>
        /// <param name="disposing">True if called from Dispose(); false if from finalizer</param>
        protected override void Dispose(bool disposing)
        {
            if (!Disposed) // Check to avoid duplicate Dispose
            {
                if (disposing)
                {
                    try
                    {
                        // Cancel any ongoing operations
                        if (!OperationsCts.IsCancellationRequested)
                        {
                            OperationsCts.Cancel();
                        }
                        
                        // Dispose the cancellation token source
                        OperationsCts.Dispose();
                        
                        // Dispose the recovery semaphore
                        _recoverySemaphore.Dispose();
                        
                        // Clear recovery tracking collections
                        _lastRecoveryAttempts.Clear();
                        
                        // Clear property validators
                        RemovePropertyValidator("ConnectionStatus");
                        RemovePropertyValidator("NetworkStrength");
                        
                        // Allow device-specific disposal
                        OnDeviceDispose();
                    }
                    catch (Exception ex)
                    {
                        // Log but don't rethrow from Dispose
                        Logger.Log($"Error during device disposal: {ex.Message}");
                    }
                }
                
                // Call base to handle state transitions, etc.
                base.Dispose(disposing);
            }
        }
        
        /// <summary>
        /// Asynchronously disposes of the device resources.
        /// </summary>
        protected override async ValueTask DisposeAsyncCore()
        {
            try 
            {
                // Cancel any ongoing operations
                if (!OperationsCts.IsCancellationRequested)
                {
                    await OperationsCts.CancelAsync();
                }
                
                // Dispose resources asynchronously when possible
                OperationsCts.Dispose();
                
                if (_recoverySemaphore is IAsyncDisposable asyncSemaphore)
                    await asyncSemaphore.DisposeAsync();
                else
                    _recoverySemaphore.Dispose();
                
                // Clear recovery tracking collections
                _lastRecoveryAttempts.Clear();
                
                // Clear property validators
                RemovePropertyValidator("ConnectionStatus");
                RemovePropertyValidator("NetworkStrength");
                
                // Allow device-specific disposal
                await OnDeviceDisposeAsync();
            }
            catch (Exception ex)
            {
                // Log but don't rethrow from Dispose
                Logger.Log($"Error during async device disposal: {ex.Message}");
            }
            
            // Call base implementation
            await base.DisposeAsyncCore();
        }
        
        /// <summary>
        /// Override this method to implement device-specific disposal logic.
        /// </summary>
        protected virtual void OnDeviceDispose()
        {
            // No default implementation
        }
        
        /// <summary>
        /// Override this method to implement device-specific asynchronous disposal logic.
        /// </summary>
        protected virtual ValueTask OnDeviceDisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
        
        /// <summary>
        /// Device-specific initialization logic - override in derived classes.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>True if initialization was successful; otherwise, false.</returns>
        protected override async Task<bool> OnInitializeAsync(CancellationToken ct)
        {
            try
            {
                // Override in derived classes to implement device-specific initialization
                // Examples:
                // - Connect to hardware
                // - Initialize sensors
                // - Setup communication channels
                
                // Set some common device properties
                await SetPropertyAsync("DeviceType", GetType().Name);
                await SetPropertyAsync("ConnectionStatus", "Disconnected");
                await SetPropertyAsync("LastInitialized", DateTimeOffset.UtcNow);
                
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log($"Error initializing device: {ex.Message}");
                
                await ErrorMonitor.ReportExceptionAsync(
                    this,
                    ex,
                    "DEVICE_INIT_ERROR",
                    $"Error initializing device {Name}: {ex.Message}",
                    ErrorSeverity.Error,
                    ErrorSource.Device,
                    new Dictionary<string, object>
                    {
                        ["DeviceId"] = Id,
                        ["DeviceName"] = Name
                    },
                    ct);
                
                return false;
            }
        }
        
        /// <summary>
        /// Device-specific start logic - override in derived classes.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>True if start was successful; otherwise, false.</returns>
        protected override async Task<bool> OnStartAsync(CancellationToken ct)
        {
            try
            {
                // Reset the operation cancellation token source if it's been canceled
                if (OperationsCts.IsCancellationRequested)
                {
                    OperationsCts.Dispose();
                    OperationsCts = new CancellationTokenSource();
                }
                
                // Override in derived classes to implement device-specific start logic
                // Examples:
                // - Start sensors
                // - Begin data collection
                // - Activate hardware components
                
                await SetPropertyAsync("ConnectionStatus", "Connected");
                await SetPropertyAsync("LastStarted", DateTimeOffset.UtcNow);
                
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log($"Error starting device: {ex.Message}");
                
                await ErrorMonitor.ReportExceptionAsync(
                    this,
                    ex,
                    "DEVICE_START_ERROR",
                    $"Error starting device {Name}: {ex.Message}",
                    ErrorSeverity.Error,
                    ErrorSource.Device,
                    new Dictionary<string, object>
                    {
                        ["DeviceId"] = Id,
                        ["DeviceName"] = Name
                    },
                    ct);
                
                return false;
            }
        }
        
        /// <summary>
        /// Device-specific stop logic - override in derived classes.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>True if stop was successful; otherwise, false.</returns>
        protected override async Task<bool> OnStopAsync(CancellationToken ct)
        {
            try
            {
                // Cancel any ongoing operations
                await OperationsCts.CancelAsync();
                
                // Override in derived classes to implement device-specific stop logic
                // Examples: 
                // - Stop sensors
                // - End data collection
                // - Deactivate hardware components
                
                await SetPropertyAsync("ConnectionStatus", "Disconnected");
                await SetPropertyAsync("LastStopped", DateTimeOffset.UtcNow);
                
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log($"Error stopping device: {ex.Message}");
                
                await ErrorMonitor.ReportExceptionAsync(
                    this,
                    ex,
                    "DEVICE_STOP_ERROR",
                    $"Error stopping device {Name}: {ex.Message}",
                    ErrorSeverity.Error,
                    ErrorSource.Device,
                    new Dictionary<string, object>
                    {
                        ["DeviceId"] = Id,
                        ["DeviceName"] = Name
                    },
                    ct);
                
                return false;
            }
        }

        /// <summary>
        /// Publishes a telemetry event with sensor readings.
        /// </summary>
        /// <param name="readings">The sensor readings to publish.</param>
        /// <param name="units">Optional units of measurement.</param>
        /// <param name="metadata">Optional metadata for the event.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        protected virtual async Task PublishTelemetryAsync(
            IDictionary<string, object> readings,
            IDictionary<string, string>? units = null,
            IDictionary<string, object>? metadata = null)
        {
            if (EventBus == null)
            {
                Logger.Log("Cannot publish telemetry: No event bus configured");
                return;
            }
            
            try
            {
                var telemetryEvent = new TelemetryEvent(
                    Id,
                    readings,
                    units);
                
                if (metadata != null)
                {
                    telemetryEvent.Metadata = metadata;
                }
                
                await EventBus.PublishAsync(this, telemetryEvent);
            }
            catch (Exception ex)
            {
                Logger.Log($"Error publishing telemetry: {ex.Message}");
            }
        }

    }
}
