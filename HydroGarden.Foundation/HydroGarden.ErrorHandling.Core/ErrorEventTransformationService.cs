using HydroGarden.Foundation.Abstractions.Interfaces.ErrorEventTransformation;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Foundation.ErrorHandling.Events;
using HydroGarden.Logger.Abstractions;

namespace HydroGarden.Foundation.ErrorHandling
{
    /// <summary>
    /// Service that handles conversion between errors and events.
    /// </summary>
    /// <param name="eventBus">The event bus for publishing events.</param>
    /// <param name="logger">The logger.</param>
    public class ErrorEventTransformationService(IEventBus eventBus, ILogger logger) : IErrorEventTransformationService
    {
        private readonly IEventBus _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        /// <inheritdoc/>
        public IErrorEvent? ExtractErrorEvent(IEvent @event)
        {
            if (@event is not ErrorOccurredEvent errorEvent)
                return null;

            return errorEvent.ErrorData;
        }

        /// <inheritdoc/>
        public async Task PublishErrorAsEventAsync(IApplicationError error, CancellationToken cancellationToken = default)
        {

            ArgumentNullException.ThrowIfNull(error);

            try
            {
                // Transform error to event
                var errorEvent = TransformErrorToEvent(error);

                // Transform to publishable event
                var publishableEvent = TransformToPublishableEvent(errorEvent);

                // Publish the event
                await _eventBus.PublishAsync(this, publishableEvent, cancellationToken);

                _logger.Log($"Published error as event: {error.ErrorCode}, DeviceId: {error.DeviceId}, Severity: {error.Severity}");
            }
            catch (Exception ex)
            {
                _logger.Log(ex, $"Failed to publish error as event: {error.ErrorCode}, DeviceId: {error.DeviceId}");
                throw;
            }
        }

        /// <inheritdoc/>
        public IErrorEvent TransformErrorToEvent(IApplicationError error)
        {

            ArgumentNullException.ThrowIfNull(error);

            return ErrorEvent.FromApplicationError(error);
        }

        /// <inheritdoc/>
        public IEvent TransformToPublishableEvent(IErrorEvent errorEvent)
        {
            ArgumentNullException.ThrowIfNull(errorEvent);

            var routingData = new ErrorEventRoutingData
            {
                DeviceId = errorEvent.DeviceId,
                Severity = errorEvent.Severity
            };

            var publishableEvent = new ErrorOccurredEvent
            {
                SourceId = errorEvent.DeviceId,
                ErrorData = errorEvent,
                CorrelationId = errorEvent.CorrelationId,
                RoutingData = routingData,
                DeviceId = errorEvent.DeviceId,
                EventType = EventType.Error
            };

            return publishableEvent;
        }
    }
}