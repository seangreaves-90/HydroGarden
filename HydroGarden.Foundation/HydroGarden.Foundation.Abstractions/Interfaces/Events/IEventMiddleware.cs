using System;
using System.Threading;
using System.Threading.Tasks;

namespace HydroGarden.Foundation.Abstractions.Interfaces.Events
{
    /// <summary>
    /// Defines a middleware component that can process events in a pipeline.
    /// </summary>
    public interface IEventMiddleware
    {
        /// <summary>
        /// Gets the unique identifier for this middleware.
        /// </summary>
        Guid Id { get; }
        
        /// <summary>
        /// Gets the display name of this middleware.
        /// </summary>
        string Name { get; }
        
        /// <summary>
        /// Gets the order in which this middleware should be executed in the pipeline.
        /// Lower values are executed earlier.
        /// </summary>
        int Order { get; }
        
        /// <summary>
        /// Processes an event and calls the next middleware in the pipeline.
        /// </summary>
        /// <param name="sender">The original sender of the event.</param>
        /// <param name="event">The event to process.</param>
        /// <param name="next">A delegate to the next middleware in the pipeline.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A result indicating the outcome of the event processing.</returns>
        Task<IEventProcessingResult> ProcessAsync(
            object sender, 
            IEvent @event, 
            Func<object, IEvent, CancellationToken, Task<IEventProcessingResult>> next, 
            CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Determines whether this middleware should be applied to the given event.
        /// </summary>
        /// <param name="event">The event to check.</param>
        /// <returns>True if this middleware should be applied, false otherwise.</returns>
        bool ShouldApply(IEvent @event);
    }
}