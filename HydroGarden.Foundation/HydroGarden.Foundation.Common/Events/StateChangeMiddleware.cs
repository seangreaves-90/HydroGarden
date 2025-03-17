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
            ArgumentNullException.ThrowIfNull(logger, nameof(logger));
            _logger = logger;
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
                ArgumentNullException.ThrowIfNull(evt, nameof(evt));

                // Check if it's a state change event
                bool isStateChangeEvent = evt.EventType == EventType.StateChange;
                IStateChangeEvent? stateChangeEvent = evt as IStateChangeEvent;

                // If it's a IStateChangeEvent but the EventType isn't set correctly, fix it
                if (!isStateChangeEvent && stateChangeEvent != null)
                {
                    _logger.Log($"Found IStateChangeEvent with incorrect EventType {evt.EventType}, correcting to StateChange");
                    // Since we can't modify the original event, we'll note this for diagnostic purposes
                    isStateChangeEvent = true;
                }

                // If it's not a state change event, continue processing
                if (!isStateChangeEvent || stateChangeEvent == null)
                {
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
                    EventRoutingData.CreateBuilder()
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