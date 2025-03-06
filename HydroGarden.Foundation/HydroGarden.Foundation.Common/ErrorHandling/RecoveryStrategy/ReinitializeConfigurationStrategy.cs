using HydroGarden.Foundation.Abstractions.Interfaces;
using HydroGarden.Foundation.Abstractions.Interfaces.Components;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.Abstractions.Interfaces.Logging;
using HydroGarden.Foundation.Abstractions.Interfaces.Services;
using HydroGarden.Foundation.Common.ErrorHandling.Constants;

namespace HydroGarden.Foundation.Common.ErrorHandling.RecoveryStrategy;

/// <summary>
/// Attempts to recover by reinitializing device configuration.
/// </summary>
public class ReinitializeConfigurationStrategy(ILogger logger, IPersistenceService persistenceService)
    : RecoveryStrategyBase(logger)
{
    private readonly IPersistenceService _persistenceService = persistenceService ?? throw new ArgumentNullException(nameof(persistenceService));

    /// <summary>
    /// Gets the name of this recovery strategy.
    /// </summary>
    public override string Name => "Configuration Reinitialize";

    /// <summary>
    /// Configuration reinitialization is a medium-priority strategy.
    /// </summary>
    public override int Priority => 20;

    /// <summary>
    /// This strategy can recover from configuration errors.
    /// </summary>
    public override bool CanRecover(IApplicationError error)
    {
        return error.ErrorCode == ErrorCodes.Device.INITIALIZATION_FAILED ||
               error.ErrorCode == ErrorCodes.Device.CONFIGURATION_INVALID ||
               error.ErrorCode == ErrorCodes.Service.CONFIGURATION_INVALID;
    }

    /// <summary>
    /// Attempts to reinitialize the device configuration.
    /// </summary>
    protected override async Task<bool> ExecuteRecoveryAsync(IApplicationError error, CancellationToken ct)
    {
        try
        {
            // Try to retrieve the device
            var device = await _persistenceService.GetPropertyAsync<IIoTDevice>(error.DeviceId, "Device", ct);
            if (device == null)
            {
                Logger.Log($"Device {error.DeviceId} not found for configuration recovery");
                return false;
            }

            // Stop the device if it's running
            if (device.State == ComponentState.Running)
            {
                await device.StopAsync(ct);
            }

            // Load default properties
            var defaultProps = await _persistenceService.GetPropertyAsync<IDictionary<string, object>>(
                error.DeviceId, "DefaultProperties", ct);

            var metadata = await _persistenceService.GetPropertyAsync<IDictionary<string, IPropertyMetadata>>(
                error.DeviceId, "Metadata", ct);

            // If we have default properties, reload them
            if (defaultProps != null)
            {
                await device.LoadPropertiesAsync(defaultProps, metadata);

                // Reinitialize
                await device.InitializeAsync(ct);
                return device.State == ComponentState.Ready;
            }

            // No default properties, just try reinitializing
            await device.InitializeAsync(ct);
            return device.State == ComponentState.Ready;
        }
        catch (Exception ex)
        {
            Logger.Log(ex, $"Error during configuration recovery for device {error.DeviceId}");
            return false;
        }
    }
}