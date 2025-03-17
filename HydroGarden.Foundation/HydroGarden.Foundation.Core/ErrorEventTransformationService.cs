using HydroGarden.Foundation.Abstractions.Interfaces.ErrorEventTransformation;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Foundation.ErrorHandling.Events;
using HydroGarden.Logger.Abstractions;

namespace HydroGarden.Foundation.Core
{
    /// <summary>
    /// Simplified implementation of error event transformation service for use when a full
    /// implementation is not available.
    /// </summary>
    public class ErrorEventTransformationService : IErrorEventTransformationService
    {
        private readonly ILogger _logger;

        /// <summary>
        /// Creates a new error event transformation service.
        /// </summary>
        public ErrorEventTransformationService(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public IErrorEvent? ExtractErrorEvent(IEvent @event)
        {
            if (@event is ErrorOccurredEvent errorEvent)
            {
                _logger.Log($"Extracted error event: {errorEvent.ErrorData.ErrorCode}");
                return errorEvent.ErrorData;
            }
            
            return null;
        }

        /// <inheritdoc />
        public async Task PublishErrorAsEventAsync(IApplicationError error, CancellationToken cancellationToken = default)
        {
            if (error == null)
                throw new ArgumentNullException(nameof(error));
                
            _logger.Log($"Would publish error (simplified implementation): {error.ErrorCode} - {error.Message}");
            
            // Simplified implementation - just log the event since we don't have an event bus
            await Task.CompletedTask;
        }

        /// <inheritdoc />
        public IErrorEvent TransformErrorToEvent(IApplicationError error)
        {
            if (error == null)
                throw new ArgumentNullException(nameof(error));
                
            _logger.Log($"Transforming error to event: {error.ErrorCode}");
            return ErrorEvent.FromApplicationError(error);
        }

        /// <inheritdoc />
        public IEvent TransformToPublishableEvent(IErrorEvent errorEvent)
        {
            if (errorEvent == null)
                throw new ArgumentNullException(nameof(errorEvent));
                
            _logger.Log($"Transforming error event to publishable event: {errorEvent.ErrorCode}");
            
            var routingData = new ErrorEventRoutingData
            {
                DeviceId = errorEvent.DeviceId,
                Severity = errorEvent.Severity
            };
            routingData.SetPriorityFromSeverity();

            return new ErrorOccurredEvent
            {
                SourceId = errorEvent.DeviceId,
                ErrorData = errorEvent,
                CorrelationId = errorEvent.CorrelationId,
                RoutingData = routingData,
                DeviceId = errorEvent.DeviceId,
                EventType = EventType.Error
            };
        }
    }
}