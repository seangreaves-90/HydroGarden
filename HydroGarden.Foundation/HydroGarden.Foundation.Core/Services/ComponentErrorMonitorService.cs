using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.Abstractions.Interfaces.Components;
using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Foundation.Abstractions.Interfaces.Logging;
using HydroGarden.Foundation.Abstractions.Interfaces.Services;
using HydroGarden.Foundation.Common.Events;
using HydroGarden.Foundation.Common.ErrorHandling;
using Microsoft.Extensions.Hosting;
using System.Collections.Concurrent;

namespace HydroGarden.Foundation.Core.Services
{
    public class ComponentErrorMonitoringService(
                                                    ILogger logger,
                                                    IEventBus eventBus,
                                                    IServiceProvider serviceProvider)
                                                    : ErrorMonitorBase(logger), IHostedService, IEventHandler
    {
        private readonly IEventBus _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        private readonly IServiceProvider _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        private Guid _subscriptionId;
        private readonly ConcurrentDictionary<Guid, ComponentErrorInfo> _deviceErrorStats = new();

        #region Component Error Info
        private class ComponentErrorInfo
        {
            public int ErrorCount { get; set; }
            public DateTimeOffset LastErrorTime { get; set; }
            public int RecoveryAttempts { get; set; }
            public DateTimeOffset LastRecoveryTime { get; set; }
            public List<AlertSeverity> RecentSeverities { get; } = [];
        }


        #endregion

        #region Service Methods
        public Task StartAsync(CancellationToken cancellationToken)
        {
            var options = new EventSubscriptionOptions
            {
                EventTypes = new[] { EventType.Alert },
                Synchronous = false
            };
            _subscriptionId = _eventBus.Subscribe(this, options);
            Logger.Log("Component error monitoring service started");
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _eventBus.Unsubscribe(_subscriptionId);
            Logger.Log("Component error monitoring service stopped");
            return Task.CompletedTask;
        }

        public async Task HandleEventAsync<T>(object sender, T evt, CancellationToken ct = default)
            where T : IEvent
        {
            if (evt is IAlertEvent alertEvent)
            {
                var deviceId = alertEvent.DeviceId;
                var errorInfo = _deviceErrorStats.GetOrAdd(deviceId, _ => new ComponentErrorInfo());
                errorInfo.ErrorCount++;
                errorInfo.LastErrorTime = DateTimeOffset.UtcNow;
                errorInfo.RecentSeverities.Add(alertEvent.Severity);

                // Create a ComponentError from the alert event
                string? errorCode = "UNKNOWN_ERROR";
                if (alertEvent.AlertData != null &&
                    alertEvent.AlertData.TryGetValue("ErrorCode", out var codeObj))
                {
                    errorCode = codeObj.ToString();
                }

                var error = new ComponentError(
                    deviceId,
                    errorCode,
                    alertEvent.Message,
                    MapAlertSeverityToErrorSeverity(alertEvent.Severity),
                    alertEvent.AlertData,
                    null // No exception available from alert event
                );

                // Report the error to the base handler
                await ReportErrorAsync(error, ct);

                if (alertEvent.Severity >= AlertSeverity.Error)
                {
                    Logger.Log($"Device {deviceId} reported alert: {alertEvent.Message}");

                    if (!IsUnrecoverableError(errorCode) && ShouldAttemptRecovery(errorInfo))
                    {
                        await AttemptDeviceRecoveryAsync(deviceId, ct);
                        errorInfo.RecoveryAttempts++;
                        errorInfo.LastRecoveryTime = DateTimeOffset.UtcNow;
                    }
                    else
                    {
                        Logger.Log($"Skipping recovery for device {deviceId}: Unrecoverable error or too many attempts");
                    }

                    await PersistErrorAsync(alertEvent, ct);
                }
            }
        }
        #endregion

        #region Helper Methods

      
        private bool IsUnrecoverableError(string? errorCode)
        {
            return errorCode is "HARDWARE_FAILURE" or
                              "RECOVERY_LIMIT_EXCEEDED" or
                              "CONFIGURATION_INVALID";
        }

        private bool ShouldAttemptRecovery(ComponentErrorInfo errorInfo)
        {
            if (errorInfo.RecoveryAttempts >= 3 &&
                (DateTimeOffset.UtcNow - errorInfo.LastRecoveryTime) < TimeSpan.FromMinutes(15))
            {
                return false;
            }

            if ((DateTimeOffset.UtcNow - errorInfo.LastRecoveryTime) > TimeSpan.FromHours(1))
            {
                errorInfo.RecoveryAttempts = 0;
            }

            return true;
        }

        private async Task AttemptDeviceRecoveryAsync(Guid deviceId, CancellationToken ct)
        {
            try
            {
                Logger.Log($"Attempting recovery for device {deviceId}");

                var persistenceService = _serviceProvider.GetService(typeof(IPersistenceService)) as IPersistenceService;
                if (persistenceService == null)
                {
                    Logger.Log($"Cannot recover device {deviceId}: PersistenceService not available");
                    return;
                }

                var deviceProperty = await persistenceService.GetPropertyAsync<IIoTDevice>(deviceId, "Device", ct);
                if (deviceProperty is IIoTDevice device)
                {
                    bool success = await device.TryRecoverAsync(ct);
                    Logger.Log(success
                        ? $"Successfully recovered device {deviceId}"
                        : $"Failed to recover device {deviceId}");
                }
                else
                {
                    if (_serviceProvider.GetService(typeof(IComponentRecoveryService)) is IComponentRecoveryService recoveryService)
                    {
                        bool success = await recoveryService.RecoverDeviceAsync(deviceId, ct);
                        Logger.Log(success
                            ? $"Successfully recovered device {deviceId} via recovery service"
                            : $"Failed to recover device {deviceId} via recovery service");
                    }
                    else
                    {
                        Logger.Log($"No recovery service available for device {deviceId}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex, $"Error occurred while attempting recovery for device {deviceId}");
            }
        }

        private Task PersistErrorAsync(IAlertEvent alertEvent, CancellationToken ct)
        {
            try
            {
                var errorDetails = new Dictionary<string, object>
                {
                    ["DeviceId"] = alertEvent.DeviceId,
                    ["Timestamp"] = alertEvent.Timestamp,
                    ["Severity"] = alertEvent.Severity,
                    ["Message"] = alertEvent.Message,
                    ["Data"] = alertEvent.AlertData ?? new Dictionary<string, object>()
                };

                Logger.Log(errorDetails, "Error details for analysis");
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Logger.Log(ex, "Failed to persist error data");
                return Task.CompletedTask;
            }
        }

        public Dictionary<string, int> AnalyzeErrorPatterns()
        {
            var results = new Dictionary<string, int>();

            foreach (var deviceStats in _deviceErrorStats)
            {
                var deviceId = deviceStats.Key;
                var errorInfo = deviceStats.Value;

                results[$"Device_{deviceId}_ErrorCount"] = errorInfo.ErrorCount;
                results[$"Device_{deviceId}_RecoveryAttempts"] = errorInfo.RecoveryAttempts;

                foreach (var severity in Enum.GetValues<AlertSeverity>())
                {
                    int count = errorInfo.RecentSeverities.Count(s => s == severity);
                    results[$"Device_{deviceId}_{severity}Count"] = count;
                }
            }

            return results;
        }

        private static ErrorSeverity MapAlertSeverityToErrorSeverity(AlertSeverity severity)
        {
            return severity switch
            {
                AlertSeverity.Info => ErrorSeverity.Warning,
                AlertSeverity.Warning => ErrorSeverity.Warning,
                AlertSeverity.Error => ErrorSeverity.Error,
                AlertSeverity.Critical => ErrorSeverity.Critical,
                _ => ErrorSeverity.Warning
            };
        }

        #endregion

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}