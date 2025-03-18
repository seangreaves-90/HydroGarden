using HydroGarden.Foundation.Abstractions.Interfaces;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Foundation.Common.PropertyMetadata;
using HydroGarden.Foundation.ErrorHandling.Extensions;
using HydroGarden.Logger.Abstractions;

namespace HydroGarden.Foundation.Core.Components.Devices
{
    /// <summary>
    /// Represents a pump device in the HydroGarden system.
    /// </summary>
    public class PumpDevice : IoTDeviceBase
    {
        private CancellationTokenSource _operationsCts = new();
        private SemaphoreSlim _operationLock = new(1, 1);
        private Random _random = new();
        private Timer? _monitorTimer;
        private bool _disposed;

        /// <summary>
        /// Gets or sets the flow rate of the pump.
        /// </summary>
        public double FlowRate { get; private set; }

        /// <summary>
        /// Gets or sets whether the pump is running.
        /// </summary>
        public bool IsRunning { get; private set; }

        /// <summary>
        /// Gets the maximum flow rate of the pump.
        /// </summary>
        public double MaxFlowRate { get; }

        /// <summary>
        /// Gets the minimum flow rate of the pump.
        /// </summary>
        public double MinFlowRate { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PumpDevice"/> class.
        /// </summary>
        public PumpDevice(
            Guid id, 
            string? name, 
            IErrorMonitor errorMonitor, 
            double maxFlowRate = 100, 
            double minFlowRate = 0, 
            IEventBus? eventBus = null, 
            ILogger? logger = null)
            : base(id, name, errorMonitor, eventBus, logger)
        {
            MaxFlowRate = maxFlowRate;
            MinFlowRate = minFlowRate;

            // Register property validators
            RegisterPropertyValidator("FlowRate", ValidateFlowRate);
        }

        /// <summary>
        /// Sets the flow rate of the pump asynchronously.
        /// </summary>
        public async Task<bool> SetFlowRateAsync(double value, CancellationToken ct = default)
        {
            try
            {
                if (value < MinFlowRate || value > MaxFlowRate)
                {
                    var errorMessage = $"Flow rate must be between {MinFlowRate} and {MaxFlowRate}";
                    Logger.Log(errorMessage);
                    
                    await ErrorMonitor.ReportDeviceErrorAsync(
                        Id,
                        "FLOW_RATE_OUT_OF_RANGE",
                        errorMessage,
                        ErrorSeverity.Warning,
                        null,
                        new Dictionary<string, object?>
                        {
                            ["RequestedFlowRate"] = value,
                            ["MinFlowRate"] = MinFlowRate,
                            ["MaxFlowRate"] = MaxFlowRate,
                            ["DeviceId"] = Id,
                            ["DeviceName"] = Name
                        },
                        ct);
                        
                    return false;
                }

                // Update flow rate using optimistic concurrency
                return await UpdatePropertyOptimisticAsync<double>(
                    "FlowRate", 
                    _ => value, 
                    validateBeforeUpdate: true);
            }
            catch (Exception ex)
            {
                Logger.Log(ex, $"Error setting flow rate to {value}");
                
                await ErrorMonitor.ReportExceptionAsync(
                    this,
                    ex,
                    "FLOW_RATE_UPDATE_FAILED",
                    $"Failed to update flow rate to {value}: {ex.Message}",
                    ErrorSeverity.Error,
                    ErrorSource.Device,
                    new Dictionary<string, object?>
                    {
                        ["RequestedFlowRate"] = value,
                        ["DeviceId"] = Id,
                        ["DeviceName"] = Name
                    },
                    ct);
                    
                return false;
            }
        }

        /// <summary>
        /// Validates flow rate values based on defined limits.
        /// </summary>
        private bool ValidateFlowRate(object? value, IPropertyMetadata metadata)
        {
            if (value is double flowRate)
            {
                return flowRate >= MinFlowRate && flowRate <= MaxFlowRate;
            }
            return false;
        }

        /// <inheritdoc/>
        protected override async Task<bool> OnInitializeAsync(CancellationToken ct)
        {
            try
            {
                // Initialize property values
                FlowRate = 0;
                IsRunning = false;

                // Initialize properties with metadata
                await SetPropertyAsync("FlowRate", FlowRate);
                await SetPropertyAsync("IsRunning", IsRunning);
                await SetPropertyAsync("MaxFlowRate", MaxFlowRate);
                await SetPropertyAsync("MinFlowRate", MinFlowRate);
                await SetPropertyAsync("CurrentFlowRate", 0.0);
                await SetPropertyAsync("PowerConsumption", 0.0);
                await SetPropertyAsync("Timestamp", DateTimeOffset.UtcNow);
                
                // Include virtual property for backward compatibility
                await SetPropertyAsync("VirtualPropTest", "testProp", new PropertyMetadata(true, false, "Virtual Prop Test", "A test virtual property"));
                
                // Initialize monitoring timer but don't start it yet
                _monitorTimer = new Timer(OnMonitorTimer, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                
                return await base.OnInitializeAsync(ct);
            }
            catch (Exception ex)
            {
                Logger.Log(ex, "Error during pump device initialization");
                
                await ErrorMonitor.ReportExceptionAsync(
                    this,
                    ex,
                    "PUMP_INIT_ERROR",
                    $"Error initializing pump device: {ex.Message}",
                    ErrorSeverity.Error,
                    ErrorSource.Device,
                    new Dictionary<string, object?>
                    {
                        ["DeviceId"] = Id,
                        ["DeviceName"] = Name
                    },
                    ct);
                    
                return false;
            }
        }

        /// <inheritdoc/>
        protected override async Task<bool> OnStartAsync(CancellationToken ct)
        {
            try
            {
                await _operationLock.WaitAsync(ct);
                try
                {
                    // Reset cancellation token if it's been used
                    if (_operationsCts.IsCancellationRequested)
                    {
                        _operationsCts.Dispose();
                        // Create a new CTS but maintain reference to the token
                        var newCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                        _operationsCts = newCts;
                    }
                    
                    // Update running state
                    IsRunning = true;
                    await SetPropertyAsync("IsRunning", IsRunning);
                    
                    // Start monitoring timer
                    _monitorTimer?.Change(TimeSpan.Zero, TimeSpan.FromSeconds(1));
                    
                    // Start pump operation task
                    _ = Task.Run(() => RunPumpOperationLoopAsync(_operationsCts.Token), _operationsCts.Token);
                }
                finally
                {
                    _operationLock.Release();
                }
                
                return await base.OnStartAsync(ct);
            }
            catch (Exception ex)
            {
                Logger.Log(ex, "Error starting pump device");
                
                await ErrorMonitor.ReportExceptionAsync(
                    this,
                    ex,
                    "PUMP_START_ERROR",
                    $"Error starting pump device: {ex.Message}",
                    ErrorSeverity.Error,
                    ErrorSource.Device,
                    new Dictionary<string, object?>
                    {
                        ["DeviceId"] = Id,
                        ["DeviceName"] = Name
                    },
                    ct);
                    
                return false;
            }
        }

        /// <summary>
        /// Runs the pump operation loop until cancelled.
        /// </summary>
        private async Task RunPumpOperationLoopAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested && IsRunning)
                {
                    try
                    {
                        await SimulatePumpOperationAsync(ct);
                    }
                    catch (Exception ex) when (!(ex is OperationCanceledException))
                    {
                        // Log but continue operation
                        Logger.Log(ex, "Error in pump operation cycle");
                    }
                    
                    // Wait a short interval before next cycle
                    await Task.Delay(500, ct);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown, no action needed
            }
            catch (Exception ex)
            {
                Logger.Log(ex, "Unexpected error in pump operation loop");
                
                // Report error but don't rethrow as this runs in a background task
                await ErrorMonitor.ReportExceptionAsync(
                    this,
                    ex,
                    "PUMP_OPERATION_ERROR",
                    $"Unexpected error in pump operation: {ex.Message}",
                    ErrorSeverity.Error,
                    ErrorSource.Device,
                    new Dictionary<string, object?>
                    {
                        ["DeviceId"] = Id,
                        ["DeviceName"] = Name,
                        ["IsRunning"] = IsRunning,
                        ["FlowRate"] = FlowRate
                    });
            }
        }

