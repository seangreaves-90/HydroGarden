using HydroGarden.Foundation.Abstractions.Interfaces;
using HydroGarden.Foundation.Abstractions.Interfaces.Components;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.Abstractions.Interfaces.Services;
using HydroGarden.Foundation.Common.Logging;

namespace HydroGarden.Foundation.Core.Services
{
    /// <summary>
    /// Default implementation of device recovery service that works with the persistence service
    /// to locate and recover devices that have entered an error state.
    /// </summary>
    public class ComponentRecoveryService : IComponentRecoveryService
    {
        private readonly IPersistenceService _persistenceService;
        private readonly Logger _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ComponentRecoveryService"/> class.
        /// </summary>
        public ComponentRecoveryService(
            IPersistenceService persistenceService,
            Logger logger)
        {
            _persistenceService = persistenceService ?? throw new ArgumentNullException(nameof(persistenceService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Attempts to recover a device by reinitializing it through the persistence service.
        /// </summary>
        public async Task<bool> RecoverDeviceAsync(Guid deviceId, CancellationToken ct = default)
        {
            try
            {
                _logger.Log($"Recovery service attempting to recover device {deviceId}");

                // Get the stored device data
                var properties = await _persistenceService.GetPropertyAsync<IDictionary<string, object>>(deviceId, "Properties", ct);

                if (properties == null)
                {
                    _logger.Log($"No properties found for device {deviceId}");
                    return false;
                }

                // Get assembly type to recreate the correct device type
                if (!properties.TryGetValue("AssemblyType", out var assemblyTypeObj) ||
                    assemblyTypeObj is not string assemblyType)
                {
                    _logger.Log($"No assembly type found for device {deviceId}");
                    return false;
                }

                // Get device name
                string deviceName = "Unknown Device";
                if (properties.TryGetValue("Name", out var nameObj) && nameObj is string name)
                {
                    deviceName = name;
                }

                // Recreate the device instance
                var deviceType = Type.GetType(assemblyType);
                if (deviceType == null)
                {
                    _logger.Log($"Could not find type {assemblyType} for device {deviceId}");
                    return false;
                }

                if (!typeof(IIoTDevice).IsAssignableFrom(deviceType))
                {
                    _logger.Log($"Type {assemblyType} is not an IIoTDevice for device {deviceId}");
                    return false;
                }

                // Create a new instance of the device
                var device = Activator.CreateInstance(deviceType, deviceId, deviceName, _logger) as IIoTDevice;
                if (device == null)
                {
                    _logger.Log($"Failed to create device instance of type {assemblyType} for device {deviceId}");
                    return false;
                }

                // Load the device's properties
                var metadata = await _persistenceService.GetPropertyAsync<IDictionary<string, IPropertyMetadata>>(
                    deviceId, "Metadata", ct);

                if (metadata != null)
                {
                    await device.LoadPropertiesAsync(properties, metadata);
                }
                else
                {
                    await device.LoadPropertiesAsync(properties);
                }

                // Initialize the device
                await device.InitializeAsync(ct);

                // Re-register the device with the persistence service
                await _persistenceService.AddOrUpdateAsync(device, ct);

                _logger.Log($"Successfully recovered device {deviceId}");
                return true;
            }
            catch (Exception? ex)
            {
                _logger.Log(ex, $"Error recovering device {deviceId}");
                return false;
            }
        }
    }
}