namespace HydroGarden.Foundation.Abstractions.Interfaces.Events
{
    /// <summary>
    /// Interface for event persistence storage
    /// </summary>
    public interface IEventStore
    {
        /// <summary>
        /// Persists an event for later processing or retry
        /// </summary>
        /// <param name="evt">The event to persist</param>
        /// <returns>A task representing the asynchronous operation</returns>
        Task PersistEventAsync(IEvent evt);

        /// <summary>
        /// Retrieves a failed event for retry processing
        /// </summary>
        /// <returns>The event to retry, or null if none</returns>
        Task<IEvent?> RetrieveFailedEventAsync();
    }
}