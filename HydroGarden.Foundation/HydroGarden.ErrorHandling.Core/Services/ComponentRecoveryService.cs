//using System.Collections.Concurrent;
//using HydroGarden.ErrorHandling.Core.Abstractions;
//using HydroGarden.ErrorHandling.Core.Abstractions.RecoveryStrategy;
//using HydroGarden.ErrorHandling.Core.Common;

//namespace HydroGarden.ErrorHandling.Core.Services
//{
//    /// <summary>
//    /// Enhanced recovery service for component errors with multiple recovery strategies.
//    /// </summary>
//    public class ComponentRecoveryService : IComponentRecoveryService, IDisposable
//    {
//        private readonly IPersistenceService _persistenceService;
//        private readonly ILogger _logger;
//        private readonly IErrorMonitor _errorMonitor;
//        private readonly ConcurrentDictionary<Guid, RecoveryState> _deviceRecoveryStates = new();
//        private readonly IEnumerable<IRecoveryStrategy> _recoveryStrategies;
//        private readonly RecoveryOrchestrator _recoveryOrchestrator;
//        private readonly SemaphoreSlim _recoveryLock = new(1, 1);
//        private readonly TimeSpan _recoveryTimeout = TimeSpan.FromMinutes(2);
//        private bool _isDisposed;

//        /// <summary>
//        /// Tracks the recovery state of a device.
//        /// </summary>
//        private class RecoveryState
//        {
//            public int AttemptCount { get; set; }
//            public DateTimeOffset LastAttempt { get; set; }
//            public bool IsRecovering { get; set; }
//            public Dictionary<string, int> StrategyAttempts { get; } = new();
//            public List<string> TriedStrategies { get; } = [];
//        }

//        /// <summary>
//        /// Creates a new enhanced component recovery service.
//        /// </summary>
//        public ComponentRecoveryService(
//            IPersistenceService persistenceService,
//            ILogger logger,
//            IErrorMonitor errorMonitor,
//            IEnumerable<IRecoveryStrategy> recoveryStrategies)
//        {
//            _persistenceService = persistenceService ?? throw new ArgumentNullException(nameof(persistenceService));
//            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
//            _errorMonitor = errorMonitor ?? throw new ArgumentNullException(nameof(errorMonitor));
//            _recoveryStrategies = recoveryStrategies?.ToList() ?? throw new ArgumentNullException(nameof(recoveryStrategies));
//            _recoveryOrchestrator = new RecoveryOrchestrator(logger, errorMonitor, recoveryStrategies);

//            logger.Log($"Enhanced Component Recovery Service initialized with {_recoveryStrategies.Count()} strategies");
//        }

//        /// <summary>
//        /// Attempts to recover a device.
//        /// </summary>
//        public async Task<bool> RecoverDeviceAsync(Guid deviceId, CancellationToken ct = default)
//        {
//            // Get the recovery state for this device
//            var recoveryState = _deviceRecoveryStates.GetOrAdd(deviceId, _ => new RecoveryState());

//            // Check if recovery is already in progress
//            if (recoveryState.IsRecovering)
//            {
//                _logger.Log($"Recovery already in progress for device {deviceId}");
//                return false;
//            }

//            // Apply backoff if needed
//            if (recoveryState.AttemptCount > 0)
//            {
//                var backoffTime = TimeSpan.FromSeconds(
//                    Math.Min(300, 5 * Math.Pow(2, recoveryState.AttemptCount)));

//                if (DateTimeOffset.UtcNow - recoveryState.LastAttempt < backoffTime)
//                {
//                    _logger.Log($"Throttling recovery for device {deviceId}, next attempt in " +
//                               $"{(backoffTime - (DateTimeOffset.UtcNow - recoveryState.LastAttempt)).TotalSeconds:0.0} seconds");
//                    return false;
//                }
//            }

//            // Set recovery flag
//            recoveryState.IsRecovering = true;

//            try
//            {
//                await _recoveryLock.WaitAsync(ct);

