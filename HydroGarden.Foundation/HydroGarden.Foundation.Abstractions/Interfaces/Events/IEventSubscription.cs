namespace HydroGarden.Foundation.Abstractions.Interfaces.Events
{
    /// <summary>
    /// Represents a subscription to events in the event bus.
    /// </summary>
    public interface IEventSubscription
    {
        /// <summary>
        /// Gets the unique identifier for this subscription.
        /// </summary>
        Guid Id { get; }

        /// <summary>
        /// Gets the event handler for this subscription.
        /// </summary>
        IEventHandler Handler { get; }

        /// <summary>
        /// Gets the options for this subscription.
        /// </summary>
        IEventSubscriptionOptions Options { get; }
    }
}