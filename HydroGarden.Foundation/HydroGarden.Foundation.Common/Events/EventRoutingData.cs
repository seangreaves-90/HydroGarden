using HydroGarden.Foundation.Abstractions.Interfaces.Events;

namespace HydroGarden.Foundation.Common.Events
{
    /// <summary>
    /// Represents routing data for events.
    /// </summary>
    public class EventRoutingData : IEventRoutingData
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EventRoutingData"/> class.
        /// </summary>
        /// <param name="targetIds">The target IDs for the event.</param>
        /// <param name="persist">Whether the event should be persisted.</param>
        /// <param name="priority">The priority of the event.</param>
        /// <param name="requiresAcknowledgment">Whether the event requires acknowledgment.</param>
        /// <param name="timeout">The timeout for processing the event.</param>
        public EventRoutingData(
            Guid[]? targetIds = null,
            bool persist = false,
            EventPriority priority = EventPriority.Normal,
            bool requiresAcknowledgment = false,
            TimeSpan? timeout = null)
        {
            TargetIds = targetIds ?? [];
            Persist = persist;
            Priority = priority;
            RequiresAcknowledgment = requiresAcknowledgment;
            Timeout = timeout;
        }

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
        public Guid[] TargetIds { get; }

        /// <inheritdoc/>
        public bool Persist { get; }

        /// <inheritdoc/>
        public EventPriority Priority { get; }

        /// <inheritdoc/>
        public bool RequiresAcknowledgment { get; }

        /// <inheritdoc/>
        public TimeSpan? Timeout { get; }

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