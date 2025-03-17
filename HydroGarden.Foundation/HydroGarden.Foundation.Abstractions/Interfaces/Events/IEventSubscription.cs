namespace HydroGarden.Foundation.Abstractions.Interfaces.Events
{
    /// <summary>
    /// Interface representing a subscription to events
    /// </summary>
    public interface IEventSubscription
    {
        /// <summary>
        /// Unique identifier for the subscription
        /// </summary>
        Guid Id { get; }
        
        /// <summary>
        /// The event handler for this subscription
        /// </summary>
        IEventHandler<IEvent> Handler { get; }
        
        /// <summary>
        /// Options that control event filtering and behavior
        /// </summary>
        IEventSubscriptionOptions Options { get; }
    }

}