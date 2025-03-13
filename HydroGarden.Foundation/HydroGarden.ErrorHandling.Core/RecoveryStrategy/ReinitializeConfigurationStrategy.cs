using HydroGarden.Foundation.Abstractions.Interfaces.Components;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling.Taxonomy;
using HydroGarden.Foundation.Abstractions.Interfaces.Services;
using HydroGarden.Foundation.Common.PropertyMetadata;
using HydroGarden.Foundation.ErrorHandling.Common;
using HydroGarden.Logger.Abstractions;

namespace HydroGarden.Foundation.ErrorHandling.RecoveryStrategy
{
    /// <summary>
    /// A recovery strategy that attempts to recover from configuration-related errors
    /// by resetting the device to default configuration values.
    /// </summary>
    public class ReinitializeConfigurationStrategy : RecoveryStrategyBase
    {
        private readonly IPersistenceService _persistenceService;
        
        /// <summary>
        /// Creates a new instance of the configuration reinitialization strategy.
        /// </summary>
        /// <param name="logger">Logger for tracking recovery attempts.</param>
        /// <param name="persistenceService">Service for retrieving device and configuration.</param>
        public ReinitializeConfigurationStrategy(ILogger logger, IPersistenceService persistenceService) 
            : base(logger)
        {
            _persistenceService = persistenceService ?? throw new ArgumentNullException(nameof(persistenceService));
        }

        /// <summary>
        /// Gets the name of this recovery strategy.
        /// </summary>
        public override string Name => "Configuration Reset Strategy";

        /// <summary>
        /// This is a medium-priority strategy.
        /// </summary>
        public override int Priority => 30;
        
        /// <summary>
        /// This strategy can handle complex recovery scenarios.
        /// </summary>
        public override ErrorTaxonomy.RecoveryComplexity ComplexityLevel => 
            ErrorTaxonomy.RecoveryComplexity.Complex;
            
        /// <summary>
        /// Root causes this strategy can address.
        /// </summary>
        public override ErrorTaxonomy.RootCause[] SupportedRootCauses =>
        [
            ErrorTaxonomy.RootCause.ConfigurationError,
            ErrorTaxonomy.RootCause.ValidationFailure,
            ErrorTaxonomy.RootCause.InvalidState
        ];

        /// <summary>
        /// Determines if this strategy can recover from the specified error.
        /// </summary>
        public override bool SupportsErrorType(IApplicationError? error)
        {
            if (error == null)
                return false;
        
            // We can handle certain configuration errors
            if (error.ErrorCode == ErrorCodes.Device.CONFIGURATION_INVALID ||
                error.ErrorCode == ErrorCodes.Service.CONFIGURATION_INVALID)
                return true;
        
            // For other errors, check against supported root causes
            var rootCause = ErrorTaxonomyExtensions.AnalyzeRootCause(error.ErrorCode);
            return SupportedRootCauses.Contains(rootCause);
        }

        public override bool CanRecover(IApplicationError? error)
        {
            if (error == null)
                return false;
                
            // We can handle configuration errors even if normally unrecoverable
            if (error.ErrorCode == ErrorCodes.Device.CONFIGURATION_INVALID ||
                error.ErrorCode == ErrorCodes.Service.CONFIGURATION_INVALID)
                return true;
                
            // For other errors, check if it's unrecoverable
            if (error is ComponentError componentError && componentError.IsUnrecoverable)
                return false;
                
            // Root cause from taxonomy analysis
            var rootCause = ErrorTaxonomyExtensions.AnalyzeRootCause(error.ErrorCode);
            return SupportedRootCauses.Contains(rootCause);
        }

