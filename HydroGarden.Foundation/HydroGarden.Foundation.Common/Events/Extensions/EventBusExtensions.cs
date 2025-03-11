using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.Abstractions.Interfaces.Events;

namespace HydroGarden.Foundation.Common.Events.Extensions
{
    /// <summary>
    /// Extension methods for the event bus to simplify common subscription scenarios.
    /// </summary>
    public static class EventBusExtensions
    {
        /// <summary>
        /// Subscribes to error events in the system.
        /// </summary>
        /// <param name="eventBus">The event bus to subscribe to.</param>
        /// <param name="handler">The handler to invoke when an error event is received.</param>
        /// <param name="options">Subscription options.</param>
        /// <returns>The subscription ID.</returns>
        public static Guid SubscribeToErrorEvents(
            this IEventBus eventBus,
            Func<IApplicationError, CancellationToken, Task> handler,
            IEventSubscriptionOptions? options = null)
        {
            var wrapper = new ErrorEventHandlerWrapper(handler);
            return eventBus.Subscribe(wrapper, options);
        }

        /// <summary>
        /// Subscribes to recovery events in the system.
        /// </summary>
        /// <param name="eventBus">The event bus to subscribe to.</param>
        /// <param name="handler">The handler to invoke when a recovery event is received.</param>
        /// <param name="options">Subscription options.</param>
        /// <returns>The subscription ID.</returns>
        public static Guid SubscribeToRecoveryEvents(
            this IEventBus eventBus,
            Func<Guid, string, bool, string, Guid, CancellationToken, Task> handler,
            IEventSubscriptionOptions? options = null)
        {
            var wrapper = new RecoveryEventHandlerWrapper(handler);
            return eventBus.Subscribe(wrapper, options);
        }

        /// <summary>
        /// Adapter to convert error events to a simplified handler signature.
        /// </summary>
        private class ErrorEventHandlerWrapper(Func<IApplicationError, CancellationToken, Task> handler) : IEventHandler
        {
            public async Task HandleEventAsync<T>(object? sender, T evt, CancellationToken ct = default) where T : IEvent
            {
                if (evt is IAlertEvent { AlertData: not null } alertEvent &&
                    alertEvent.AlertData.TryGetValue("ApplicationError", out var errorObj) &&
                    errorObj is IApplicationError error)
                {
                    await handler(error, ct);
                }
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }

        /// <summary>
        /// Adapter to convert recovery events to a simplified handler signature.
        /// </summary>
        private class RecoveryEventHandlerWrapper(
            Func<Guid, string, bool, string, Guid, CancellationToken, Task> handler)
            : IEventHandler
        {
            public async Task HandleEventAsync<T>(object? sender, T evt, CancellationToken ct = default) where T : IEvent
            {
                if (evt.EventType == EventType.System && evt is ISystemEvent { EventSubType: "RecoveryAttempt" } systemEvent)
                {
                    // Extract recovery data from event
                    if (systemEvent.EventData.TryGetValue("DeviceId", out var deviceIdObj) && deviceIdObj is Guid deviceId &&
                        systemEvent.EventData.TryGetValue("ErrorCode", out var errorCodeObj) && errorCodeObj is string errorCode &&
                        systemEvent.EventData.TryGetValue("IsSuccessful", out var isSuccessfulObj) && isSuccessfulObj is bool isSuccessful &&
                        systemEvent.EventData.TryGetValue("Message", out var messageObj) && messageObj is string message &&
                        systemEvent.EventData.TryGetValue("CorrelationId", out var correlationIdObj) && correlationIdObj is Guid correlationId)
                    {
                        await handler(deviceId, errorCode, isSuccessful, message, correlationId, ct);
                    }
                }
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// Interface for system events.
    /// </summary>
    public interface ISystemEvent : IEvent
    {
        /// <summary>
        /// Gets the subtype of the system event.
        /// </summary>
        string EventSubType { get; }

        /// <summary>
        /// Gets the event data.
        /// </summary>
        IDictionary<string, object> EventData { get; }
    }
}