//                try
//                {
//                    // Track this recovery attempt
//                    recoveryState.AttemptCount++;
//                    recoveryState.LastAttempt = DateTimeOffset.UtcNow;
//                    recoveryState.TriedStrategies.Clear();

//                    _logger.Log($"Attempting to recover device {deviceId} (attempt {recoveryState.AttemptCount})");

//                    // First try to recover via direct device access
//                    var device = await GetDeviceAsync(deviceId, ct);
//                    if (device != null)
//                    {
//                        _logger.Log($"Found device instance {deviceId}, attempting direct recovery");

//                        // Create error to use with the recovery orchestrator
//                        var error = new ComponentError(
//                            deviceId,
//                            ErrorCodes.Device.STATE_TRANSITION_FAILED,
//                            $"Attempting recovery for device {deviceId}",
//                            ErrorSeverity.Error,
//                            true,
//                            ErrorSource.Device,
//                            true,
//                            ErrorContextBuilder.Create()
//                                .WithProperty("DeviceId", deviceId)
//                                .WithOperation("RecoverDevice")
//                                .Build());

//                        // Try recovery via orchestrator with all available strategies
//                        if (await _recoveryOrchestrator.AttemptRecoveryAsync(error, ct))
//                        {
//                            _logger.Log($"Successfully recovered device {deviceId} via orchestrator");
//                            recoveryState.AttemptCount = 0;
//                            return true;
//                        }

//                        // If orchestrator failed, try direct device recovery
//                        _logger.Log($"Orchestrator failed to recover device {deviceId}, trying direct recovery");

//                        if (await device.TryRecoverAsync(ct))
//                        {
//                            _logger.Log($"Successfully recovered device {deviceId} via direct recovery");
//                            recoveryState.AttemptCount = 0;
//                            return true;
//                        }
//                    }
//                    else
//                    {
//                        _logger.Log($"Device instance {deviceId} not found, attempting reconstruction");

//                        // Device not available, attempt to reconstruct it
//                        if (await ReconstructDeviceAsync(deviceId, ct))
//                        {
//                            _logger.Log($"Successfully reconstructed device {deviceId}");
//                            recoveryState.AttemptCount = 0;
//                            return true;
//                        }
//                    }

//                    // All recovery methods failed
//                    _logger.Log($"All recovery methods failed for device {deviceId}");

//                    // If we've exceeded max attempts, report unrecoverable error
//                    if (recoveryState.AttemptCount >= 3)
//                    {
//                        var error = new ComponentError(
//                            deviceId,
//                            ErrorCodes.Recovery.ATTEMPT_LIMIT_REACHED,
//                            $"Device {deviceId} recovery failed after {recoveryState.AttemptCount} attempts",
//                            ErrorSeverity.Critical,
//                            false,
//                            ErrorSource.Device,
//                            false,
//                            ErrorContextBuilder.Create()
//                                .WithProperty("DeviceId", deviceId)
//                                .WithProperty("AttemptCount", recoveryState.AttemptCount)
//                                .WithProperty("TriedStrategies", string.Join(", ", recoveryState.TriedStrategies))
//                                .Build());

//                        await _errorMonitor.ReportErrorAsync(error, ct);
//                    }

//                    return false;
//                }
//                finally
//                {
//                    _recoveryLock.Release();
//                }
//            }
//            catch (Exception ex)
//            {
//                _logger.Log(ex, $"Error recovering device {deviceId}");

//                await _errorMonitor.ReportErrorAsync(
//                    new ComponentError(
//                        deviceId,
//                        ErrorCodes.Recovery.STRATEGY_FAILED,
//                        $"Exception during device recovery for {deviceId}: {ex.Message}",
//                        ErrorSeverity.Error,
//                        false,
//                        ErrorSource.Service,
//                        false,
//                        ErrorContextBuilder.Create()
//                            .WithProperty("DeviceId", deviceId)
//                            .WithOperation("RecoverDevice")
//                            .Build(),
//                        ex),
//                    ct);

