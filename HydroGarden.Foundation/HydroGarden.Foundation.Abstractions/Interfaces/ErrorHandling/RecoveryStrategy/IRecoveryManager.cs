
using System;
using System.Threading;
using System.Threading.Tasks;

namespace HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling.RecoveryStrategy
{
    /// <summary>
    /// Status of a recovery operation.
    /// </summary>
    public class RecoveryStatus
    {
        /// <summary>
        /// Whether the recovery operation was successful.
        /// </summary>
        public bool IsSuccessful { get; set; }

        /// <summary>
        /// Number of attempts made.
        /// </summary>
        public int AttemptCount { get; set; }


        /// <summary>
        /// Name of the strategy that succeeded, if any.
        /// </summary>
        public string? SuccessfulStrategy { get; set; }

        /// <summary>
        /// List of error codes that were addressed.
        /// </summary>
        public string[] ErrorCodes { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Timestamp of the recovery operation.
        /// </summary>
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

        public int SuccessCount { get; set; }

        public DateTimeOffset LastAttempt { get; set; }
    }

    /// <summary>
    /// Interface for recovery management.
    /// </summary>
    public interface IRecoveryManager
    {
        /// <summary>
        /// Attempts to recover from an error.
        /// </summary>
        Task<RecoveryStatus> AttemptRecoveryAsync(IApplicationError error, CancellationToken ct = default);

        /// <summary>
        /// Attempts to recover a device.
        /// </summary>
        Task<RecoveryStatus> RecoverDeviceAsync(Guid deviceId, CancellationToken ct = default);

        /// <summary>
        /// Gets recovery statistics.
        /// </summary>
        Task<IDictionary<string, int>> GetRecoveryStatisticsAsync(DateTimeOffset since, CancellationToken ct = default);
    }
}

