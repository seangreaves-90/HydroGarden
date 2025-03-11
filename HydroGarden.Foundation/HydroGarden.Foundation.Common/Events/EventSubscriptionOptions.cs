using HydroGarden.Foundation.Abstractions.Interfaces.Events;

namespace HydroGarden.Foundation.Common.Events
{
    /// <summary>
    /// Implementation of event subscription options for filtering events.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="EventSubscriptionOptions"/> class.
    /// </remarks>
    public class EventSubscriptionOptions(Func<IEvent, bool>? filter) : IEventSubscriptionOptions
    {

        /// <summary>
        /// Initializes a new instance of the <see cref="EventSubscriptionOptions"/> class.
        /// </summary>
        public EventSubscriptionOptions() : this (null)
        {
        }

        /// <inheritdoc/>
        public EventType[] EventTypes { get; set; } = [];

        /// <inheritdoc/>
        public Guid[] SourceIds { get; set; } = [];

        /// <inheritdoc/>
        public Func<IEvent, bool>? Filter { get; set; } = filter;

        /// <inheritdoc/>
        public bool IncludeConnectedSources { get; set; } = false;

        /// <inheritdoc/>
        public bool Synchronous { get; set; } = false;
    }
}