//                return false;
//            }
//            finally
//            {
//                recoveryState.IsRecovering = false;
//            }
//        }

//        /// <summary>
//        /// Attempts to reconstruct a device from stored properties.
//        /// </summary>
//        private async Task<bool> ReconstructDeviceAsync(Guid deviceId, CancellationToken ct)
//        {
//            try
//            {
//                _logger.Log($"Attempting to reconstruct device {deviceId}");

//                // Load device properties
//                var properties = await _persistenceService.GetPropertyAsync<IDictionary<string, object>>(
//                    deviceId, "Properties", ct);

//                if (properties == null)
//                {
//                    _logger.Log($"No properties found for device {deviceId}");
//                    return false;
//                }

//                // Check for assembly type
//                if (!properties.TryGetValue("AssemblyType", out var assemblyTypeObj) ||
//                    assemblyTypeObj is not string assemblyType)
//                {
//                    _logger.Log($"No assembly type found for device {deviceId}");
//                    return false;
//                }

//                // Get device name
//                string deviceName = "Unknown Device";
//                if (properties.TryGetValue("Name", out var nameObj) && nameObj is string name)
//                {
//                    deviceName = name;
//                }

//                // Find the device type
//                var deviceType = Type.GetType(assemblyType);
//                if (deviceType == null)
//                {
//                    _logger.Log($"Could not find type {assemblyType} for device {deviceId}");
//                    return false;
//                }

//                // Verify it's an IoT device
//                if (!typeof(IIoTDevice).IsAssignableFrom(deviceType))
//                {
//                    _logger.Log($"Type {assemblyType} is not an IIoTDevice for device {deviceId}");
//                    return false;
//                }

//                // Create a new instance
//                _logger.Log($"Creating new instance of {assemblyType} for device {deviceId}");

//                var device = Activator.CreateInstance(
//                    deviceType, deviceId, deviceName, _errorMonitor, _logger) as IIoTDevice;

//                if (device == null)
//                {
//                    _logger.Log($"Failed to create device instance of type {assemblyType} for device {deviceId}");
//                    return false;
//                }

//                // Load properties and metadata
//                var metadata = await _persistenceService.GetPropertyAsync<IDictionary<string, IPropertyMetadata>>(
//                    deviceId, "Metadata", ct);

//                if (metadata != null)
//                {
//                    await device.LoadPropertiesAsync(properties, metadata);
//                }
//                else
//                {
//                    await device.LoadPropertiesAsync(properties);
//                }

//                // Initialize the device
//                await device.InitializeAsync(ct);

//                // Update persistence
//                await _persistenceService.AddOrUpdateAsync(device, ct);

//                // Clear active errors
//                var activeErrors = await _errorMonitor.GetActiveErrorsForDeviceAsync(deviceId, ct);
//                foreach (var error in activeErrors)
//                {
//                    if (error.ErrorCode != null)
//                    {
//                        await _errorMonitor.MarkErrorHandledAsync(deviceId, error.ErrorCode, ct);
//                    }
//                }

//                _logger.Log($"Successfully reconstructed device {deviceId}");
//                return true;
//            }
//            catch (Exception ex)
//            {
//                _logger.Log(ex, $"Error reconstructing device {deviceId}");
//                return false;
//            }
//        }

//        /// <summary>
//        /// Gets a device instance from the persistence service.
//        /// </summary>
//        private async Task<IIoTDevice?> GetDeviceAsync(Guid deviceId, CancellationToken ct)
//        {
//            try
//            {
//                return await _persistenceService.GetPropertyAsync<IIoTDevice>(deviceId, "Device", ct);
//            }
//            catch (Exception ex)
//            {
//                _logger.Log(ex, $"Error retrieving device {deviceId}");
//                return null;
//            }
//        }

//        /// <summary>
//        /// Disposes resources.
//        /// </summary>
//        public void Dispose()
//        {
//            if (_isDisposed)
//                return;

//            _recoveryLock.Dispose();
//            _isDisposed = true;

//            GC.SuppressFinalize(this);
//        }
//    }
//}