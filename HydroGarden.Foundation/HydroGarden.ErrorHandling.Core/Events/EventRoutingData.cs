using HydroGarden.Foundation.Abstractions.Interfaces.Events;

namespace HydroGarden.Foundation.ErrorHandling.Events
{
    /// <summary>
    /// Provides routing data for events.
    /// </summary>
    public class EventRoutingData : IEventRoutingData
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EventRoutingData"/> class.
        /// </summary>
        /// <param name="persist">Whether the event should be persisted.</param>
        /// <param name="priority">The priority of the event.</param>
        /// <param name="requiresAcknowledgment">Whether the event requires acknowledgment.</param>
        /// <param name="targetIds">The target IDs for the event.</param>
        public EventRoutingData(
            bool persist = false,
            EventPriority priority = EventPriority.Normal,
            bool requiresAcknowledgment = false,
            params Guid[] targetIds)
        {
            Persist = persist;
            Priority = priority;
            RequiresAcknowledgment = requiresAcknowledgment;
            TargetIds = targetIds;
        }

        /// <inheritdoc/>
        public bool Persist { get; }

        /// <inheritdoc/>
        public EventPriority Priority { get; }

        /// <inheritdoc/>
        public bool RequiresAcknowledgment { get; }
        /// <inheritdoc/>
        public TimeSpan? Timeout { get; set; }

        /// <inheritdoc/>
        public Guid[] TargetIds { get; }
    }
}
