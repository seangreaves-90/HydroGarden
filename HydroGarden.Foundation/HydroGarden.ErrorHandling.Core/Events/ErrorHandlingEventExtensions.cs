using HydroGarden.ErrorHandling.Core;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorEventTransformation;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Foundation.ErrorHandling.Common;

namespace HydroGarden.Foundation.ErrorHandling.Events
{
    /// <summary>
    /// Extension methods for working with error events.
    /// </summary>
    public static class ErrorHandlingEventExtensions
    {
        /// <summary>
        /// Subscribes to all error events.
        /// </summary>
        /// <param name="eventBus">The event bus.</param>
        /// <param name="handler">The handler for error events.</param>
        /// <returns>The subscription ID.</returns>
        public static Guid SubscribeToErrors(this IEventBus eventBus, IEventHandler handler)
        {
            return eventBus.Subscribe(handler, EventSubscriptionOptions.ForErrorEvents());
        }

        /// <summary>
        /// Subscribes to error events for a specific device.
        /// </summary>
        /// <param name="eventBus">The event bus.</param>
        /// <param name="handler">The handler for error events.</param>
        /// <param name="deviceId">The device ID to filter events for.</param>
        /// <returns>The subscription ID.</returns>
        public static Guid SubscribeToDeviceErrors(this IEventBus eventBus, IEventHandler handler, Guid deviceId)
        {
            return eventBus.Subscribe(handler, EventSubscriptionOptions.ForErrorEvents(deviceId));
        }

        /// <summary>
        /// Subscribes to error events with a minimum severity level.
        /// </summary>
        /// <param name="eventBus">The event bus.</param>
        /// <param name="handler">The handler for error events.</param>
        /// <param name="minimumSeverity">The minimum severity level to receive.</param>
        /// <returns>The subscription ID.</returns>
        public static Guid SubscribeToErrorsBySeverity(
            this IEventBus eventBus,
            IEventHandler handler,
            ErrorSeverity minimumSeverity)
        {
            return eventBus.Subscribe(handler, EventSubscriptionOptions.ForErrorEventsBySeverity(minimumSeverity));
        }

        /// <summary>
        /// Extracts error data from an event if it's an error event.
        /// </summary>
        /// <param name="event">The event to extract from.</param>
        /// <returns>The error data if available, null otherwise.</returns>
        public static IErrorEvent? ExtractErrorData(this IEvent @event)
        {
            if (@event is ErrorOccurredEvent errorEvent)
            {
                return errorEvent.ErrorData;
            }
            return null;
        }

        /// <summary>
        /// Creates an error event ready for publishing.
        /// </summary>
        /// <param name="source">The source of the error.</param>
        /// <param name="deviceId">The device ID associated with the error.</param>
        /// <param name="errorCode">The error code.</param>
        /// <param name="message">The error message.</param>
        /// <param name="severity">The error severity.</param>
        /// <param name="errorSource">The error source.</param>
        /// <param name="exception">The exception that caused the error, if any.</param>
        /// <returns>An event that can be published on the event bus.</returns>
        public static IEvent CreateErrorEvent(
            object source,
            Guid deviceId,
            string errorCode,
            string message,
            ErrorSeverity severity = ErrorSeverity.Error,
            ErrorSource errorSource = ErrorSource.Unknown,
            Exception? exception = null)
        {
            // Create context with source information
            var context = ErrorContextBuilder.Create()
                .WithSource(source)
                .WithDevice(deviceId)
                .WithErrorClassification(errorCode, severity, errorSource, DeriveCategory(errorCode))
                .Build();

            if (exception != null)
            {
                context = ErrorContextBuilder.Create()
                    .WithProperties(context)
                    .WithException(exception)
                    .Build();
            }

            // Create the component error
            var error = new ComponentError(
                deviceId,
                errorCode,
                message,
                severity,
                errorSource,
                context,
                exception);

            // Create the error event
            var errorEvent = ErrorEvent.FromApplicationError(error);

            // Create the event to publish
            var routingData = new ErrorEventRoutingData
            {
                DeviceId = deviceId,
                Severity = severity
            };

            return new ErrorOccurredEvent
            {
                SourceId = deviceId,
                DeviceId = deviceId,
                ErrorData = errorEvent,
                CorrelationId = error.CorrelationId,
                RoutingData = routingData,
                EventType = EventType.Error
            };
        }

        /// <summary>
        /// Derives error category from the error code pattern.
        /// </summary>
        private static ErrorCategory DeriveCategory(string errorCode)
        {
            if (string.IsNullOrEmpty(errorCode))
                return ErrorCategory.Unknown;

            if (errorCode.StartsWith("DEVICE_"))
                return ErrorCategory.Device;
            if (errorCode.StartsWith("SERVICE_"))
                return ErrorCategory.Service;
            if (errorCode.StartsWith("COMM_"))
                return ErrorCategory.Communication;
            if (errorCode.StartsWith("EVENT_"))
                return ErrorCategory.EventSystem;
            if (errorCode.StartsWith("STORAGE_"))
                return ErrorCategory.Storage;

            return ErrorCategory.Unknown;
        }
    }
}