        /// <summary>
        /// Simulates the pump operation by calculating and updating the current flow rate.
        /// </summary>
        private async Task SimulatePumpOperationAsync(CancellationToken ct)
        {
            if (IsRunning && FlowRate > 0)
            {
                // Calculate simulated actual flow rate with slight variations
                var fluctuation = 0.04 * _random.NextDouble() - 0.02; // +/- 2% fluctuation
                var actualFlowRate = FlowRate * (1 + fluctuation);
                actualFlowRate = Math.Max(0, Math.Min(MaxFlowRate, actualFlowRate));
                actualFlowRate = Math.Round(actualFlowRate, 2);
                
                await SetPropertyAsync("CurrentFlowRate", actualFlowRate);
                
                // Simulate power consumption - varies with flow rate
                var powerConsumption = Math.Round(10 + (actualFlowRate / MaxFlowRate) * 40, 1); // 10-50W range
                await SetPropertyAsync("PowerConsumption", powerConsumption);
                
                // Publish telemetry if event bus is available
                if (EventBus != null)
                {
                    await PublishTelemetryAsync(
                        new Dictionary<string, object>
                        {
                            ["FlowRate"] = actualFlowRate,
                            ["PowerConsumption"] = powerConsumption
                        },
                        new Dictionary<string, string>
                        {
                            ["FlowRate"] = "%",
                            ["PowerConsumption"] = "W"
                        },
                        new Dictionary<string, object>
                        {
                            ["DeviceId"] = Id,
                            ["DeviceType"] = "Pump",
                            ["Timestamp"] = DateTimeOffset.UtcNow
                        });
                }
            }
            else if (IsRunning)
            {
                // Pump is running but flow rate is zero (idle state)
                await SetPropertyAsync("CurrentFlowRate", 0.0);
                await SetPropertyAsync("PowerConsumption", 5.0); // Idle power consumption
            }
        }

