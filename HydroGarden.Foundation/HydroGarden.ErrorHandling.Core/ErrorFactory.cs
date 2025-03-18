using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;

namespace HydroGarden.Foundation.ErrorHandling
{
    /// <summary>
    /// Factory class for creating application errors.
    /// </summary>
    public class ErrorFactory
    {
        /// <summary>
        /// Creates a device error.
        /// </summary>
        /// <param name="deviceId">The device ID.</param>
        /// <param name="errorCode">The error code.</param>
        /// <param name="message">The error message.</param>
        /// <param name="severity">The error severity.</param>
        /// <param name="exception">The exception that caused the error, if any.</param>
        /// <param name="context">Additional context information.</param>
        /// <returns>An application error.</returns>
        public static IApplicationError CreateDeviceError(
            Guid deviceId,
            string errorCode,
            string message,
            ErrorSeverity severity = ErrorSeverity.Error,
            Exception? exception = null,
            Dictionary<string, object?>? context = null)
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
        /// Creates a service error.
        /// </summary>
        /// <param name="serviceName">The service name.</param>
        /// <param name="errorCode">The error code.</param>
        /// <param name="message">The error message.</param>
        /// <param name="severity">The error severity.</param>
        /// <param name="exception">The exception that caused the error, if any.</param>
        /// <param name="context">Additional context information.</param>
        /// <returns>An application error.</returns>
        public static IApplicationError CreateServiceError(
            string serviceName,
            string errorCode,
            string message,
            ErrorSeverity severity = ErrorSeverity.Error,
            Exception? exception = null,
            IDictionary<string, object>? context = null)
        {
            var contextDict = new Dictionary<string, object>();

            if (context != null)
            {
                foreach (var kvp in context)
                {
                    contextDict[kvp.Key] = kvp.Value;
                }
            }

            contextDict["ServiceName"] = serviceName;

            return new ComponentError(
                Guid.Empty, // No device ID for service errors
                errorCode,
                message,
                severity,
                ErrorSource.Service,
                contextDict,
                exception,
                ErrorCategory.Service);
        }

        /// <summary>
        /// Creates a communication error.
        /// </summary>
        /// <param name="deviceId">The device ID, if applicable.</param>
        /// <param name="errorCode">The error code.</param>
        /// <param name="message">The error message.</param>
        /// <param name="severity">The error severity.</param>
        /// <param name="exception">The exception that caused the error, if any.</param>
        /// <param name="context">Additional context information.</param>
        /// <returns>An application error.</returns>
        public static IApplicationError CreateCommunicationError(
            Guid deviceId,
            string errorCode,
            string message,
            ErrorSeverity severity = ErrorSeverity.Error,
            Exception? exception = null,
            IDictionary<string, object>? context = null)
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
        /// Creates a storage error.
        /// </summary>
        /// <param name="errorCode">The error code.</param>
        /// <param name="message">The error message.</param>
        /// <param name="severity">The error severity.</param>
        /// <param name="exception">The exception that caused the error, if any.</param>
        /// <param name="context">Additional context information.</param>
        /// <returns>An application error.</returns>
        public static IApplicationError CreateStorageError(
            string errorCode,
            string message,
            ErrorSeverity severity = ErrorSeverity.Error,
            Exception? exception = null,
            IDictionary<string, object>? context = null)
        {
            return new ComponentError(
                Guid.Empty, // No device ID for storage errors
                errorCode,
                message,
                severity,
                ErrorSource.Database,
                context,
                exception,
                ErrorCategory.Storage);
        }

        /// <summary>
        /// Creates an event system error.
        /// </summary>
        /// <param name="errorCode">The error code.</param>
        /// <param name="message">The error message.</param>
        /// <param name="severity">The error severity.</param>
        /// <param name="exception">The exception that caused the error, if any.</param>
        /// <param name="context">Additional context information.</param>
        /// <returns>An application error.</returns>
        public static IApplicationError CreateEventSystemError(
            string errorCode,
            string message,
            ErrorSeverity severity = ErrorSeverity.Error,
            Exception? exception = null,
            IDictionary<string, object>? context = null)
        {
            return new ComponentError(
                Guid.Empty, // No device ID for event system errors
                errorCode,
                message,
                severity,
                ErrorSource.Service,
                context,
                exception,
                ErrorCategory.EventSystem);
        }

        /// <summary>
        /// Creates an error from an exception, attempting to extract appropriate context.
        /// </summary>
        /// <param name="exception">The exception to convert.</param>
        /// <param name="deviceId">The device ID, if applicable.</param>
        /// <param name="errorCode">The error code to assign. If null, will try to infer from exception.</param>
        /// <param name="context">Additional context information.</param>
        /// <returns>An application error.</returns>
        public static IApplicationError FromException(
            Exception exception,
            Guid? deviceId = null,
            string? errorCode = null,
            IDictionary<string, object?>? context = null)
        {

            ArgumentNullException.ThrowIfNull(exception);

            // Build context with exception details
            var contextBuilder = ErrorContextBuilder.Create()
                .WithException(exception)
                .WithLocation();

            if (context != null)
            {
                contextBuilder.WithProperties(context);
            }

            // If this is one of our application exceptions, extract its information
            if (exception is ErrorHandling.Exceptions.ApplicationException appException)
            {
                return new ComponentError(
                    appException.DeviceId ?? deviceId ?? Guid.Empty,
                    appException.ErrorCode,
                    appException.Message,
                    appException.Severity,
                    appException.Source,
                    contextBuilder.Build(),
                    exception,
                    appException.Category);
            }

            // Otherwise, determine best defaults based on the exception type
            var derivedErrorCode = errorCode ?? DeriveErrorCodeFromException(exception);
            var derivedSource = DeriveSourceFromException(exception);
            var derivedCategory = DeriveCategoryFromException(exception);
            var severity = DeriveSeverityFromException(exception);

            return new ComponentError(
                deviceId ?? Guid.Empty,
                derivedErrorCode,
                exception.Message,
                severity,
                derivedSource,
                contextBuilder.Build(),
                exception,
                derivedCategory);
        }

