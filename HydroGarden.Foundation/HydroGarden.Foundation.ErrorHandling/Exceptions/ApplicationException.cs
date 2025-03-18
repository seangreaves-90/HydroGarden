using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;

namespace HydroGarden.Foundation.ErrorHandling.Exceptions
{
    /// <summary>
    /// Base exception class for all application-specific exceptions.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="errorCode">The error code.</param>
    /// <param name="severity">The severity level.</param>
    /// <param name="source">The error source.</param>
    /// <param name="innerException">The inner exception.</param>
    /// <param name="deviceId">The associated device ID, if applicable.</param>
    /// <param name="context">Additional context information.</param>
    /// <param name="category">The error category.</param>
    public abstract class ApplicationException(string message, string errorCode, ErrorSeverity severity, ErrorSource source, 
        Exception? innerException = null, Guid? deviceId = null, IDictionary<string, object>? context = null, ErrorCategory? category = null) 
        : Exception(message, innerException)
    {
        /// <summary>
        /// Gets the error code associated with this exception.
        /// </summary>
        public string ErrorCode { get; } = errorCode ?? throw new ArgumentNullException(nameof(errorCode));

        /// <summary>
        /// Gets the severity level of the exception.
        /// </summary>
        public ErrorSeverity Severity { get; } = severity;

        /// <summary>
        /// Gets the source of the exception.
        /// </summary>
        public new ErrorSource Source { get; } = source;

        /// <summary>
        /// Gets the context data for the exception.
        /// </summary>
        public IDictionary<string, object> Context { get; } = context is null ? [] :  new Dictionary<string, object>(context);

        /// <summary>
        /// Gets the device ID associated with this exception, if applicable.
        /// </summary>
        public Guid? DeviceId { get; } = deviceId;

        /// <summary>
        /// Gets the error category.
        /// </summary>
        public ErrorCategory Category { get; } = category ?? DeriveCategory(errorCode);

        /// <summary>
        /// Converts the exception to an ApplicationError for monitoring and reporting.
        /// </summary>
        /// <returns>An IApplicationError representing this exception.</returns>
        public IApplicationError ToApplicationError()
        {
            return new ComponentError(
                DeviceId ?? Guid.Empty,
                ErrorCode,
                Message,
                Severity,
                Source,
                Context,
                this,
                Category);
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