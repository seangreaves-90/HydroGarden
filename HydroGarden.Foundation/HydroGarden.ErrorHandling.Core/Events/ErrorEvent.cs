using HydroGarden.Foundation.Abstractions.Interfaces.ErrorEventTransformation;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;

namespace HydroGarden.Foundation.ErrorHandling.Events
{
    /// <summary>
    /// Represents an error event that can be published on the event bus.
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
    public class ErrorEvent(Guid deviceId, string errorCode, string message, ErrorSeverity severity, ErrorSource source,
        string? exceptionDetails, string? exceptionType, Guid correlationId, IDictionary<string, object> context,
        DateTimeOffset? timestamp = null) : IErrorEvent
    {
        /// <inheritdoc/>
        public Guid Id { get; } = Guid.NewGuid();

        /// <inheritdoc/>
        public Guid DeviceId { get; } = deviceId;

        /// <inheritdoc/>
        public string ErrorCode { get; } = errorCode ?? throw new ArgumentNullException(nameof(errorCode));

        /// <inheritdoc/>
        public string Message { get; } = message ?? throw new ArgumentNullException(nameof(message));

        /// <inheritdoc/>
        public DateTimeOffset Timestamp { get; } = timestamp ?? DateTimeOffset.UtcNow;

        /// <inheritdoc/>
        public ErrorSeverity Severity { get; } = severity;

        /// <inheritdoc/>
        public ErrorSource Source { get; } = source;

        /// <inheritdoc/>
        public string? ExceptionDetails { get; } = exceptionDetails;

        /// <inheritdoc/>
        public Guid CorrelationId { get; } = correlationId;

        /// <inheritdoc/>
        public IDictionary<string, object> Context { get; } = new Dictionary<string, object>(context);

        /// <inheritdoc/>
        public string? ExceptionType { get; } = exceptionType;

        /// <summary>
        /// Creates an error event from an application error.
        /// </summary>
        /// <param name="error">The application error.</param>
        /// <returns>An error event representing the application error.</returns>
        public static ErrorEvent FromApplicationError(IApplicationError error)
        {
            ArgumentNullException.ThrowIfNull(error);

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