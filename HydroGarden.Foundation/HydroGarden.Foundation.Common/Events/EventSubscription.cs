using HydroGarden.Foundation.Abstractions.Interfaces.Events;


namespace HydroGarden.Foundation.Common.Events
{
    /// <summary>
    /// Represents an event subscription
    /// </summary>
    public class EventSubscription : IEventSubscription
    {
        /// <inheritdoc/>
        public Guid Id { get; }

        /// <inheritdoc/>
        public IHydroGardenEventHandler Handler { get; }

        /// <inheritdoc/>
        public IEventSubscriptionOptions Options { get; }

        /// <summary>
        /// Creates a new event subscription
        /// </summary>
        /// <param name="id">Unique identifier</param>
        /// <param name="handler">Event handler</param>
        /// <param name="options">Subscription options</param>
        public EventSubscription(Guid id, IHydroGardenEventHandler handler, IEventSubscriptionOptions options)
        {
            Id = id;
            Handler = handler ?? throw new ArgumentNullException(nameof(handler));
            Options = (EventSubscriptionOptions)(options ?? new EventSubscriptionOptions());
        }
    }


}