        /// <summary>
        /// Timer callback for monitoring the pump state.
        /// </summary>
        private async void OnMonitorTimer(object? state)
        {
            try
            {
                if (!_disposed && IsRunning)
                {
                    // Update timestamp for monitoring
                    await SetPropertyAsync("Timestamp", DateTimeOffset.UtcNow);
                    
                    // Check for any potential issues
                    var currentFlowRate = await GetPropertyAsync<double>("CurrentFlowRate");
                    var expectedFlowRate = FlowRate;
                    
                    // Check if flow rate is significantly below expected (potential blockage)
                    if (expectedFlowRate > 10 && currentFlowRate < expectedFlowRate * 0.7)
                    {
                        // Report potential blockage as a warning
                        await ErrorMonitor.ReportDeviceErrorAsync(
                            Id,
                            "PUMP_FLOW_REDUCED",
                            "Pump flow rate is significantly below expected value",
                            ErrorSeverity.Warning,
                            null,
                            new Dictionary<string, object?>
                            {
                                ["ExpectedFlowRate"] = expectedFlowRate,
                                ["ActualFlowRate"] = currentFlowRate,
                                ["FlowRateRatio"] = currentFlowRate / expectedFlowRate,
                                ["DeviceId"] = Id,
                                ["DeviceName"] = Name
                            });
                    }
                }
            }
            catch (Exception ex)
            {
                if (!_disposed)
                {
                    Logger.Log(ex, "Error in pump monitoring timer");
                }
            }
        }
        
