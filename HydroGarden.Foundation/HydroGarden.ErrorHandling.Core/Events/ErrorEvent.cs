using HydroGarden.Foundation.Abstractions.Interfaces.ErrorEventTransformation;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;

namespace HydroGarden.Foundation.ErrorHandling.Events
{
    /// <summary>
    /// Represents an error event that can be published on the event bus.
    /// </summary>
    public class ErrorEvent : IErrorEvent
    {
        /// <inheritdoc/>
        public Guid Id { get; }

        /// <inheritdoc/>
        public Guid DeviceId { get; }

        /// <inheritdoc/>
        public string ErrorCode { get; }

        /// <inheritdoc/>
        public string Message { get; }

        /// <inheritdoc/>
        public DateTimeOffset Timestamp { get; }

        /// <inheritdoc/>
        public ErrorSeverity Severity { get; }

        /// <inheritdoc/>
        public ErrorSource Source { get; }

        /// <inheritdoc/>
        public string? ExceptionDetails { get; }

        /// <inheritdoc/>
        public Guid CorrelationId { get; }

        /// <inheritdoc/>
        public IDictionary<string, object> Context { get; }

        /// <inheritdoc/>
        public string? ExceptionType { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ErrorEvent"/> class.
        /// </summary>
        /// <param name="deviceId">The device ID.</param>
        /// <param name="errorCode">The error code.</param>
        /// <param name="message">The error message.</param>
        /// <param name="severity">The error severity.</param>
        /// <param name="source">The error source.</param>
        /// <param name="exceptionDetails">The exception details.</param>
        /// <param name="exceptionType">The exception type.</param>
        /// <param name="correlationId">The correlation ID.</param>
        /// <param name="context">The context dictionary.</param>
        /// <param name="timestamp">The timestamp.</param>
        public ErrorEvent(
            Guid deviceId,
            string errorCode,
            string message,
            ErrorSeverity severity,
            ErrorSource source,
            string? exceptionDetails,
            string? exceptionType,
            Guid correlationId,
            IDictionary<string, object> context,
            DateTimeOffset? timestamp = null)
        {
            Id = Guid.NewGuid();
            DeviceId = deviceId;
            ErrorCode = errorCode ?? throw new ArgumentNullException(nameof(errorCode));
            Message = message ?? throw new ArgumentNullException(nameof(message));
            Severity = severity;
            Source = source;
            ExceptionDetails = exceptionDetails;
            ExceptionType = exceptionType;
            CorrelationId = correlationId;
            Context = new Dictionary<string, object>(context ?? new Dictionary<string, object>());
            Timestamp = timestamp ?? DateTimeOffset.UtcNow;
        }

        /// <summary>
        /// Creates an error event from an application error.
        /// </summary>
        /// <param name="error">The application error.</param>
        /// <returns>An error event representing the application error.</returns>
        public static ErrorEvent FromApplicationError(IApplicationError error)
        {
            if (error == null)
                throw new ArgumentNullException(nameof(error));

            string? exceptionDetails = null;
            string? exceptionType = null;

            if (error.Exception != null)
            {
                exceptionDetails = error.Exception.ToString();
                exceptionType = error.Exception.GetType().FullName;
            }

            return new ErrorEvent(
                error.DeviceId,
                error.ErrorCode ?? "UNKNOWN_ERROR",
                error.Message,
                error.Severity,
                error.Source,
                exceptionDetails,
                exceptionType,
                error.CorrelationId,
                error.Context);
        }
    }
}