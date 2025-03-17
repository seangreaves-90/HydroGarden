using HydroGarden.Foundation.Abstractions.Interfaces.Components;
using HydroGarden.Foundation.Abstractions.Interfaces.Events;

namespace HydroGarden.Foundation.Common.Events
{
    /// <summary>
    /// Represents a state change event for a HydroGarden component.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="StateChangedEvent"/> class.
    /// </remarks>
    /// <param name="deviceId">The ID of the component that changed state.</param>
    /// <param name="sourceId">The ID of the source component.</param>
    /// <param name="oldState">The previous state of the component.</param>
    /// <param name="newState">The new state of the component.</param>
    /// <param name="timestamp">The time of the state change.</param>
    /// <param name="routingData">Optional routing data.</param>
    /// <param name="metadata">Optional metadata for the event.</param>
    public class StateChangedEvent(
        Guid deviceId,
        Guid sourceId,
        ComponentState oldState,
        ComponentState newState,
        DateTimeOffset timestamp,
        IEventRoutingData? routingData = null,
        IDictionary<string, object>? metadata = null) : IStateChangeEvent
    {

        /// <inheritdoc/>
        public Guid DeviceId { get; } = deviceId;

        /// <inheritdoc/>
        public Guid EventId { get; } = Guid.NewGuid();

        /// <inheritdoc/>
        public DateTimeOffset Timestamp { get; } = timestamp;

        /// <inheritdoc/>
        public Guid SourceId { get; } = sourceId;

        /// <inheritdoc/>
        public EventType EventType { get; } = EventType.StateChange;

        /// <inheritdoc/>
        public IEventRoutingData? RoutingData { get; } = routingData;

        /// <inheritdoc/>
        public IDictionary<string, object>? Metadata { get; } = metadata;

        /// <summary>
        /// Gets the old state of the component.
        /// </summary>
        public ComponentState OldState { get; } = oldState;

        /// <summary>
        /// Gets the new state of the component.
        /// </summary>
        public ComponentState NewState { get; } = newState;
    }
}