        /// <summary>
        /// Attempts to derive an appropriate error code from an exception type.
        /// </summary>
        public static string DeriveErrorCodeFromException(Exception exception)
        {
            var exceptionType = exception.GetType().Name;

            if (exceptionType.Contains("Timeout"))
                return "SERVICE_OP_TIMEOUT";

            if (exceptionType.Contains("ArgumentNull") || exceptionType.Contains("ArgumentOutOfRange"))
                return "SERVICE_INVALID_ARGUMENT";

            if (exceptionType.Contains("IO") || exceptionType.Contains("File"))
                return "STORAGE_IO_ERROR";

            if (exceptionType.Contains("Format") || exceptionType.Contains("Parse"))
                return "SERVICE_DATA_FORMAT_ERROR";

            if (exceptionType.Contains("NotSupported") || exceptionType.Contains("NotImplemented"))
                return "SERVICE_NOT_SUPPORTED";

            if (exceptionType.Contains("Security") || exceptionType.Contains("Unauthorized"))
                return "SECURITY_ERROR";

            if (exceptionType.Contains("ObjectDisposed"))
                return "SERVICE_DISPOSED_ERROR";

            // Default fallback
            return "SERVICE_UNHANDLED_EXCEPTION";
        }

        /// <summary>
        /// Attempts to derive an appropriate error source from an exception type.
        /// </summary>
        private static ErrorSource DeriveSourceFromException(Exception exception)
        {
            var exceptionType = exception.GetType().Name;

            if (exceptionType.Contains("IO") || exceptionType.Contains("File") ||
                exceptionType.Contains("Sql") || exceptionType.Contains("Entity") ||
                exceptionType.Contains("Data"))
                return ErrorSource.Database;

            if (exceptionType.Contains("Http") || exceptionType.Contains("Socket") ||
                exceptionType.Contains("Tcp") || exceptionType.Contains("Network"))
                return ErrorSource.Communication;

            // Default fallback
            return ErrorSource.Service;
        }

        /// <summary>
        /// Attempts to derive an appropriate error category from an exception type.
        /// </summary>
        private static ErrorCategory DeriveCategoryFromException(Exception exception)
        {
            var exceptionType = exception.GetType().Name;

            if (exceptionType.Contains("IO") || exceptionType.Contains("File") ||
                exceptionType.Contains("Sql") || exceptionType.Contains("Entity") ||
                exceptionType.Contains("Data"))
                return ErrorCategory.Storage;

            if (exceptionType.Contains("Http") || exceptionType.Contains("Socket") ||
                exceptionType.Contains("Tcp") || exceptionType.Contains("Network"))
                return ErrorCategory.Communication;

            if (exceptionType.Contains("Event"))
                return ErrorCategory.EventSystem;

            // Default fallback
            return ErrorCategory.Service;
        }

        /// <summary>
        /// Creates an error with a given context.
        /// </summary>
        /// <param name="deviceId">The device ID.</param>
        /// <param name="errorCode">The error code.</param>
        /// <param name="message">The error message.</param>
        /// <param name="severity">The error severity.</param>
        /// <param name="source">The error source.</param>
        /// <param name="context">The context information.</param>
        /// <param name="exception">The exception that caused the error, if any.</param>
        /// <returns>An application error.</returns>
        public static IApplicationError CreateWithContext(
            Guid deviceId,
            string errorCode,
            string message,
            ErrorSeverity severity,
            ErrorSource source,
            IDictionary<string, object> context,
            Exception? exception = null)
        {
            // Determine appropriate category based on source
            ErrorCategory category = source switch
            {
                ErrorSource.Device => ErrorCategory.Device,
                ErrorSource.Service => ErrorCategory.Service,
                ErrorSource.Communication => ErrorCategory.Communication,
                ErrorSource.Database => ErrorCategory.Storage,
                ErrorSource.System => ErrorCategory.System,
                _ => ErrorCategory.Unknown
            };

            return new ComponentError(
                deviceId,
                errorCode,
                message,
                severity,
                source,
                context,
                exception,
                category);
        }

        /// <summary>
        /// Attempts to derive an appropriate error severity from an exception type.
        /// </summary>
        private static ErrorSeverity DeriveSeverityFromException(Exception exception)
        {
            var exceptionType = exception.GetType().Name;

            // Critical errors that likely affect system stability
            if (exceptionType.Contains("OutOfMemory") ||
                exceptionType.Contains("ThreadAbort") ||
                exceptionType.Contains("StackOverflow") ||
                exceptionType.Contains("ExecutionEngine"))
                return ErrorSeverity.Catastrophic;

            // Serious errors that need attention but might not crash the system
            if (exceptionType.Contains("Security") ||
                exceptionType.Contains("Unauthorized") ||
                exceptionType.Contains("InvalidOperation") ||
                exceptionType.Contains("NotSupported"))
                return ErrorSeverity.Critical;

            // Default for most exceptions
            return ErrorSeverity.Error;
        }
    }
}