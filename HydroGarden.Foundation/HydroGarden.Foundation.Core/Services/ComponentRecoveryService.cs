using HydroGarden.Foundation.Abstractions.Interfaces;
using HydroGarden.Foundation.Abstractions.Interfaces.Components;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.Abstractions.Interfaces.Services;
using HydroGarden.Foundation.Common.Logging;
using System.Collections.Concurrent;

namespace HydroGarden.Foundation.Core.Services
{
    /// <summary>
    /// Enhanced implementation of device recovery service that works with the persistence service
    /// to locate and recover devices that have entered an error state, with improved retry logic.
    /// </summary>
    public class ComponentRecoveryService : IComponentRecoveryService
    {
        private readonly IPersistenceService _persistenceService;
        private readonly Logger _logger;
        private readonly IErrorMonitor _errorMonitor;
        private readonly ConcurrentDictionary<Guid, RecoveryState> _deviceRecoveryStates;

        private class RecoveryState
        {
            public int AttemptCount { get; set; }
            public DateTimeOffset LastAttempt { get; set; }
            public bool IsRecovering { get; set; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ComponentRecoveryService"/> class.
        /// </summary>
        public ComponentRecoveryService(
            IPersistenceService persistenceService,
            Logger logger,
            IErrorMonitor errorMonitor)
        {
            _persistenceService = persistenceService ?? throw new ArgumentNullException(nameof(persistenceService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _errorMonitor = errorMonitor ?? throw new ArgumentNullException(nameof(errorMonitor));
            _deviceRecoveryStates = new ConcurrentDictionary<Guid, RecoveryState>();
        }

        /// <summary>
        /// Attempts to recover a device by reinitializing it through the persistence service.
        /// </summary>
        public async Task<bool> RecoverDeviceAsync(Guid deviceId, CancellationToken ct = default)
        {
            // Get or create recovery state for this device
            var recoveryState = _deviceRecoveryStates.GetOrAdd(deviceId, _ => new RecoveryState());

            // Check if we're already attempting recovery for this device
            if (recoveryState.IsRecovering)
            {
                _logger.Log($"Recovery already in progress for device {deviceId}");
                return false;
            }

            // Check if we need to throttle recovery attempts
            if (recoveryState.AttemptCount > 0)
            {
                // Calculate backoff time: 5s, 10s, 20s, 40s... up to 5 minutes
                var backoffTime = TimeSpan.FromSeconds(
                    Math.Min(300, 5 * Math.Pow(2, recoveryState.AttemptCount)));

                if (DateTimeOffset.UtcNow - recoveryState.LastAttempt < backoffTime)
                {
                    _logger.Log($"Throttling recovery for device {deviceId}, next attempt in {backoffTime - (DateTimeOffset.UtcNow - recoveryState.LastAttempt)}");
                    return false;
                }
            }

            // Set recovery flag to prevent concurrent recovery attempts
            recoveryState.IsRecovering = true;

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

                // Create a new instance of the device with error monitor
                var device = Activator.CreateInstance(deviceType, deviceId, deviceName, _errorMonitor, _logger) as IIoTDevice;
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

                // Update recovery state
                recoveryState.AttemptCount = 0;

                // Get active errors for this device
                var activeErrors = await _errorMonitor.GetActiveErrorsForDeviceAsync(deviceId, ct);

                // Register recovery success for each error
                foreach (var error in activeErrors)
                {
                    if (error.ErrorCode != null)
                    {
                        await _errorMonitor.RegisterRecoveryAttemptAsync(
                            deviceId, error.ErrorCode, true, ct);
                    }
                }

                _logger.Log($"Successfully recovered device {deviceId}");
                return true;
            }
            catch (Exception? ex)
            {
                _logger.Log(ex, $"Error recovering device {deviceId}");

                // Update recovery state to indicate failure
                recoveryState.AttemptCount++;
                recoveryState.LastAttempt = DateTimeOffset.UtcNow;

                return false;
            }
            finally
            {
                // Clear recovery flag
                recoveryState.IsRecovering = false;
            }
        }
    }
}