using HydroGarden.Foundation.Abstractions.Interfaces.Components;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Foundation.Abstractions.Interfaces.Logging;
using HydroGarden.Foundation.Abstractions.Interfaces.Services;
using HydroGarden.Foundation.Common.Events;
using HydroGarden.Foundation.Common.Logging;
using Microsoft.Extensions.Hosting;
using System.Collections.Concurrent;


namespace HydroGarden.Foundation.Core.Services
{
    /// <summary>
    /// Centralizes error handling and recovery orchestration for the system.
    /// Monitors for alert events and manages device recovery attempts.
    /// </summary>
    public class ErrorMonitoringService : IHostedService, IEventHandler
    {
        private readonly IEventBus _eventBus;
        private readonly Logger _logger;
        private readonly IServiceProvider _serviceProvider;
        private Guid _subscriptionId;
        private readonly ConcurrentDictionary<Guid, DeviceErrorInfo> _deviceErrorStats;

        /// <summary>
        /// Contains error tracking information for a specific device
        /// </summary>
        private class DeviceErrorInfo
        {
            public int ErrorCount { get; set; }
            public DateTimeOffset LastErrorTime { get; set; }
            public int RecoveryAttempts { get; set; }
            public DateTimeOffset LastRecoveryTime { get; set; }
            public List<AlertSeverity> RecentSeverities { get; } = new List<AlertSeverity>();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ErrorMonitoringService"/> class.
        /// </summary>
        public ErrorMonitoringService(
            IEventBus eventBus,
            Logger logger,
            IServiceProvider serviceProvider)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _deviceErrorStats = new ConcurrentDictionary<Guid, DeviceErrorInfo>();
        }

        /// <summary>
        /// Starts the monitoring service and subscribes to alert events.
        /// </summary>
        public Task StartAsync(CancellationToken cancellationToken)
        {
            // Subscribe to all Alert events
            var options = new EventSubscriptionOptions
            {
                EventTypes = new[] { EventType.Alert },
                Synchronous = false
            };

            _subscriptionId = _eventBus.Subscribe(this, options);
            _logger.Log("Error monitoring service started");

            return Task.CompletedTask;
        }

        /// <summary>
        /// Stops the monitoring service and unsubscribes from events.
        /// </summary>
        public Task StopAsync(CancellationToken cancellationToken)
        {
            _eventBus.Unsubscribe(_subscriptionId);
            _logger.Log("Error monitoring service stopped");

            return Task.CompletedTask;
        }

        /// <summary>
        /// Handles alert events by processing errors and orchestrating recovery.
        /// </summary>
        public async Task HandleEventAsync<T>(object sender, T evt, CancellationToken ct = default)
            where T : IEvent
        {
            if (evt is IAlertEvent alertEvent)
            {
                var deviceId = alertEvent.DeviceId;

                // Update error statistics for this device
                var errorInfo = _deviceErrorStats.GetOrAdd(deviceId, _ => new DeviceErrorInfo());
                errorInfo.ErrorCount++;
                errorInfo.LastErrorTime = DateTimeOffset.UtcNow;
                errorInfo.RecentSeverities.Add(alertEvent.Severity);

                // Keep only the last 10 severity records
                if (errorInfo.RecentSeverities.Count > 10)
                {
                    errorInfo.RecentSeverities.RemoveAt(0);
                }

                if (alertEvent.Severity >= AlertSeverity.Error)
                {
                    _logger.Log($"Device {deviceId} reported alert: {alertEvent.Message}");

                    // For errors, attempt recovery if possible
                    if (alertEvent.AlertData != null &&
                        alertEvent.AlertData.TryGetValue("ErrorCode", out var errorCodeObj) &&
                        errorCodeObj is string errorCode)
                    {
                        // Attempt recovery for recoverable errors
                        if (!IsUnrecoverableError(errorCode) && ShouldAttemptRecovery(errorInfo))
                        {
                            await AttemptDeviceRecoveryAsync(deviceId, ct);
                            errorInfo.RecoveryAttempts++;
                            errorInfo.LastRecoveryTime = DateTimeOffset.UtcNow;
                        }
                        else
                        {
                            _logger.Log($"Skipping recovery for device {deviceId}: Unrecoverable error or too many attempts");
                        }
                    }

                    // Log to persistent store for later analysis
                    await PersistErrorAsync(alertEvent, ct);
                }
            }
        }

