using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;

namespace HydroGarden.Foundation.Abstractions.Interfaces.ErrorEventTransformation
{
    /// <summary>
    /// Defines the contract for error events that can be published on the event bus.
    /// </summary>
    public interface IErrorEvent
    {
        /// <summary>
        /// Gets the unique identifier for this error event.
        /// </summary>
        public Guid Id { get; }

        /// <summary>
        /// Gets the device ID associated with this error.
        /// </summary>
        public Guid DeviceId { get; }

        /// <summary>
        /// Gets the error code.
        /// </summary>
        public string ErrorCode { get; }

        /// <summary>
        /// Gets the error message.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Gets the timestamp when the error occurred.
        /// </summary>
        public DateTimeOffset Timestamp { get; }

        /// <summary>
        /// Gets the severity level of the error.
        /// </summary>
        public ErrorSeverity Severity { get; }

        /// <summary>
        /// Gets the source of the error.
        /// </summary>
        public ErrorSource Source { get; }

        /// <summary>
        /// Gets the exception details.
        /// </summary>
        public string? ExceptionDetails { get; }

        /// <summary>
        /// Gets the correlation identifier for tracing.
        /// </summary>
        public Guid CorrelationId { get; }

        /// <summary>
        /// Gets additional contextual information about the error.
        /// </summary>
        public IDictionary<string, object> Context { get; }

        /// <summary>
        /// Gets the type of the original exception.
        /// </summary>
        public string? ExceptionType { get; }
    }
}
