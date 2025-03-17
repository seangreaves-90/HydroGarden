namespace HydroGarden.Foundation.Abstractions.Interfaces.Events
{
    /// <summary>
    /// Type-specific event handler for processing events of a specific type
    /// </summary>
    /// <typeparam name="TEvent">The specific event type this handler processes</typeparam>
    public interface IEventHandler<in TEvent> where TEvent : IEvent
    {
        /// <summary>
        /// Handles an event of the specific type
        /// </summary>
        /// <param name="event">The event to handle</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>A task representing the asynchronous operation</returns>
        Task HandleAsync(TEvent @event, CancellationToken ct = default);
    }
}