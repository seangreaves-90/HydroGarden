using HydroGarden.Foundation.Abstractions.Interfaces.ErrorEventTransformation;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;

namespace HydroGarden.Foundation.ErrorHandling.Models
{
    /// <summary>
    /// Represents an error that has been converted to an event.
    /// </summary>
    public class ErrorEvent(
        Guid id,
        Guid deviceId,
        string errorCode,
        string message,
        DateTimeOffset timestamp,
        ErrorSeverity severity,
        ErrorSource source,
        string exceptionDetails,
        Guid correlationId,
        bool isTransient,
        string? exceptionType)
        : IErrorEvent
    {
        /// <inheritdoc />
        public Guid Id { get; set; } = id;

        /// <inheritdoc />
        public Guid DeviceId { get; set; } = deviceId;

        /// <inheritdoc />
        public string ErrorCode { get; set; } = errorCode;

        /// <inheritdoc />
        public string Message { get; set; } = message;

        /// <inheritdoc />
        public DateTimeOffset Timestamp { get; set; } = timestamp;

        /// <inheritdoc />
        public ErrorSeverity Severity { get; set; } = severity;

        /// <inheritdoc />
        public ErrorSource Source { get; set; } = source;

        /// <inheritdoc />
        public string ExceptionDetails { get; set; } = exceptionDetails;

        /// <inheritdoc />
        public Guid CorrelationId { get; set; } = correlationId;

        /// <inheritdoc />
        public Dictionary<string, object>? Context { get; set; } = new();

        /// <inheritdoc />
        public bool IsTransient { get; set; } = isTransient;

        /// <inheritdoc />
        public string? ExceptionType { get; set; } = exceptionType;
    }

    /// <summary>
    /// Represents an event indicating a recovery attempt for a previous error.
    /// </summary>
    public class RecoveryEvent(
        Guid id,
        Guid deviceId,
        string errorCode,
        DateTimeOffset timestamp,
        bool isSuccessful,
        string message,
        Guid correlationId)
        : IRecoveryEvent
    {
        /// <inheritdoc />
        public Guid Id { get; set; } = id;

        /// <inheritdoc />
        public Guid DeviceId { get; set; } = deviceId;

        /// <inheritdoc />
        public string ErrorCode { get; set; } = errorCode;

        /// <inheritdoc />
        public DateTimeOffset Timestamp { get; set; } = timestamp;

        /// <inheritdoc />
        public bool IsSuccessful { get; set; } = isSuccessful;

        /// <inheritdoc />
        public string Message { get; set; } = message;

        /// <inheritdoc />
        public Guid CorrelationId { get; set; } = correlationId;

        /// <inheritdoc />
        public Dictionary<string, object> Context { get; set; } = new();
    }
}