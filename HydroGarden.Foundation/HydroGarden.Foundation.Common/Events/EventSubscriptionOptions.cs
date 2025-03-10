using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using System;

namespace HydroGarden.Foundation.Common.Events
{
    /// <summary>
    /// Implementation of event subscription options for filtering events.
    /// </summary>
    public class EventSubscriptionOptions : IEventSubscriptionOptions
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EventSubscriptionOptions"/> class.
        /// </summary>
        public EventSubscriptionOptions()
        {
            EventTypes = new EventType[0];
            SourceIds = new Guid[0];
            IncludeConnectedSources = false;
            Synchronous = false;
        }

        /// <inheritdoc/>
        public EventType[] EventTypes { get; set; }

        /// <inheritdoc/>
        public Guid[] SourceIds { get; set; }

        /// <inheritdoc/>
        public Func<IEvent, bool> Filter { get; set; }

        /// <inheritdoc/>
        public bool IncludeConnectedSources { get; set; }

        /// <inheritdoc/>
        public bool Synchronous { get; set; }
    }
}