        /// <summary>
        /// Determines if an error is considered unrecoverable.
        /// </summary>
        private bool IsUnrecoverableError(string errorCode)
        {
            // Define error codes that cannot be automatically recovered
            return errorCode is "HARDWARE_FAILURE" or
                              "RECOVERY_LIMIT_EXCEEDED" or
                              "CONFIGURATION_INVALID";
        }

        /// <summary>
        /// Determines whether to attempt recovery based on recent error history.
        /// </summary>
        private bool ShouldAttemptRecovery(DeviceErrorInfo errorInfo)
        {
            // Don't attempt recovery if we've tried too many times recently
            if (errorInfo.RecoveryAttempts >= 3 &&
                (DateTimeOffset.UtcNow - errorInfo.LastRecoveryTime) < TimeSpan.FromMinutes(15))
            {
                return false;
            }

            // Reset recovery counter if it's been a while
            if ((DateTimeOffset.UtcNow - errorInfo.LastRecoveryTime) > TimeSpan.FromHours(1))
            {
                errorInfo.RecoveryAttempts = 0;
            }

            return true;
        }

        /// <summary>
        /// Attempts to recover a device by finding and using the appropriate recovery service.
        /// </summary>
        private async Task AttemptDeviceRecoveryAsync(Guid deviceId, CancellationToken ct)
        {
            try
            {
                _logger.Log($"Attempting recovery for device {deviceId}");

                // Get persistence service to find the device
                var persistenceService = _serviceProvider.GetService(typeof(IPersistenceService)) as IPersistenceService;
                if (persistenceService == null)
                {
                    _logger.Log($"Cannot recover device {deviceId}: PersistenceService not available");
                    return;
                }

                // First try to get the device directly (if it's in memory)
                var deviceProperty = await persistenceService.GetPropertyAsync<IIoTDevice>(deviceId, "Device", ct);

                if (deviceProperty is IIoTDevice device)
                {
                    // Device is available, try to recover directly
                    bool success = await device.TryRecoverAsync(ct);
                    _logger.Log(success
                        ? $"Successfully recovered device {deviceId}"
                        : $"Failed to recover device {deviceId}");
                }
                else
                {
                    // Try to find a recovery service that can help with this device
                    if (_serviceProvider.GetService(typeof(IComponentRecoveryService)) is IComponentRecoveryService recoveryService)
                    {
                        bool success = await recoveryService.RecoverDeviceAsync(deviceId, ct);
                        _logger.Log(success
                            ? $"Successfully recovered device {deviceId} via recovery service"
                            : $"Failed to recover device {deviceId} via recovery service");
                    }
                    else
                    {
                        _logger.Log($"No recovery service available for device {deviceId}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Log(ex, $"Error occurred while attempting recovery for device {deviceId}");
            }
        }

        /// <summary>
        /// Persists error information for later analysis.
        /// </summary>
        private Task PersistErrorAsync(IAlertEvent alertEvent, CancellationToken ct)
        {
            try
            {
                // Use a simplified error log format for now
                var errorDetails = new Dictionary<string, object>
                {
                    ["DeviceId"] = alertEvent.DeviceId,
                    ["Timestamp"] = alertEvent.Timestamp,
                    ["Severity"] = alertEvent.Severity,
                    ["Message"] = alertEvent.Message,
                    ["Data"] = alertEvent.AlertData ?? new Dictionary<string, object>()
                };

                // Log structured error information
                _logger.Log(errorDetails, "Error details for analysis");

                // TODO: Store in a more permanent/structured way when database is available
                // This could be implemented by finding an IErrorRepository service
                // or by storing to a dedicated error log file

                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.Log(ex, "Failed to persist error data");
                return Task.CompletedTask;
            }
        }

        /// <summary>
        /// Analyzes device error patterns to identify systemic issues.
        /// </summary>
        public Dictionary<string, int> AnalyzeErrorPatterns()
        {
            var results = new Dictionary<string, int>();

            // Count errors by type
            foreach (var deviceStats in _deviceErrorStats)
            {
                var deviceId = deviceStats.Key;
                var errorInfo = deviceStats.Value;

                results[$"Device_{deviceId}_ErrorCount"] = errorInfo.ErrorCount;
                results[$"Device_{deviceId}_RecoveryAttempts"] = errorInfo.RecoveryAttempts;

                // Count by severity
                foreach (var severity in Enum.GetValues<AlertSeverity>())
                {
                    int count = errorInfo.RecentSeverities.Count(s => s == severity);
                    results[$"Device_{deviceId}_{severity}Count"] = count;
                }
            }

            return results;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}