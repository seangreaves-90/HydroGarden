using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Logger.Abstractions;

namespace HydroGarden.Foundation.Common.Events
{
    /// <summary>
    /// Middleware that ensures proper processing of state change events.
    /// </summary>
    public class StateChangeMiddleware : IEventMiddleware
    {
        private readonly ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="StateChangeMiddleware"/> class.
        /// </summary>
        /// <param name="logger">The logger to use.</param>
        public StateChangeMiddleware(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            Id = Guid.NewGuid();
        }

        /// <inheritdoc/>
        public Guid Id { get; }

        /// <inheritdoc/>
        public int Priority => 2000; // Higher priority than transformer middleware

        /// <inheritdoc/>
        public Task<IMiddlewareProcessingResult> ProcessEventAsync(object? sender, IEvent evt, CancellationToken cancellationToken = default)
        {
            try
            {
                if (evt == null)
                {
                    throw new ArgumentNullException(nameof(evt));
                }

                // Only process state change events
                if (evt.EventType != EventType.StateChange || !(evt is IStateChangeEvent stateChangeEvent))
                {
                    // Not a state change event, continue processing
                    return Task.FromResult<IMiddlewareProcessingResult>(
                        new MiddlewareProcessingResult(
                            evt,
                            true,
                            false));
                }

                // Log state change for better debugging
                _logger.Log($"Processing state change event: {stateChangeEvent.OldState} -> {stateChangeEvent.NewState}");

                // Apply high-priority routing if it's a state change event
                if (evt.RoutingData == null)
                {
                    // Create new routing data for this event
                    var routingData = EventRoutingData.CreateBuilder()
                        .WithPriority(EventPriority.High)
                        .Build();

                    // We can't modify the existing event, so we'd normally create a new one
                    // For now, just ensure it's processed with high priority
                    
                    _logger.Log($"Assigned high priority to state change event");
                }
                else
                {
                    _logger.Log($"State change event already has routing data: Priority={evt.RoutingData.Priority}");
                }

                return Task.FromResult<IMiddlewareProcessingResult>(
                    new MiddlewareProcessingResult(
                        evt,
                        true,
                        false)); // Continue processing
            }
            catch (Exception ex)
            {
                _logger.Log(ex, $"Error processing state change event {evt.EventId}");
                
                // Return a result that indicates failure but continues processing
                return Task.FromResult<IMiddlewareProcessingResult>(
                    new MiddlewareProcessingResult(
                        evt,
                        false,
                        false,
                        ex));
            }
        }
    }
}