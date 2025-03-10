using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using System;

namespace HydroGarden.Foundation.ErrorHandling.Core.Events
{
    /// <summary>
    /// Implementation of event routing data for error events.
    /// </summary>
    public class ErrorEventRoutingData : IEventRoutingData
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ErrorEventRoutingData"/> class.
        /// </summary>
        /// <param name="targetIds">The target IDs for routing.</param>
        /// <param name="persist">Whether the event should be persisted.</param>
        /// <param name="priority">The priority of the event.</param>
        /// <param name="requiresAcknowledgment">Whether the event requires acknowledgment.</param>
        /// <param name="timeout">The timeout for processing the event.</param>
        public ErrorEventRoutingData(
            Guid[] targetIds = null,
            bool persist = true,
            EventPriority priority = EventPriority.High,
            bool requiresAcknowledgment = false,
            TimeSpan? timeout = null)
        {
            TargetIds = targetIds ?? Array.Empty<Guid>();
            Persist = persist;
            Priority = priority;
            RequiresAcknowledgment = requiresAcknowledgment;
            Timeout = timeout;
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
    }
}