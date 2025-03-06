using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.Abstractions.Interfaces.Events;

namespace HydroGarden.Foundation.Common.ErrorHandling
{
    public class ComponentError(Guid deviceId, string? errorCode, string message,
        ErrorSeverity severity, bool isRecoverable, IDictionary<string, object>? context = null,
        Exception? exception = null) : IApplicationError
    {
        public Guid DeviceId { get; } = deviceId;
        public string? ErrorCode { get; } = errorCode;
        public string Message { get; } = message;
        public ErrorSeverity Severity { get; } = severity;
        public IDictionary<string, object> Context { get; } = context ?? new Dictionary<string, object>();
        public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
        public Exception? Exception { get; } = exception;
        public bool IsRecoverable { get; } = isRecoverable;
        public int RecoveryAttemptCount { get; set; }
        public DateTimeOffset? LastRecoveryAttempt { get; set; }
        public TimeSpan RecoveryBackoffInterval => TimeSpan.FromSeconds(Math.Pow(2, Math.Min(RecoveryAttemptCount, 10)));



        /// <summary>
        /// Creates a non-recoverable error
        /// </summary>
        public static ComponentError CreateNonRecoverable(
            Guid deviceId,
            string errorCode,
            string message,
            ErrorSeverity severity = ErrorSeverity.Critical,
            IDictionary<string, object>? context = null,
            Exception? exception = null)
        {
            return new ComponentError(
                deviceId,
                errorCode,
                message,
                severity,
                false,
                context,
                exception);
        }

        /// <summary>
        /// Records a recovery attempt for this error
        /// </summary>
        public void RecordRecoveryAttempt()
        {
            RecoveryAttemptCount++;
            LastRecoveryAttempt = DateTimeOffset.UtcNow;
        }

        /// <summary>
        /// Determines if enough time has passed to attempt recovery again
        /// </summary>
        public bool CanAttemptRecovery()
        {
            if (!IsRecoverable) return false;

            if (LastRecoveryAttempt == null) return true;

            return (DateTimeOffset.UtcNow - LastRecoveryAttempt.Value) > RecoveryBackoffInterval;
        }
    }
}