        /// <summary>
        /// Attempts to reinitialize the device configuration to recover from the error.
        /// </summary>
        protected override async Task<bool> ExecuteRecoveryAsync(IApplicationError? error, CancellationToken ct)
        {
            if (error == null)
                return false;
                
            try
            {
                // Step 1: Retrieve the device
                var device = await GetDeviceAsync(error.DeviceId, ct);
                if (device == null)
                {
                    Logger.Log($"Device {error.DeviceId} not found");
                    Logger.Log("Device not found");
                    return false;
                }
                
                Logger.Log($"Retrieved device {error.DeviceId} ({device.Name}) for configuration recovery");

                // Step 2: Stop device if running
                await SafeStopDeviceAsync(device, ct);
                
                // Step 3: Get default configuration values
                var defaultConfig = await GetDefaultConfigurationAsync(device, ct);
                if (defaultConfig == null || !defaultConfig.Any())
                {
                    Logger.Log($"No default configuration found for device {device.Id}");
                    Logger.Log("No default properties found");
                    return false;
                }
                
                // Step 4: Reset and restart device
                return await ResetAndRestartDeviceAsync(device, defaultConfig, ct);
            }
            catch (Exception ex)
            {
                Logger.Log(ex, "Error during configuration reset");
                Logger.Log(ex, $"Error during configuration recovery for device {error.DeviceId}");
                return false;
            }
        }
        
        /// <summary>
        /// Safely stops a device before configuration reset.
        /// </summary>
        private async Task SafeStopDeviceAsync(IIoTDevice device, CancellationToken ct)
        {
            if (device.State != ComponentState.Running)
                return;
                
            try
            {
                Logger.Log($"Stopping device {device.Id} before configuration reset");
                await device.StopAsync(ct);
                
                // Wait for device to fully stop
                await Task.Delay(TimeSpan.FromSeconds(1), ct);
            }
            catch (Exception ex)
            {
                Logger.Log(ex, $"Error stopping device {device.Id}");
            }
        }
        
        /// <summary>
        /// Resets device configuration and restarts it.
        /// </summary>
        private async Task<bool> ResetAndRestartDeviceAsync(IIoTDevice device, IDictionary<string, object> defaultConfig, CancellationToken ct)
        {
            try
            {
                // Step 1: Apply default configuration
                bool configApplied = await ApplyDefaultConfigurationAsync(device, defaultConfig, ct);
                if (!configApplied)
                {
                    Logger.Log($"Failed to apply default configuration to device {device.Id}");
                    return false;
                }
                
                // Step 2: Reinitialize the device
                Logger.Log($"Reinitializing device {device.Id} with default configuration");
                await device.InitializeAsync(ct);
                
                if (device.State != ComponentState.Ready && device.State != ComponentState.Running)
                {
                    Logger.Log($"Device initialization failed after configuration reset. State: {device.State}");
                    return false;
                }
                
                // Step 3: Restart if needed
                if (device.State == ComponentState.Ready)
                {
                    Logger.Log($"Restarting device {device.Id} after configuration reset");
                    await device.StartAsync(ct);
                }
                
                Logger.Log("Configuration reset successful");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log(ex, $"Error during device reset and restart: {device.Id}");
                return false;
            }
        }
        
        /// <summary>
        /// Retrieves a device instance from the persistence service.
        /// </summary>
        private async Task<IIoTDevice?> GetDeviceAsync(Guid deviceId, CancellationToken ct)
        {
            try
            {
                // First try to get the device directly
                var device = await _persistenceService.GetPropertyAsync<IIoTDevice>(deviceId, "Device", ct);
                if (device != null)
                {
                    Logger.Log($"Retrieved device {deviceId} directly");
                    return device;
                }

                Logger.Log($"Device {deviceId} not found directly, checking device collection");
                
                // If that fails, try to find it in the device collection
                var devices = await _persistenceService.GetPropertyAsync<IEnumerable<IIoTDevice>>(Guid.Empty, "Devices", ct);
                var foundDevice = devices?.FirstOrDefault(d => d.Id == deviceId);
                
                if (foundDevice == null)
                {
                    Logger.Log($"Device {deviceId} not found in device collection");
                    Logger.Log("Device not found");
                }
                else
                {
                    Logger.Log($"Found device {deviceId} in device collection");
                }
                
                return foundDevice;
            }
            catch (Exception ex)
            {
                Logger.Log(ex, $"Error retrieving device {deviceId} from persistence service");
                return null;
            }
        }
        
