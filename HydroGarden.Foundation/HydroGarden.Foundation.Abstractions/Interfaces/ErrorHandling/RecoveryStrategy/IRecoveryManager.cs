namespace HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling.RecoveryStrategy
{
    /// <summary>
    /// Interface for recovery management.
    /// </summary>
    public interface IRecoveryManager
    {
        /// <summary>
        /// Attempts to recover from an error.
        /// </summary>
        Task<IRecoveryStatus> AttemptRecoveryAsync(IApplicationError error, CancellationToken ct = default);

        /// <summary>
        /// Attempts to recover a device.
        /// </summary>
        Task<IRecoveryStatus> RecoverDeviceAsync(Guid deviceId, CancellationToken ct = default);

        /// <summary>
        /// Gets recovery statistics.
        /// </summary>
        Task<IDictionary<string, int>> GetRecoveryStatisticsAsync(DateTimeOffset since, CancellationToken ct = default);
    }
}

