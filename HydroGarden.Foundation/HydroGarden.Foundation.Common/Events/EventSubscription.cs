using HydroGarden.Foundation.Abstractions.Interfaces.Events;


namespace HydroGarden.Foundation.Common.Events
{
    /// <summary>
    /// Represents an event subscription
    /// </summary>
    /// <param name="id">Unique identifier</param>
    /// <param name="handler">Event handler</param>
    /// <param name="options">Subscription options</param>
    public class EventSubscription(Guid id, IEventHandler<IEvent> handler, IEventSubscriptionOptions options) : IEventSubscription
    {
        /// <inheritdoc/>
        public Guid Id { get; } = id;

        /// <inheritdoc/>
        public IEventHandler<IEvent> Handler { get; } = handler ?? throw new ArgumentNullException(nameof(handler));

        /// <inheritdoc/>
        public IEventSubscriptionOptions Options { get; } = options ?? throw new ArgumentNullException(nameof(options));
    }


}
