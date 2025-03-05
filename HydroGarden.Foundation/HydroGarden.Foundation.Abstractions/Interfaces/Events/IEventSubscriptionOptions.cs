namespace HydroGarden.Foundation.Abstractions.Interfaces.Events
{
    public interface IEventSubscriptionOptions
    {
        /// <summary>
        /// Optional filter for specific event types
        /// </summary>
        public EventType[] EventTypes { get; set; }

        /// <summary>
        /// Optional filter for specific source components
        /// </summary>
        public Guid[] SourceIds { get; set; }

        /// <summary>
        /// Custom filter predicate for fine-grained control
        /// </summary>
        public Func<IEvent, bool>? Filter { get; set; }

        /// <summary>
        /// Whether to receive events from all connected components
        /// </summary>
        public bool IncludeConnectedSources { get; set; }

        /// <summary>
        /// Whether to handle events synchronously
        /// </summary>
        public bool Synchronous { get; set; }
    }
}
