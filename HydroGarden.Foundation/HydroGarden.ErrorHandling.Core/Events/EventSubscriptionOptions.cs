using HydroGarden.Foundation.Abstractions.Interfaces.Events;

namespace HydroGarden.Foundation.ErrorHandling.Events
{
    /// <summary>
    /// Options for subscribing to events.
    /// </summary>
    public class EventSubscriptionOptions : IEventSubscriptionOptions
    {
        /// <inheritdoc/>
        public EventType[] EventTypes { get; set; } = Array.Empty<EventType>();

        /// <inheritdoc/>
        public Guid[] SourceIds { get; set; } = Array.Empty<Guid>();

        /// <inheritdoc/>
        public Func<IEvent, bool>? Filter { get; set; }

        /// <inheritdoc/>
        public bool IncludeConnectedSources { get; set; }

        /// <inheritdoc/>
        public bool Synchronous { get; set; }

        /// <summary>
        /// Creates subscription options for error events.
        /// </summary>
        /// <param name="deviceId">Optional device ID to filter errors for a specific device.</param>
        /// <returns>Subscription options configured for error events.</returns>
        public static IEventSubscriptionOptions ForErrorEvents(Guid? deviceId = null)
        {
            var options = new EventSubscriptionOptions
            {
                EventTypes = new[] { EventType.Error },
                IncludeConnectedSources = true,
                Synchronous = false
            };

            if (deviceId.HasValue)
            {
                options.SourceIds = new[] { deviceId.Value };
            }

            return options;
        }

        /// <summary>
        /// Creates subscription options for specific error severity.
        /// </summary>
        /// <param name="minimumSeverity">The minimum severity level to receive.</param>
        /// <param name="deviceId">Optional device ID to filter errors for a specific device.</param>
        /// <returns>Subscription options configured for filtered error events.</returns>
        public static IEventSubscriptionOptions ForErrorEventsBySeverity(
            Abstractions.Interfaces.ErrorHandling.ErrorSeverity minimumSeverity,
            Guid? deviceId = null)
        {
            var options = new EventSubscriptionOptions
            {
                EventTypes = new[] { EventType.Error },
                IncludeConnectedSources = true,
                Synchronous = false
            };

            if (deviceId.HasValue)
            {
                options.SourceIds = new[] { deviceId.Value };
            }

            options.Filter = evt =>
            {
                if (evt is ErrorOccurredEvent errorEvent &&
                    errorEvent.ErrorData != null)
                {
                    return errorEvent.ErrorData.Severity >= minimumSeverity;
                }
                return false;
            };

            return options;
        }
    }
}