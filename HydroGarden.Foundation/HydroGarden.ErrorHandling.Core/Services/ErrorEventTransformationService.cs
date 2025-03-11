using HydroGarden.Foundation.Abstractions.Interfaces.ErrorEventTransformation;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Foundation.ErrorHandling.Events;
using HydroGarden.Foundation.ErrorHandling.Models;
using HydroGarden.Logger.Abstractions;

namespace HydroGarden.Foundation.ErrorHandling.Services
{
    /// <summary>
    /// Service that handles the conversion between errors and events.
    /// </summary>
    public class ErrorEventTransformationService : IErrorEventTransformationService
    {
        private readonly IEventBus _eventBus;
        private readonly ILogger _logger;
        private static readonly IDictionary<ErrorSeverity, EventPriority> SeverityToPriorityMap = new Dictionary<ErrorSeverity, EventPriority>
        {
            { ErrorSeverity.Warning, EventPriority.Normal },
            { ErrorSeverity.Error, EventPriority.High },
            { ErrorSeverity.Critical, EventPriority.Critical },
            { ErrorSeverity.Catastrophic, EventPriority.Critical }
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="ErrorEventTransformationService"/> class.
        /// </summary>
        /// <param name="eventBus">The event bus to publish events.</param>
        /// <param name="logger">The logger.</param>
        public ErrorEventTransformationService(IEventBus eventBus, ILogger logger)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public IErrorEvent TransformErrorToEvent(IApplicationError error)
        {
            if (error == null)
            {
                throw new ArgumentNullException(nameof(error));
            }

            var errorEvent = new ErrorEvent
            {
                Id = Guid.NewGuid(),
                DeviceId = error.DeviceId,
                ErrorCode = error.ErrorCode ?? "UNKNOWN_ERROR",
                Message = error.Message,
                Timestamp = error.Timestamp,
                Severity = error.Severity,
                Source = error.Source,
                CorrelationId = error.CorrelationId,
                IsTransient = error.IsTransient,
                Context = new Dictionary<string, object>(error.Context)
            };

            if (error.Exception != null)
            {
                errorEvent.ExceptionDetails = error.Exception.ToString();
                errorEvent.ExceptionType = error.Exception.GetType().FullName;
            }

            return errorEvent;
        }

        /// <inheritdoc/>
        public IEvent TransformToPublishableEvent(IErrorEvent errorEvent)
        {
            if (errorEvent == null)
            {
                throw new ArgumentNullException(nameof(errorEvent));
            }

            var publishableEvent = new ErrorOccurredEvent
            {
                DeviceId = errorEvent.DeviceId,
                SourceId = Guid.Empty, // Will be set by the publisher
                ErrorData = errorEvent,
                CorrelationId = errorEvent.CorrelationId
            };

            // Set routing data based on error severity
            var priority = SeverityToPriorityMap.TryGetValue(errorEvent.Severity, out var eventPriority)
                ? eventPriority
                : EventPriority.High;

            publishableEvent.RoutingData = new ErrorEventRoutingData(
                persist: true,
                priority: priority,
                requiresAcknowledgment: errorEvent.Severity >= ErrorSeverity.Critical
            );

            return publishableEvent;
        }

        /// <inheritdoc/>
        public IEvent TransformToPublishableEvent(IRecoveryEvent recoveryEvent)
        {
            if (recoveryEvent == null)
            {
                throw new ArgumentNullException(nameof(recoveryEvent));
            }

            var publishableEvent = new RecoveryAttemptedEvent
            {
                DeviceId = recoveryEvent.DeviceId,
                SourceId = Guid.Empty, // Will be set by the publisher
                RecoveryData = recoveryEvent,
                CorrelationId = recoveryEvent.CorrelationId,
                RoutingData = new ErrorEventRoutingData(
                    persist: true,
                    priority: EventPriority.High,
                    requiresAcknowledgment: false
                )
            };

            return publishableEvent;
        }

        /// <inheritdoc/>
        public IErrorEvent? ExtractErrorEvent(IEvent @event)
        {
            if (@event == null)
            {
                throw new ArgumentNullException(nameof(@event));
            }

            if (@event is ErrorOccurredEvent errorOccurredEvent)
            {
                return errorOccurredEvent.ErrorData;
            }

            return null;
        }

        /// <inheritdoc/>
        public IRecoveryEvent? ExtractRecoveryEvent(IEvent @event)
        {
            if (@event == null)
            {
                throw new ArgumentNullException(nameof(@event));
            }

            if (@event is RecoveryAttemptedEvent recoveryAttemptedEvent)
            {
                return recoveryAttemptedEvent.RecoveryData;
            }

            return null;
        }

        /// <inheritdoc/>
        public async Task PublishErrorAsEventAsync(IApplicationError error, CancellationToken cancellationToken = default)
        {
            if (error == null)
            {
                throw new ArgumentNullException(nameof(error));
            }

            try
            {
                _logger.Log($"Publishing error {error.ErrorCode} as event");
                
                var errorEvent = TransformErrorToEvent(error);
                var publishableEvent = TransformToPublishableEvent(errorEvent);
                
                await _eventBus.PublishAsync(this, publishableEvent, cancellationToken);
                
                _logger.Log($"Successfully published error {error.ErrorCode} as event with ID {publishableEvent.EventId}");
            }
            catch (Exception ex)
            {
                _logger.Log(ex, $"Failed to publish error {error.ErrorCode} as event");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task PublishRecoveryAsEventAsync(
            Guid deviceId,
            string errorCode,
            bool isSuccessful,
            string message,
            Guid correlationId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.Log($"Publishing recovery attempt for error {errorCode} as event");
                
                var recoveryEvent = new RecoveryEvent
                {
                    Id = Guid.NewGuid(),
                    DeviceId = deviceId,
                    ErrorCode = errorCode,
                    Timestamp = DateTimeOffset.UtcNow,
                    IsSuccessful = isSuccessful,
                    Message = message,
                    CorrelationId = correlationId
                };
                
                var publishableEvent = TransformToPublishableEvent(recoveryEvent);
                
                await _eventBus.PublishAsync(this, publishableEvent, cancellationToken);
                
                _logger.Log($"Successfully published recovery for error {errorCode} as event with ID {publishableEvent.EventId}");
            }
            catch (Exception ex)
            {
                _logger.Log(ex, $"Failed to publish recovery for error {errorCode} as event");
                throw;
            }
        }
    }
}