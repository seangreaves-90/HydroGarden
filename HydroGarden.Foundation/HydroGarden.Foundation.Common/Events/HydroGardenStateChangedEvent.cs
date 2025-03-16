using HydroGarden.Foundation.Abstractions.Interfaces.Components;
using HydroGarden.Foundation.Abstractions.Interfaces.Events;

namespace HydroGarden.Foundation.Common.Events
{
    /// <summary>
    /// Represents a state change event for a HydroGarden component.
    /// </summary>
    public class HydroGardenStateChangedEvent : IStateChangeEvent
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="HydroGardenStateChangedEvent"/> class.
        /// </summary>
        /// <param name="deviceId">The ID of the component that changed state.</param>
        /// <param name="sourceId">The ID of the source component.</param>
        /// <param name="oldState">The previous state of the component.</param>
        /// <param name="newState">The new state of the component.</param>
        /// <param name="timestamp">The time of the state change.</param>
        /// <param name="routingData">Optional routing data.</param>
        /// <param name="metadata">Optional metadata for the event.</param>
        public HydroGardenStateChangedEvent(
            Guid deviceId,
            Guid sourceId,
            ComponentState oldState,
            ComponentState newState,
            DateTimeOffset timestamp,
            IEventRoutingData? routingData = null,
            IDictionary<string, object>? metadata = null)
        {
            DeviceId = deviceId;
            SourceId = sourceId;
            EventId = Guid.NewGuid();
            Timestamp = timestamp;
            EventType = EventType.StateChange;
            OldState = oldState;
            NewState = newState;
            RoutingData = routingData;
            Metadata = metadata;
        }

        /// <inheritdoc/>
        public Guid DeviceId { get; }

        /// <inheritdoc/>
        public Guid EventId { get; }

        /// <inheritdoc/>
        public DateTimeOffset Timestamp { get; }

        /// <inheritdoc/>
        public Guid SourceId { get; }

        /// <inheritdoc/>
        public EventType EventType { get; }

        /// <inheritdoc/>
        public IEventRoutingData? RoutingData { get; }

        /// <inheritdoc/>
        public IDictionary<string, object>? Metadata { get; }

        /// <summary>
        /// Gets the old state of the component.
        /// </summary>
        public ComponentState OldState { get; }

        /// <summary>
        /// Gets the new state of the component.
        /// </summary>
        public ComponentState NewState { get; }
    }
}
