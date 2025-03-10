using System;
using System.Threading;
using System.Threading.Tasks;

namespace HydroGarden.Foundation.Abstractions.Interfaces.Events
{
    /// <summary>
    /// Defines a pipeline for processing events with middleware components that can transform,
    /// filter, or perform additional operations on events as they flow through the system.
    /// </summary>
    public interface IEventProcessingPipeline
    {
        /// <summary>
        /// Processes an event through the middleware pipeline.
        /// </summary>
        /// <param name="sender">The original sender of the event.</param>
        /// <param name="event">The event to process.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A result indicating the outcome of the event processing.</returns>
        Task<IEventProcessingResult> ProcessEventAsync(object sender, IEvent @event, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Adds a middleware to the pipeline.
        /// </summary>
        /// <param name="middleware">The middleware to add.</param>
        void AddMiddleware(IEventMiddleware middleware);
        
        /// <summary>
        /// Adds a middleware to the pipeline for specific event types.
        /// </summary>
        /// <param name="middleware">The middleware to add.</param>
        /// <param name="eventTypes">The event types to apply this middleware to.</param>
        void AddMiddleware(IEventMiddleware middleware, params EventType[] eventTypes);
        
        /// <summary>
        /// Removes a middleware from the pipeline.
        /// </summary>
        /// <param name="middlewareId">The ID of the middleware to remove.</param>
        /// <returns>True if the middleware was found and removed, false otherwise.</returns>
        bool RemoveMiddleware(Guid middlewareId);
    }

    /// <summary>
    /// Defines the result of processing an event through the pipeline.
    /// </summary>
    public interface IEventProcessingResult
    {
        /// <summary>
        /// Gets a value indicating whether the event was successfully processed.
        /// </summary>
        bool IsSuccess { get; }
        
        /// <summary>
        /// Gets the exception that occurred during processing, if any.
        /// </summary>
        Exception Exception { get; }
        
        /// <summary>
        /// Gets the event after it has been processed through the pipeline.
        /// This may be different from the original event if a middleware transformed it.
        /// </summary>
        IEvent ProcessedEvent { get; }
        
        /// <summary>
        /// Gets a value indicating whether the event should be retried.
        /// </summary>
        bool ShouldRetry { get; }
        
        /// <summary>
        /// Gets the retry count for this event.
        /// </summary>
        int RetryCount { get; }
        
        /// <summary>
        /// Gets the suggested delay before the next retry attempt.
        /// </summary>
        TimeSpan RetryDelay { get; }
    }
}