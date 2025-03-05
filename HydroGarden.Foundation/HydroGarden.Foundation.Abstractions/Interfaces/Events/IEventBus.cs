namespace HydroGarden.Foundation.Abstractions.Interfaces.Events
{
    /// <summary>
    /// Interface for the event bus service
    /// </summary>
    public interface IEventBus
    {
        /// <summary>
        /// Sets the topology service for the event bus
        /// </summary>
        /// <param name="topologyService">The tology service</param>
        void SetTopologyService(ITopologyService topologyService);
        /// <summary>
        /// Subscribes to events with the given handler and options
        /// </summary>
        /// <param name="handler">The event handler</param>
        /// <param name="options">Options that control event filtering</param>
        /// <returns>Subscription ID that can be used to unsubscribe</returns>
        Guid Subscribe<T>(T handler, IEventSubscriptionOptions? options = null) where T : IEventHandler;

        /// <summary>
        /// Unsubscribes from events using the given subscription ID
        /// </summary>
        /// <param name="subscriptionId">The subscription ID to unsubscribe</param>
        /// <returns>True if the subscription was found and removed, false otherwise</returns>
        bool Unsubscribe(Guid subscriptionId);

        /// <summary>
        /// Publishes an event to all relevant subscribers
        /// </summary>
        /// <param name="sender">The object that raised the event</param>
        /// <param name="evt">The event to publish</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Result of the publish operation</returns>
        Task<IPublishResult> PublishAsync(object sender, IEvent evt, CancellationToken ct = default);
    }

    public interface IEventStore
    {
        Task PersistEventAsync(IEvent evt);
        Task<IEvent?> RetrieveFailedEventAsync();
    }

    public interface IEventRetryPolicy
    {
        Task<bool> ShouldRetryAsync(IEvent evt, int attemptCount);
    }

    public interface IEventTransformer
    {
        IEvent Transform(IEvent evt);
    }
}
