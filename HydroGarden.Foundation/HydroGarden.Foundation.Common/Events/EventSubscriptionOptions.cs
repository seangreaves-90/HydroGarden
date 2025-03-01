using HydroGarden.Foundation.Abstractions.Interfaces.Events;

namespace HydroGarden.Foundation.Common.Events
{
    /// <summary>
    /// Implementation of the subscription options that control which events a subscriber receives.
    /// </summary>
    public class EventSubscriptionOptions : IEventSubscriptionOptions
    {
        /// <inheritdoc/>
        public EventType[] EventTypes { get; set; } = Array.Empty<EventType>();

        /// <inheritdoc/>
        public Guid[] SourceIds { get; set; } = Array.Empty<Guid>();

        /// <inheritdoc/>
        public Func<IHydroGardenEvent, bool>? Filter { get; set; }

        /// <inheritdoc/>
        public bool IncludeConnectedSources { get; set; }

        /// <inheritdoc/>
        public bool Synchronous { get; set; }

        /// <summary>
        /// Creates a new instance of EventSubscriptionOptions with default values.
        /// </summary>
        public EventSubscriptionOptions()
        {
        }

        /// <summary>
        /// Creates a new instance of EventSubscriptionOptions with specified event types.
        /// </summary>
        public EventSubscriptionOptions(params EventType[] eventTypes)
        {
            EventTypes = eventTypes;
        }

        /// <summary>
        /// Creates a builder for fluent configuration of subscription options.
        /// </summary>
        public static EventSubscriptionOptionsBuilder CreateBuilder()
        {
            return new EventSubscriptionOptionsBuilder();
        }
    }

    /// <summary>
    /// Builder class for fluent configuration of event subscription options.
    /// </summary>
    public class EventSubscriptionOptionsBuilder
    {
        private readonly EventSubscriptionOptions _options = new();

        /// <summary>
        /// Configures the event types the subscription is interested in.
        /// </summary>
        public EventSubscriptionOptionsBuilder WithEventTypes(params EventType[] eventTypes)
        {
            _options.EventTypes = eventTypes;
            return this;
        }

        /// <summary>
        /// Configures the source IDs the subscription is interested in.
        /// </summary>
        public EventSubscriptionOptionsBuilder WithSourceIds(params Guid[] sourceIds)
        {
            _options.SourceIds = sourceIds;
            return this;
        }

        /// <summary>
        /// Configures a custom filter function for additional filtering logic.
        /// </summary>
        public EventSubscriptionOptionsBuilder WithFilter(Func<IHydroGardenEvent, bool> filter)
        {
            _options.Filter = filter;
            return this;
        }

        /// <summary>
        /// Configures whether to include events from connected sources.
        /// </summary>
        public EventSubscriptionOptionsBuilder WithConnectedSources(bool includeConnectedSources = true)
        {
            _options.IncludeConnectedSources = includeConnectedSources;
            return this;
        }

        /// <summary>
        /// Configures whether the handler should be called synchronously.
        /// </summary>
        public EventSubscriptionOptionsBuilder WithSynchronousHandling(bool synchronous = true)
        {
            _options.Synchronous = synchronous;
            return this;
        }

        /// <summary>
        /// Builds and returns the configured subscription options.
        /// </summary>
        public EventSubscriptionOptions Build()
        {
            return _options;
        }
    }
}
