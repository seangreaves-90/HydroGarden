using HydroGarden.Foundation.Abstractions.Interfaces;

namespace HydroGarden.Foundation.Common.Events
{
    public class EventSubscriptionOptions : IEventSubscriptionOptions
    {
        /// <inheritdoc />
        public EventType[] EventTypes { get; set; } = Array.Empty<EventType>();

        /// <inheritdoc />
        public Guid[] SourceIds { get; set; } = Array.Empty<Guid>();

        /// <inheritdoc />
        public Func<IHydroGardenEvent, bool>? Filter { get; set; }

        /// <inheritdoc />
        public bool IncludeConnectedSources { get; set; } = false;

        /// <inheritdoc />
        public bool Synchronous { get; set; } = false;
    }
}