        /// <summary>
        /// Gets the default configuration for a device.
        /// </summary>
        private async Task<IDictionary<string, object>?> GetDefaultConfigurationAsync(IIoTDevice device, CancellationToken ct)
        {
            try
            {
                // First try device-specific default configuration
                Logger.Log($"Looking for device-specific configuration for {device.Id}");
                var deviceConfig = await _persistenceService.GetPropertyAsync<IDictionary<string, object>>(
                    device.Id, 
                    "DefaultProperties", 
                    ct);
                    
                if (deviceConfig != null && deviceConfig.Any())
                {
                    Logger.Log($"Found device-specific configuration with {deviceConfig.Count} properties");
                    return deviceConfig;
                }
                
                // If that fails, try to get default configuration by device type
                var deviceType = device.GetType().Name;
                Logger.Log($"Looking for type-specific configuration for {deviceType}");
                var typeConfig = await _persistenceService.GetPropertyAsync<IDictionary<string, object>>(
                    Guid.Empty, 
                    $"DefaultConfiguration_{deviceType}", 
                    ct);
                    
                if (typeConfig != null && typeConfig.Any())
                {
                    Logger.Log($"Found type-specific configuration with {typeConfig.Count} properties");
                    return typeConfig;
                }
                
                // If that fails, use hardcoded defaults based on device type
                Logger.Log($"Using hardcoded defaults for {deviceType}");
                var defaults = CreateHardcodedDefaults(device);
                
                if (defaults == null || !defaults.Any())
                {
                    Logger.Log("No default properties found for device configuration reset");
                    Logger.Log("No default properties found");
                    return null;
                }
                
                Logger.Log($"Created {defaults.Count} hardcoded default properties");
                return defaults;
            }
            catch (Exception ex)
            {
                Logger.Log(ex, $"Error retrieving default configuration for device {device.Id}");
                Logger.Log(ex, "Exception during configuration reset");
                return null;
            }
        }
        
        /// <summary>
        /// Creates hardcoded default configuration values based on device type.
        /// This is a fallback when no stored defaults are available.
        /// </summary>
        private IDictionary<string, object>? CreateHardcodedDefaults(IIoTDevice? device)
        {
            if (device == null)
                return null;
                
            var deviceType = device.GetType().Name;
            
            // Use device type to determine defaults
            return deviceType.ToLowerInvariant() switch
            {
                var name when name.Contains("ph") => new Dictionary<string, object>
                {
                    ["PollingInterval"] = TimeSpan.FromSeconds(5),
                    ["CalibrationPoint1"] = 4.0,
                    ["CalibrationPoint2"] = 7.0,
                    ["AlarmThresholdLow"] = 5.5,
                    ["AlarmThresholdHigh"] = 6.5
                },
                
                var name when name.Contains("temp") => new Dictionary<string, object>
                {
                    ["PollingInterval"] = TimeSpan.FromSeconds(10),
                    ["TemperatureUnit"] = "Celsius",
                    ["AlarmThresholdLow"] = 18.0,
                    ["AlarmThresholdHigh"] = 27.0
                },
                
                var name when name.Contains("pump") => new Dictionary<string, object>
                {
                    ["DefaultSpeed"] = 50,
                    ["MaxSpeed"] = 100,
                    ["MinSpeed"] = 10,
                    ["StartupDelay"] = TimeSpan.FromSeconds(2)
                },
                
                // Generic defaults
                _ => new Dictionary<string, object>
                {
                    ["PollingInterval"] = TimeSpan.FromSeconds(30),
                    ["Enabled"] = true
                }
            };
        }
        
        /// <summary>
        /// Applies default configuration to a device.
        /// </summary>
        private async Task<bool> ApplyDefaultConfigurationAsync(
            IIoTDevice? device, 
            IDictionary<string, object>? defaultConfig,
            CancellationToken ct)
        {
            if (device == null || defaultConfig == null || !defaultConfig.Any())
            {
                return false;
            }
            
            try
            {
                Logger.Log($"Applying {defaultConfig.Count} default configuration values to device {device.Id}");
                
                // Apply each configuration value
                foreach (var (key, value) in defaultConfig)
                {
                    Logger.Log($"Setting property {key} = {value}");
                    var metadata = new PropertyMetadata(isEditable: true, isVisible: true);
                    await device.SetPropertyAsync(key, value, metadata);
                }
                
                // Save the updated configuration
                await _persistenceService.AddOrUpdateAsync(device, ct);
                
                Logger.Log("Configuration reset successful");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log(ex, $"Error during configuration reset for device {device.Id}");
                Logger.Log(ex, $"Error during configuration reset");
                return false;
            }
        }
    }
}
