using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.Common.ErrorHandling.Constants;

namespace HydroGarden.Foundation.Common.ErrorHandling
{
    /// <summary>
    /// Enhanced error representation for IoT components with improved classification,
    /// context capture, and recovery tracking capabilities.
    /// </summary>
    public class ComponentError : IApplicationError
    {
        // Core properties from IApplicationError
        public Guid DeviceId { get; }
        public string? ErrorCode { get; }
        public string Message { get; }
        public ErrorSeverity Severity { get; }
        public Guid CorrelationId { get; } = Guid.NewGuid();
        public ErrorSource Source { get; }
        public bool IsTransient { get; }
        public IDictionary<string, object> Context { get; }
        public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
        public Exception? Exception { get; }

        // Enhanced recovery properties
        public bool IsRecoverable { get; }
        public int RecoveryAttemptCount { get; private set; }
        public DateTimeOffset? LastRecoveryAttempt { get; private set; }
        public int MaxRecoveryAttempts { get; } = 5;
        public ErrorCategory Category { get; }

        // Recovery backoff with exponential delay capped at 10 minutes
        public TimeSpan RecoveryBackoffInterval => TimeSpan.FromSeconds(
            Math.Min(600, Math.Pow(2, Math.Min(RecoveryAttemptCount, 9))));

        /// <summary>
        /// Creates a new ComponentError with detailed classification.
        /// </summary>
        public ComponentError(
            Guid deviceId,
            string? errorCode,
            string message,
            ErrorSeverity severity,
            bool isRecoverable,
            ErrorSource source,
            bool isTransient,
            IDictionary<string, object>? context = null,
            Exception? exception = null,
            ErrorCategory? category = null)
        {
            DeviceId = deviceId;
            ErrorCode = errorCode;
            Message = message;
            Severity = severity;
            IsRecoverable = isRecoverable && !ErrorCodes.IsUnrecoverable(errorCode);
            Source = source;
            IsTransient = isTransient;
            Exception = exception;

            // Derive category from error code if not provided
            Category = category ?? DeriveCategory(errorCode);

            // Enhanced context capture
            Context = new Dictionary<string, object>(context ?? new Dictionary<string, object>());
            EnrichContext(exception);
        }

        /// <summary>
        /// Creates a non-recoverable error with appropriate classification.
        /// </summary>
        public static ComponentError CreateNonRecoverable(
            Guid deviceId,
            string errorCode,
            string message,
            ErrorSeverity severity = ErrorSeverity.Critical,
            ErrorSource source = ErrorSource.Unknown,
            IDictionary<string, object>? context = null,
            Exception? exception = null)
        {
            return new ComponentError(
                deviceId,
                errorCode,
                message,
                severity,
                false,
                source,
                false,
                context,
                exception);
        }


        /// <summary>
        /// Creates a transient error that can be retried.
        /// </summary>
        public static ComponentError CreateTransient(
            Guid deviceId,
            string errorCode,
            string message,
            ErrorSeverity severity = ErrorSeverity.Error,
            ErrorSource source = ErrorSource.Unknown,
            IDictionary<string, object>? context = null,
            Exception? exception = null)
        {
            return new ComponentError(
                deviceId,
                errorCode,
                message,
                severity,
                true,
                source,
                true,
                context,
                exception);
        }

        /// <summary>
        /// Records an attempt to recover from this error.
        /// </summary>
        public void RecordRecoveryAttempt()
        {
            RecoveryAttemptCount++;
            LastRecoveryAttempt = DateTimeOffset.UtcNow;
        }

        /// <summary>
        /// Determines if recovery can be attempted based on attempt count and backoff.
        /// </summary>
        public bool CanAttemptRecovery()
        {
            if (!IsRecoverable) return false;
            if (RecoveryAttemptCount >= MaxRecoveryAttempts) return false;
            if (LastRecoveryAttempt == null) return true;
            return (DateTimeOffset.UtcNow - LastRecoveryAttempt.Value) > RecoveryBackoffInterval;
        }

        /// <summary>
        /// Reports if the error is unrecoverable.
        /// </summary>
        public bool IsUnrecoverable => !IsRecoverable ||
                                       RecoveryAttemptCount >= MaxRecoveryAttempts ||
                                       ErrorCodes.IsUnrecoverable(ErrorCode);

        /// <summary>
        /// Enriches the context with additional diagnostic information.
        /// </summary>
        private void EnrichContext(Exception? exception)
        {
            // Add timestamp to context for consistent access
            Context["Timestamp"] = Timestamp.ToString("o");
            Context["ErrorId"] = CorrelationId.ToString();

            if (!Context.ContainsKey("DeviceId"))
                Context["DeviceId"] = DeviceId.ToString();

            // Add source component info
            if (!string.IsNullOrEmpty(ErrorCode))
                Context.TryAdd("ErrorCode", ErrorCode);

            // Add error category for classification
            Context["ErrorCategory"] = Category.ToString();

            // Add basic exception details if available
            if (exception != null)
            {
                if (!Context.ContainsKey("ExceptionType"))
                    Context["ExceptionType"] = exception.GetType().Name;

                // Add inner exception for better diagnostics
                if (exception.InnerException != null && !Context.ContainsKey("InnerExceptionType"))
                    Context["InnerExceptionType"] = exception.InnerException.GetType().Name;

                // Add stack trace hash for pattern recognition without storing full traces
                if (!Context.ContainsKey("StackTraceHash") && !string.IsNullOrEmpty(exception.StackTrace))
                    Context["StackTraceHash"] = exception.StackTrace.GetHashCode().ToString();
            }
        }

        /// <summary>
        /// Derives error category from the error code pattern.
        /// </summary>
        private static ErrorCategory DeriveCategory(string? errorCode)
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
            if (errorCode.StartsWith("RECOVERY_"))
                return ErrorCategory.Recovery;

            return ErrorCategory.Unknown;
        }

        /// <summary>
        /// Gets a diagnostics-friendly string representation of this error.
        /// </summary>
        public override string ToString()
        {
            return $"[{Severity}] [{ErrorCode}] {Message} - DeviceId: {DeviceId}, RecoveryAttempts: {RecoveryAttemptCount}, Timestamp: {Timestamp}";
        }
    }

    /// <summary>
    /// Categorizes errors for better grouping and analysis.
    /// </summary>
    public enum ErrorCategory
    {
        Unknown = 0,
        Device = 10,
        Service = 20,
        Communication = 30,
        EventSystem = 40,
        Storage = 50,
        Recovery = 60,
        Security = 70
    }
}