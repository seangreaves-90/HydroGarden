namespace HydroGarden.Foundation.Abstractions.Interfaces.Events
{
    /// <summary>
    /// Base interface for event handlers
    /// </summary>
    public interface IEventHandler : IAsyncDisposable
    {
        /// <summary>
        /// Handles any event type
        /// </summary>
        /// <typeparam name="T">The event type</typeparam>
        /// <param name="sender">The sender that raised the event</param>
        /// <param name="evt">The event to handle</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>A task representing the asynchronous operation</returns>
        Task HandleEventAsync<T>(object? sender, T evt, CancellationToken ct = default) where T : IEvent;
    }
}