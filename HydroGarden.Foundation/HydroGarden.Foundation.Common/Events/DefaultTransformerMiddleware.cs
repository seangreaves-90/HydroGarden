using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Logger.Abstractions;

namespace HydroGarden.Foundation.Common.Events
{
    /// <summary>
    /// Default middleware that handles event transformation.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="DefaultTransformerMiddleware"/> class.
    /// </remarks>
    /// <param name="transformer">The event transformer to use.</param>
    /// <param name="logger">The logger to use.</param>
    public class DefaultTransformerMiddleware(IEventTransformer transformer, ILogger logger) : IEventMiddleware
    {
        private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private readonly IEventTransformer _transformer = transformer ?? throw new ArgumentNullException(nameof(transformer));

        /// <inheritdoc/>
        public Guid Id { get; } = Guid.NewGuid();

        /// <inheritdoc/>
        public int Priority => 2500; // Higher priority than state change middleware (2000) but lower than validation (3000)

        /// <inheritdoc/>
        public Task<IMiddlewareProcessingResult> ProcessEventAsync(object? sender, IEvent evt, CancellationToken cancellationToken = default)
        {
            try
            {

                ArgumentNullException.ThrowIfNull(evt);

                // Transform the event
                var transformedEvent = _transformer.Transform(evt);

                // Log the transformation details
                _logger.Log($"Event {evt.EventId} transformed by {_transformer.GetType().Name}");

                // If the event type changed, log it
                if (transformedEvent.EventType != evt.EventType)
                {
                    _logger.Log($"Event type changed from {evt.EventType} to {transformedEvent.EventType}");
                }

                // Ensure the transformer didn't change the event ID - very important
                if (transformedEvent.EventId != evt.EventId)
                {
                    _logger.Log($"Warning: Transformer changed event ID from {evt.EventId} to {transformedEvent.EventId}. This may cause issues.");
                }

                return Task.FromResult<IMiddlewareProcessingResult>(
                    new MiddlewareProcessingResult(
                        transformedEvent,
                        true,
                        false)); // Continue processing with the transformed event
            }
            catch (Exception ex)
            {
                if (evt != null)
                {
                    _logger.Log(ex, $"Error transforming event {evt.EventId}");
                }
                // Return a result that indicates failure
                return Task.FromResult<IMiddlewareProcessingResult>(
                    new MiddlewareProcessingResult(
                        evt ?? throw new ArgumentNullException(nameof(evt)),
                        false,
                        true,
                        ex)); // Stop processing due to error
            }
        }
    }
}