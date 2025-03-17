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
    }
}