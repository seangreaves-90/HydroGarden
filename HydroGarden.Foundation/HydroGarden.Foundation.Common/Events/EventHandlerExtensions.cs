using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Foundation.Abstractions.Interfaces;

namespace HydroGarden.Foundation.Common.Events
{
    /// <summary>
    /// Extension methods for the IHydroGardenEventHandler interface to support various event types.
    /// </summary>
    public static class EventHandlerExtensions
    {
        /// <summary>
        /// Handles a standard HydroGarden event.
        /// This method should be implemented by consumers to handle specific event types.
        /// </summary>
        public static async Task HandleEventAsync(
            this IHydroGardenEventHandler handler,
            object sender,
            IHydroGardenEvent evt,
            CancellationToken ct = default)
        {
            // Handle based on event type
            switch (evt)
            {
                case IHydroGardenPropertyChangedEvent propertyChangedEvent:
                    await handler.HandleEventAsync(sender, propertyChangedEvent, ct);
                    break;

                case IHydroGardenLifecycleEvent lifecycleEvent:
                    await HandleLifecycleEventAsync(handler, sender, lifecycleEvent, ct);
                    break;

                case IHydroGardenCommandEvent commandEvent:
                    await HandleCommandEventAsync(handler, sender, commandEvent, ct);
                    break;

                case IHydroGardenTelemetryEvent telemetryEvent:
                    await HandleTelemetryEventAsync(handler, sender, telemetryEvent, ct);
                    break;

                case IHydroGardenAlertEvent alertEvent:
                    await HandleAlertEventAsync(handler, sender, alertEvent, ct);
                    break;

                default:
                    await HandleGenericEventAsync(handler, sender, evt, ct);
                    break;
            }
        }

        /// <summary>
        /// Handles a lifecycle event. Override this method to provide custom implementation.
        /// </summary>
        public static Task HandleLifecycleEventAsync(
            this IHydroGardenEventHandler handler,
            object sender,
            IHydroGardenLifecycleEvent evt,
            CancellationToken ct = default)
        {
            // Default implementation does nothing.
            // Handlers should override this method to handle lifecycle events.
            return Task.CompletedTask;
        }

        /// <summary>
        /// Handles a command event. Override this method to provide custom implementation.
        /// </summary>
        public static Task HandleCommandEventAsync(
            this IHydroGardenEventHandler handler,
            object sender,
            IHydroGardenCommandEvent evt,
            CancellationToken ct = default)
        {
            // Default implementation does nothing.
            // Handlers should override this method to handle command events.
            return Task.CompletedTask;
        }

        /// <summary>
        /// Handles a telemetry event. Override this method to provide custom implementation.
        /// </summary>
        public static Task HandleTelemetryEventAsync(
            this IHydroGardenEventHandler handler,
            object sender,
            IHydroGardenTelemetryEvent evt,
            CancellationToken ct = default)
        {
            // Default implementation does nothing.
            // Handlers should override this method to handle telemetry events.
            return Task.CompletedTask;
        }

        /// <summary>
        /// Handles an alert event. Override this method to provide custom implementation.
        /// </summary>
        public static Task HandleAlertEventAsync(
            this IHydroGardenEventHandler handler,
            object sender,
            IHydroGardenAlertEvent evt,
            CancellationToken ct = default)
        {
            // Default implementation does nothing.
            // Handlers should override this method to handle alert events.
            return Task.CompletedTask;
        }

        /// <summary>
        /// Handles a generic event. Override this method to provide custom implementation.
        /// </summary>
        public static Task HandleGenericEventAsync(
            this IHydroGardenEventHandler handler,
            object sender,
            IHydroGardenEvent evt,
            CancellationToken ct = default)
        {
            // Default implementation does nothing.
            // Handlers should override this method to handle generic events.
            return Task.CompletedTask;
        }
    }
}
