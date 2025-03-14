using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.ErrorHandling.Common;

namespace HydroGarden.Foundation.ErrorHandling.Exceptions
{
    /// <summary>
    /// Exception thrown when event publication fails.
    /// </summary>
    public class EventPublicationException : ApplicationException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EventPublicationException"/> class.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="eventType">The type of the event that failed to publish.</param>
        /// <param name="eventId">The ID of the event.</param>
        /// <param name="innerException">The inner exception.</param>
        public EventPublicationException(
            string message,
            string eventType,
            Guid eventId,
            Exception? innerException = null)
            : base(
                message,
                ErrorCodes.Event.PUBLICATION_FAILED,
                ErrorSeverity.Error,
                ErrorSource.Service,
                innerException,
                null,
                new Dictionary<string, object>
                {
                    { "EventType", eventType },
                    { "EventId", eventId.ToString() }
                },
                ErrorCategory.EventSystem)
        {
        }
    }

    /// <summary>
    /// Exception thrown when an event subscription fails.
    /// </summary>
    public class EventSubscriptionException : ApplicationException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EventSubscriptionException"/> class.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="eventType">The type of the event for the subscription.</param>
        /// <param name="subscriberId">The ID of the subscriber.</param>
        /// <param name="innerException">The inner exception.</param>
        public EventSubscriptionException(
            string message,
            string eventType,
            string subscriberId,
            Exception? innerException = null)
            : base(
                message,
                ErrorCodes.Event.SUBSCRIPTION_ERROR,
                ErrorSeverity.Error,
                ErrorSource.Service,
                innerException,
                null,
                new Dictionary<string, object>
                {
                    { "EventType", eventType },
                    { "SubscriberId", subscriberId }
                },
                ErrorCategory.EventSystem)
        {
        }
    }

    /// <summary>
    /// Exception thrown when an event handler fails.
    /// </summary>
    public class EventHandlerException : ApplicationException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EventHandlerException"/> class.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="eventType">The type of the event being handled.</param>
        /// <param name="handlerName">The name of the handler that failed.</param>
        /// <param name="eventId">The ID of the event.</param>
        /// <param name="innerException">The inner exception.</param>
        public EventHandlerException(
            string message,
            string eventType,
            string handlerName,
            Guid eventId,
            Exception? innerException = null)
            : base(
                message,
                ErrorCodes.Event.HANDLER_EXCEPTION,
                ErrorSeverity.Error,
                ErrorSource.Service,
                innerException,
                null,
                new Dictionary<string, object>
                {
                    { "EventType", eventType },
                    { "HandlerName", handlerName },
                    { "EventId", eventId.ToString() }
                },
                ErrorCategory.EventSystem)
        {
        }
    }

    /// <summary>
    /// Exception thrown when event routing fails.
    /// </summary>
    public class EventRoutingException : ApplicationException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EventRoutingException"/> class.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="eventType">The type of the event that failed to route.</param>
        /// <param name="eventId">The ID of the event.</param>
        /// <param name="destination">The intended destination.</param>
        /// <param name="innerException">The inner exception.</param>
        public EventRoutingException(
            string message,
            string eventType,
            Guid eventId,
            string destination,
            Exception? innerException = null)
            : base(
                message,
                ErrorCodes.Event.ROUTING_ERROR,
                ErrorSeverity.Error,
                ErrorSource.Service,
                innerException,
                null,
                new Dictionary<string, object>
                {
                    { "EventType", eventType },
                    { "EventId", eventId.ToString() },
                    { "Destination", destination }
                },
                ErrorCategory.EventSystem)
        {
        }
    }

    /// <summary>
    /// Exception thrown when event processing times out.
    /// </summary>
    public class EventProcessingTimeoutException : ApplicationException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EventProcessingTimeoutException"/> class.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="eventType">The type of the event that timed out.</param>
        /// <param name="eventId">The ID of the event.</param>
        /// <param name="processorName">The name of the processor.</param>
        /// <param name="timeout">The timeout value in milliseconds.</param>
        /// <param name="innerException">The inner exception.</param>
        public EventProcessingTimeoutException(
            string message,
            string eventType,
            Guid eventId,
            string processorName,
            int timeout,
            Exception? innerException = null)
            : base(
                message,
                ErrorCodes.Event.PROCESSING_TIMEOUT,
                ErrorSeverity.Error,
                ErrorSource.Service,
                innerException,
                null,
                new Dictionary<string, object>
                {
                    { "EventType", eventType },
                    { "EventId", eventId.ToString() },
                    { "ProcessorName", processorName },
                    { "TimeoutMs", timeout }
                },
                ErrorCategory.EventSystem)
        {
        }
    }
}