namespace HydroGarden.Foundation.Abstractions.Interfaces.Events
{
    /// <summary>
    /// Defines an event processing pipeline that can process events through middleware components.
    /// </summary>
    public interface IEventProcessingPipeline
    {
        /// <summary>
        /// Adds middleware to the pipeline.
        /// </summary>
        /// <param name="middleware">The middleware to add.</param>
        void AddMiddleware(IEventMiddleware middleware);

        /// <summary>
        /// Adds middleware to the pipeline for specific event types.
        /// </summary>
        /// <param name="middleware">The middleware to add.</param>
        /// <param name="eventTypes">The event types the middleware should process.</param>
        void AddMiddleware(IEventMiddleware middleware, params EventType[]? eventTypes);

        /// <summary>
        /// Removes middleware from the pipeline.
        /// </summary>
        /// <param name="middlewareId">The ID of the middleware to remove.</param>
        /// <returns>True if the middleware was removed, false if not found.</returns>
        bool RemoveMiddleware(Guid middlewareId);

        /// <summary>
        /// Processes an event through the pipeline.
        /// </summary>
        /// <param name="sender">The sender of the event.</param>
        /// <param name="event">The event to process.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>The result of processing the event.</returns>
        Task<IEventProcessingResult> ProcessEventAsync(object? sender, IEvent @event, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Defines the result of processing an event through a pipeline.
    /// </summary>
    public interface IEventProcessingResult
    {
        /// <summary>
        /// Gets the event that was processed.
        /// </summary>
        IEvent Event { get; }

        /// <summary>
        /// Gets the processed event that may have been modified by middleware.
        /// </summary>
        IEvent ProcessedEvent { get; }

        /// <summary>
        /// Gets whether the processing was successful.
        /// </summary>
        bool IsSuccess { get; }

        /// <summary>
        /// Gets whether the event should be retried.
        /// </summary>
        bool ShouldRetry { get; }

        /// <summary>
        /// Gets the exception that occurred during processing, if any.
        /// </summary>
        Exception? Exception { get; }
        
        /// <summary>
        /// Gets the number of retry attempts that have been made.
        /// </summary>
        int RetryCount { get; }
        
        /// <summary>
        /// Gets the delay before the next retry attempt.
        /// </summary>
        TimeSpan RetryDelay { get; }
    }
}