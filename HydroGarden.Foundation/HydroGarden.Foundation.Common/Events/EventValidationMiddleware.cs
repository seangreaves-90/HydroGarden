using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Logger.Abstractions;

namespace HydroGarden.Foundation.Common.Events
{
    /// <summary>
    /// Middleware that validates events.
    /// </summary>
    public class EventValidationMiddleware : IEventMiddleware
    {
        private readonly ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventValidationMiddleware"/> class.
        /// </summary>
        /// <param name="logger">The logger to use.</param>
        public EventValidationMiddleware(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            Id = Guid.NewGuid();
        }

        /// <inheritdoc/>
        public Guid Id { get; }

        /// <inheritdoc/>
        public int Priority => 3000; // Higher priority than state change middleware

        /// <inheritdoc/>
        public Task<IMiddlewareProcessingResult> ProcessEventAsync(object? sender, IEvent evt, CancellationToken cancellationToken = default)
        {
            try
            {
                if (evt == null)
                {
                    throw new ArgumentNullException(nameof(evt));
                }

                // Perform basic event validation
                if (evt.EventId == Guid.Empty)
                {
                    throw new ArgumentException("Event ID cannot be empty", nameof(evt));
                }

                if (evt.SourceId == Guid.Empty)
                {
                    throw new ArgumentException("Source ID cannot be empty", nameof(evt));
                }

                if (evt.Timestamp == default)
                {
                    throw new ArgumentException("Event timestamp cannot be default", nameof(evt));
                }

                _logger.Log($"Event {evt.EventId} passed validation");

                return Task.FromResult<IMiddlewareProcessingResult>(
                    new MiddlewareProcessingResult(
                        evt,
                        true,
                        false)); // Continue processing
            }
            catch (Exception ex)
            {
                _logger.Log(ex, $"Event validation failed for event {evt?.EventId}: {ex.Message}");
                
                // Return a result that indicates validation failure and stops processing
                return Task.FromResult<IMiddlewareProcessingResult>(
                    new MiddlewareProcessingResult(
                        evt,
                        false,
                        true,
                        ex));
            }
        }
    }
}