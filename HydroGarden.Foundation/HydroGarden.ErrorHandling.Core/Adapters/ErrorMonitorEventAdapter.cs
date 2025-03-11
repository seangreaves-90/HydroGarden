using HydroGarden.Foundation.Abstractions.Interfaces.ErrorEventTransformation;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Foundation.Common.Events;
using HydroGarden.Foundation.Common.Events.Extensions;
using HydroGarden.Logger.Abstractions;

namespace HydroGarden.Foundation.ErrorHandling.Adapters
{
    /// <summary>
    /// Adapter that connects the error monitoring system to the event system,
    /// enabling bidirectional flow of errors and recovery information.
    /// </summary>
    public class ErrorMonitorEventAdapter : IDisposable
    {
        private readonly IErrorMonitor _errorMonitor;
        private readonly IErrorEventTransformationService _transformationService;
        private readonly IEventBus _eventBus;
        private readonly ILogger _logger;
        private Guid _errorSubscriptionId;
        private Guid _recoverySubscriptionId;
        private bool _isDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="ErrorMonitorEventAdapter"/> class.
        /// </summary>
        /// <param name="errorMonitor">The error monitor to adapt.</param>
        /// <param name="transformationService">The error-event transformation service.</param>
        /// <param name="eventBus">The event bus.</param>
        /// <param name="logger">The logger.</param>
        public ErrorMonitorEventAdapter(
            IErrorMonitor errorMonitor,
            IErrorEventTransformationService transformationService,
            IEventBus eventBus,
            ILogger logger)
        {
            _errorMonitor = errorMonitor ?? throw new ArgumentNullException(nameof(errorMonitor));
            _transformationService = transformationService ?? throw new ArgumentNullException(nameof(transformationService));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Initializes the adapter by setting up the event subscriptions.
        /// </summary>
        public void Initialize()
        {
            _logger.Log("Initializing ErrorMonitorEventAdapter");

            // Subscribe to errors in the error monitor
            _errorMonitor.SubscribeToErrorsAsync(
                async (error) => await HandleErrorAsync(error),
                null, 
                CancellationToken.None).ContinueWith(t => 
                {
                    if (t.IsCompletedSuccessfully)
                    {
                        _logger.Log($"Successfully subscribed to error monitor with ID {t.Result}");
                    }
                    else
                    {
                        _logger.Log(t.Exception, "Failed to subscribe to error monitor");
                    }
                });

            // Subscribe to error events in the event bus
            var errorSubscriptionOptions = new EventSubscriptionOptions
            {
                EventTypes = new[] { EventType.Alert }
            };

            _errorSubscriptionId = _eventBus.SubscribeToErrorEvents(
                HandleErrorEventAsync,
                errorSubscriptionOptions);

            // Subscribe to recovery events in the event bus
            var recoverySubscriptionOptions = new EventSubscriptionOptions
            {
                EventTypes = new[] { EventType.System }
            };

            _recoverySubscriptionId = _eventBus.SubscribeToRecoveryEvents(
                HandleRecoveryEventAsync,
                recoverySubscriptionOptions);

            _logger.Log("ErrorMonitorEventAdapter initialized successfully");
        }

        /// <summary>
        /// Handles an error from the error monitor by publishing it as an event.
        /// </summary>
        /// <param name="error">The error to handle.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task HandleErrorAsync(IApplicationError error)
        {
            try
            {
                await _transformationService.PublishErrorAsEventAsync(error);
            }
            catch (Exception ex)
            {
                _logger.Log(ex, $"Failed to handle error {error.ErrorCode} from error monitor");
            }
        }

        /// <summary>
        /// Handles an error event from the event bus by reporting it to the error monitor.
        /// </summary>
        /// <param name="error">The error to handle.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task HandleErrorEventAsync(IApplicationError error, CancellationToken cancellationToken)
        {
            try
            {
                // Only report to the error monitor if it's not already there (check by correlation ID)
                var existingErrors = await _errorMonitor.GetErrorsByCorrelationIdAsync(error.CorrelationId, cancellationToken);
                
                if (existingErrors.Count == 0)
                {
                    await _errorMonitor.ReportErrorAsync(error, cancellationToken);
                    _logger.Log($"Reported error event {error.ErrorCode} to error monitor");
                }
                else
                {
                    _logger.Log($"Error {error.ErrorCode} already exists in error monitor, skipping report");
                }
            }
            catch (Exception ex)
            {
                _logger.Log(ex, $"Failed to handle error event {error.ErrorCode} from event bus");
            }
        }

        /// <summary>
        /// Handles a recovery event from the event bus by registering it with the error monitor.
        /// </summary>
        /// <param name="deviceId">The device ID.</param>
        /// <param name="errorCode">The error code.</param>
        /// <param name="isSuccessful">Whether the recovery was successful.</param>
        /// <param name="message">The recovery message.</param>
        /// <param name="correlationId">The correlation ID.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task HandleRecoveryEventAsync(
            Guid deviceId,
            string errorCode,
            bool isSuccessful,
            string message,
            Guid correlationId,
            CancellationToken cancellationToken)
        {
            try
            {
                await _errorMonitor.RegisterRecoveryAttemptAsync(
                    deviceId,
                    errorCode,
                    isSuccessful,
                    cancellationToken);

                if (isSuccessful)
                {
                    await _errorMonitor.MarkErrorHandledAsync(
                        deviceId,
                        errorCode,
                        cancellationToken);
                }

                _logger.Log($"Registered recovery attempt for error {errorCode} with monitor, success: {isSuccessful}");
            }
            catch (Exception ex)
            {
                _logger.Log(ex, $"Failed to handle recovery event for error {errorCode} from event bus");
            }
        }

        /// <summary>
        /// Disposes the adapter and unsubscribes from events.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;

            try
            {
                // Unsubscribe from the event bus
                if (_errorSubscriptionId != Guid.Empty)
                {
                    _eventBus.Unsubscribe(_errorSubscriptionId);
                }

                if (_recoverySubscriptionId != Guid.Empty)
                {
                    _eventBus.Unsubscribe(_recoverySubscriptionId);
                }

                // Unsubscribe from the error monitor
                // (Assuming there's a way to unsubscribe, add code here)

                _logger.Log("ErrorMonitorEventAdapter disposed successfully");
            }
            catch (Exception ex)
            {
                _logger.Log(ex, "Error disposing ErrorMonitorEventAdapter");
            }

            _isDisposed = true;
        }
    }
}