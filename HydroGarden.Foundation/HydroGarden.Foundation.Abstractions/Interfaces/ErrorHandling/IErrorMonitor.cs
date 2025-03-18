namespace HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling
{
    /// <summary>
    /// Defines the contract for error reporting and monitoring.
    /// </summary>
    public interface IErrorMonitor
    {
        /// <summary>
        /// Reports an error to the monitoring system.
        /// </summary>
        /// <param name="error">The error to report.</param>
        /// <param name="ct">A cancellation token.</param>
        Task ReportErrorAsync(IApplicationError error, CancellationToken ct = default);

        /// <summary>
        /// Reports an exception as an error.
        /// </summary>
        /// <param name="source">The source of the error.</param>
        /// <param name="exception">The exception that occurred.</param>
        /// <param name="errorCode">A code that identifies the error type.</param>
        /// <param name="message">A human-readable error message.</param>
        /// <param name="severity">The severity of the error.</param>
        /// <param name="errorSource">The source of the error.</param>
        /// <param name="context">Additional context for the error.</param>
        /// <param name="ct">A cancellation token.</param>
        Task ReportExceptionAsync(object source,
            Exception exception,
            string errorCode,
            string message,
            ErrorSeverity severity = ErrorSeverity.Error,
            ErrorSource errorSource = ErrorSource.Unknown,
            IDictionary<string, object> context = null,
            CancellationToken ct = default);
        
        /// <summary>
        /// Gets the most recent errors, up to the specified limit.
        /// </summary>
        /// <param name="limit">The maximum number of errors to return.</param>
        /// <param name="ct">A cancellation token.</param>
        /// <returns>A collection of the most recent errors.</returns>
        Task<IReadOnlyCollection<IApplicationError>> GetRecentErrorsAsync(int limit = 10, CancellationToken ct = default);
        
        /// <summary>
        /// Checks if there are any active errors with the specified minimum severity.
        /// </summary>
        /// <param name="minSeverity">The minimum severity to check for.</param>
        /// <param name="ct">A cancellation token.</param>
        /// <returns>True if there are active errors of the specified severity, false otherwise.</returns>
        Task<bool> HasActiveErrorsAsync(ErrorSeverity minSeverity = ErrorSeverity.Warning, CancellationToken ct = default);

        /// <summary>
        /// Gets all active errors for a specific device.
        /// </summary>
        /// <param name="deviceId">The ID of the device.</param>
        /// <param name="ct">A cancellation token.</param>
        /// <returns>A collection of active errors for the device.</returns>
        Task<IReadOnlyCollection<IApplicationError>> GetActiveErrorsForDeviceAsync(
            Guid deviceId,
            CancellationToken ct = default);

        /// <summary>
        /// Clears an active error for a device.
        /// </summary>
        /// <param name="deviceId">The ID of the device.</param>
        /// <param name="errorCode">The error code to clear.</param>
        /// <param name="ct">A cancellation token.</param>
        Task ClearErrorAsync(
            Guid deviceId,
            string errorCode,
            CancellationToken ct = default);
    }
}