        /// <inheritdoc/>
        protected override async Task<bool> OnStopAsync(CancellationToken ct)
        {
            try
            {
                await _operationLock.WaitAsync(ct);
                try
                {
                    // Cancel ongoing operations
                    if (!_operationsCts.IsCancellationRequested)
                    {
                        await _operationsCts.CancelAsync();
                    }
                    
                    // Stop monitoring timer
                    _monitorTimer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                    
                    // Update state
                    IsRunning = false;
                    await SetPropertyAsync("IsRunning", IsRunning);
                    
                    // Clear current readings
                    await SetPropertyAsync("CurrentFlowRate", 0.0);
                    await SetPropertyAsync("PowerConsumption", 0.0);
                }
                finally
                {
                    _operationLock.Release();
                }
                
                return await base.OnStopAsync(ct);
            }
            catch (Exception ex)
            {
                Logger.Log(ex, "Error stopping pump device");
                
                await ErrorMonitor.ReportExceptionAsync(
                    this,
                    ex,
                    "PUMP_STOP_ERROR",
                    $"Error stopping pump device: {ex.Message}",
                    ErrorSeverity.Error,
                    ErrorSource.Device,
                    new Dictionary<string, object?>
                    {
                        ["DeviceId"] = Id,
                        ["DeviceName"] = Name
                    },
                    ct);
                    
                return false;
            }
        }

        /// <summary>
        /// Overrides the default property metadata for pump-specific properties.
        /// </summary>
        public override IPropertyMetadata ConstructDefaultPropertyMetadata(string name, bool isEditable = true, bool isVisible = true)
        {
            // Retrieve base metadata from IoTDeviceBase
            var baseMetadata = base.ConstructDefaultPropertyMetadata(name, isEditable, isVisible);

            return name switch
            {
                "FlowRate" => new PropertyMetadata(true, true, "Flow Rate", "The percentage of pump flow rate"),
                "IsRunning" => new PropertyMetadata(false, true, "Pump Running", "Indicates if the pump is running"),
                "MaxFlowRate" => new PropertyMetadata(false, true, "Max Flow Rate", "The maximum possible flow rate"),
                "MinFlowRate" => new PropertyMetadata(false, true, "Min Flow Rate", "The minimum possible flow rate"),
                "CurrentFlowRate" => new PropertyMetadata(false, true, "Current Flow Rate", "The actual measured flow rate"),
                "PowerConsumption" => new PropertyMetadata(false, true, "Power Consumption", "The estimated power consumption in watts"),
                "Timestamp" => new PropertyMetadata(false, true, "Last Updated", "The last recorded update timestamp"),
                _ => baseMetadata
            };
        }

        /// <summary>
        /// Attempts recovery for the pump device.
        /// </summary>
        protected override async Task<bool> OnTryRecoverAsync(CancellationToken ct)
        {
            try
            {
                // Get current state
                var isRunning = await GetPropertyAsync<bool>("IsRunning");
                if (isRunning)
                {
                    // Stop the pump as part of recovery
                    await StopAsync(ct);
                    
                    // Wait for pump to completely stop
                    await Task.Delay(2000, ct);
                }
                
                // Reset any internal state
                await _operationLock.WaitAsync(ct);
                try
                {
                    // Cancel any existing operations
                    if (!_operationsCts.IsCancellationRequested)
                    {
                        await _operationsCts.CancelAsync();
                    }
                    
                    // Create new operation CTS
                    _operationsCts.Dispose();
                    _operationsCts = new CancellationTokenSource();
                    
                    // Reset pump to a known state
                    await SetPropertyAsync("FlowRate", 0.0);
                    await SetPropertyAsync("CurrentFlowRate", 0.0);
                    await SetPropertyAsync("PowerConsumption", 0.0);
                    IsRunning = false;
                    await SetPropertyAsync("IsRunning", false);
                }
                finally
                {
                    _operationLock.Release();
                }
                
                // Report recovery action
                Logger.Log($"Pump device {Id} ({Name}) recovered successfully");
                
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log(ex, $"Error during pump recovery: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Handles device-specific cleanup during disposal.
        /// </summary>
        protected override void OnDeviceDispose()
        {
            try
            {
                _disposed = true;
                
                // Stop and dispose timer
                _monitorTimer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                _monitorTimer?.Dispose();
                _monitorTimer = null;
                
                // Cancel and dispose operation CTS
                if (!_operationsCts.IsCancellationRequested)
                {
                    _operationsCts.Cancel();
                }
                _operationsCts.Dispose();
                
                // Dispose the operation lock
                _operationLock.Dispose();
            }
            catch (Exception ex)
            {
                Logger.Log(ex, "Error during pump device disposal");
            }
        }
    }
}
