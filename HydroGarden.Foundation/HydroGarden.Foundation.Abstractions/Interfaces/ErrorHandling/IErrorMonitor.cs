namespace HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling
{
    public interface IErrorMonitor
    {
        Task ReportErrorAsync(IApplicationError error, CancellationToken ct = default);
        Task<IReadOnlyList<IApplicationError>> GetRecentErrorsAsync(int count = 10, CancellationToken ct = default);
        Task<bool> HasActiveErrorsAsync(ErrorSeverity minimumSeverity = ErrorSeverity.Error, CancellationToken ct = default);

        /// <summary>
        /// Gets all active errors for a specific device
        /// </summary>
        Task<IReadOnlyList<IApplicationError>> GetActiveErrorsForDeviceAsync(
            Guid deviceId,
            CancellationToken ct = default);

        /// <summary>
        /// Marks an error as handled
        /// </summary>
        Task MarkErrorHandledAsync(
            Guid deviceId,
            string errorCode,
            CancellationToken ct = default);

        /// <summary>
        /// Gets statistics about error occurrences
        /// </summary>
        Task<IDictionary<string, int>> GetErrorStatisticsAsync(
            DateTimeOffset since,
            CancellationToken ct = default);

        /// <summary>
        /// Registers a recovery attempt for an error
        /// </summary>
        Task RegisterRecoveryAttemptAsync(
            Guid deviceId,
            string errorCode,
            bool successful,
            CancellationToken ct = default);

    }
}
