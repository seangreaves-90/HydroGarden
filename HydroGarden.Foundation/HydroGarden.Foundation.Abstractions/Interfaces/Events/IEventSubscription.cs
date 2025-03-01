namespace HydroGarden.Foundation.Abstractions.Interfaces.Events
{
    public interface IEventSubscription
    {
        /// <summary>
        /// Unique identifier for this subscription
        /// </summary>
        public Guid Id { get; }

        /// <summary>
        /// The event handler for this subscription
        /// </summary>
        public IHydroGardenEventHandler Handler { get; }

        /// <summary>
        /// Options that control how events are routed to this subscription
        /// </summary>
        public IEventSubscriptionOptions Options { get; }
    }
}
