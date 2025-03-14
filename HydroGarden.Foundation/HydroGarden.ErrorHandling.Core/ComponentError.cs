using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;

namespace HydroGarden.Foundation.ErrorHandling
{
    /// <summary>
    /// Error representation for IoT components with improved classification and context capture.
    /// </summary>
    public class ComponentError : IApplicationError
    {
        /// <inheritdoc />
        public Guid DeviceId { get; }
        
        /// <inheritdoc />
        public string? ErrorCode { get; }
        
        /// <inheritdoc />
        public string Message { get; }
        
        /// <inheritdoc />
        public ErrorSeverity Severity { get; }
        
        /// <inheritdoc />
        public Guid CorrelationId { get; }
        
        /// <inheritdoc />
        public ErrorSource Source { get; }
        
        /// <inheritdoc />
        public IDictionary<string, object> Context { get; }
        
        /// <inheritdoc />
        public DateTimeOffset Timestamp { get; }
        
        /// <inheritdoc />
        public Exception? Exception { get; }
        
        /// <inheritdoc />
        public ErrorCategory Category { get; }

        /// <summary>
        /// Creates a new ComponentError with detailed classification.
        /// </summary>
        public ComponentError(
            Guid deviceId,
            string? errorCode,
            string message,
            ErrorSeverity severity,
            ErrorSource source,
            IDictionary<string, object>? context = null,
            Exception? exception = null,
            ErrorCategory? category = null)
        {
            DeviceId = deviceId;
            ErrorCode = errorCode;
            Message = message;
            Severity = severity;
            Source = source;
            Exception = exception;
            CorrelationId = Guid.NewGuid();
            Timestamp = DateTimeOffset.UtcNow;

            // Derive category from error code if not provided
            Category = category ?? DeriveCategory(errorCode);

            // Initialize and enrich context
            Context = new Dictionary<string, object>(context ?? new Dictionary<string, object>());
            EnrichContext(exception);
        }

        /// <summary>
        /// Creates an error for device failures.
        /// </summary>
        public static ComponentError CreateDeviceError(
            Guid deviceId,
            string errorCode,
            string message,
            ErrorSeverity severity = ErrorSeverity.Error,
            IDictionary<string, object>? context = null,
            Exception? exception = null)
        {
            return new ComponentError(
                deviceId,
                errorCode,
                message,
                severity,
                ErrorSource.Device,
                context,
                exception,
                ErrorCategory.Device);
        }

        /// <summary>
        /// Creates an error for service failures.
        /// </summary>
        public static ComponentError CreateServiceError(
            Guid deviceId,
            string errorCode,
            string message,
            ErrorSeverity severity = ErrorSeverity.Error,
            IDictionary<string, object>? context = null,
            Exception? exception = null)
        {
            return new ComponentError(
                deviceId,
                errorCode,
                message,
                severity,
                ErrorSource.Service,
                context,
                exception,
                ErrorCategory.Service);
        }

        /// <summary>
        /// Creates an error for communication failures.
        /// </summary>
        public static ComponentError CreateCommunicationError(
            Guid deviceId,
            string errorCode,
            string message,
            ErrorSeverity severity = ErrorSeverity.Error,
            IDictionary<string, object>? context = null,
            Exception? exception = null)
        {
            return new ComponentError(
                deviceId,
                errorCode,
                message,
                severity,
                ErrorSource.Communication,
                context,
                exception,
                ErrorCategory.Communication);
        }

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
            if (!string.IsNullOrEmpty(ErrorCode) && !Context.ContainsKey("ErrorCode"))
                Context["ErrorCode"] = ErrorCode;

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

            return ErrorCategory.Unknown;
        }

        /// <summary>
        /// Gets a diagnostics-friendly string representation of this error.
        /// </summary>
        public override string ToString()
        {
            return $"[{Severity}] [{ErrorCode}] {Message} - DeviceId: {DeviceId}, Timestamp: {Timestamp}";
        }
    }
}