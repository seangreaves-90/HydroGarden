using HydroGarden.Foundation.Abstractions.Interfaces;

namespace HydroGarden.Foundation.Common.Events
{
    /// <summary>
    /// Contains routing information for events
    /// </summary>
    public class EventRoutingData : IEventRoutingData
    {
        /// <inheritdoc />
        public Guid[] TargetIds { get; set; } = Array.Empty<Guid>();

        /// <inheritdoc />
        public bool Persist { get; set; } = false;

        /// <inheritdoc />
        public EventPriority Priority { get; set; } = EventPriority.Normal;

        /// <inheritdoc />
        public bool RequiresAcknowledgment { get; set; } = false;

        /// <inheritdoc />
        public TimeSpan? Timeout { get; set; }
    }
}
