using HydroGarden.Foundation.Abstractions.Interfaces.Events;

namespace HydroGarden.Foundation.Common.Events
{
    /// <summary>
    /// Represents routing data for events.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="EventRoutingData"/> class.
    /// </remarks>
    /// <param name="targetIds">The target IDs for the event.</param>
    /// <param name="persist">Whether the event should be persisted.</param>
    /// <param name="priority">The priority of the event.</param>
    /// <param name="requiresAcknowledgment">Whether the event requires acknowledgment.</param>
    /// <param name="timeout">The timeout for processing the event.</param>
    public class EventRoutingData(
        List<Guid>? targetIds = null,
        bool persist = false,
        EventPriority priority = EventPriority.Normal,
        bool requiresAcknowledgment = false,
        TimeSpan? timeout = null) : IEventRoutingData
    {

        /// <summary>
        /// Initializes a new instance of the <see cref="EventRoutingData"/> class with a single target.
        /// </summary>
        /// <param name="targetId">The target ID for the event.</param>
        /// <param name="persist">Whether the event should be persisted.</param>
        /// <param name="priority">The priority of the event.</param>
        /// <param name="requiresAcknowledgment">Whether the event requires acknowledgment.</param>
        /// <param name="timeout">The timeout for processing the event.</param>
        public EventRoutingData(
            Guid targetId,
            bool persist = false,
            EventPriority priority = EventPriority.Normal,
            bool requiresAcknowledgment = false,
            TimeSpan? timeout = null)
            : this([targetId], persist, priority, requiresAcknowledgment, timeout)
        {
        }

        /// <inheritdoc/>
        public List<Guid> TargetIds { get; } = targetIds ?? [];

        /// <inheritdoc/>
        public bool Persist { get; } = persist;

        /// <inheritdoc/>
        public EventPriority Priority { get; } = priority;

        /// <inheritdoc/>
        public bool RequiresAcknowledgment { get; } = requiresAcknowledgment;

        /// <inheritdoc/>
        public TimeSpan? Timeout { get; } = timeout;

        /// <summary>
        /// Creates a new instance of the <see cref="EventRoutingDataBuilder"/> class for building routing data with a fluent interface.
        /// </summary>
        /// <returns>A new builder for creating EventRoutingData.</returns>
        public static EventRoutingDataBuilder CreateBuilder()
        {
            return new EventRoutingDataBuilder();
        }
    }
}