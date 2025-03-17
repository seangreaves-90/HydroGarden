using HydroGarden.Foundation.Abstractions.Interfaces.Events;

namespace HydroGarden.Foundation.ErrorHandling.Events
{
    /// <summary>
    /// Provides routing data for events.
    /// </summary>
    /// <param name="targetIds">The target IDs for the event.</param>
    /// <param name="persist">Whether the event should be persisted.</param>
    /// <param name="priority">The priority of the event.</param>
    /// <param name="requiresAcknowledgment">Whether the event requires acknowledgment.</param>
    public class EventRoutingData(List<Guid> targetIds, bool persist = false, 
        EventPriority priority = EventPriority.Normal, bool requiresAcknowledgment = false) : IEventRoutingData
    {
        /// <inheritdoc/>
        public bool Persist { get; } = persist;

        /// <inheritdoc/>
        public EventPriority Priority { get; } = priority;

        /// <inheritdoc/>
        public bool RequiresAcknowledgment { get; } = requiresAcknowledgment;
        /// <inheritdoc/>
        public TimeSpan? Timeout { get; set; }

        /// <inheritdoc/>
        public List<Guid> TargetIds { get; } = targetIds;
    }
}
