namespace HydroGarden.Foundation.Abstractions.Interfaces.Events
{
    /// <summary>
    /// Represents middleware in the event processing pipeline.
    /// </summary>
    public interface IEventMiddleware
    {
        /// <summary>
        /// Gets the unique identifier for the middleware.
        /// </summary>
        Guid Id { get; }

        /// <summary>
        /// Gets the priority of the middleware. Higher values mean higher priority.
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// Processes an event asynchronously.
        /// </summary>
        /// <param name="sender">The sender of the event.</param>
        /// <param name="evt">The event to process.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>The result of processing the event.</returns>
        Task<IMiddlewareProcessingResult> ProcessEventAsync(object? sender, IEvent evt, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Represents the result of middleware processing of an event.
    /// </summary>
    public interface IMiddlewareProcessingResult
    {
        /// <summary>
        /// Gets the event after processing.
        /// </summary>
        IEvent Event { get; }

        /// <summary>
        /// Gets whether the processing was successful.
        /// </summary>
        bool Success { get; }

        /// <summary>
        /// Gets whether further processing should be stopped.
        /// </summary>
        bool ShouldStopProcessing { get; }

        /// <summary>
        /// Gets the exception that occurred during processing, if any.
        /// </summary>
        Exception? Exception { get; }
    }

    /// <summary>
    /// Default implementation of middleware processing result.
    /// </summary>
    public class MiddlewareProcessingResult : IMiddlewareProcessingResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MiddlewareProcessingResult"/> class.
        /// </summary>
        /// <param name="event">The processed event.</param>
        /// <param name="success">Whether processing was successful.</param>
        /// <param name="shouldStopProcessing">Whether further processing should be stopped.</param>
        /// <param name="exception">Any exception that occurred.</param>
        public MiddlewareProcessingResult(IEvent @event, bool success, bool shouldStopProcessing, Exception? exception = null)
        {
            Event = @event;
            Success = success;
            ShouldStopProcessing = shouldStopProcessing;
            Exception = exception;
        }

        /// <inheritdoc/>
        public IEvent Event { get; }

        /// <inheritdoc/>
        public bool Success { get; }

        /// <inheritdoc/>
        public bool ShouldStopProcessing { get; }

        /// <inheritdoc/>
        public Exception? Exception { get; }
    }
}