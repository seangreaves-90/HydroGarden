using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling.RecoveryStrategy;

namespace HydroGarden.Foundation.ErrorHandling.Models
{
    /// <summary>
    /// Represents the result of a recovery operation.
    /// </summary>
    public class RecoveryStatus : IRecoveryStatus
    {
        /// <summary>
        /// Gets or sets a value indicating whether the recovery was successful.
        /// </summary>
        public bool IsSuccessful { get; set; }

        /// <summary>
        /// Gets or sets the successful strategy name, if any.
        /// </summary>
        public string? SuccessfulStrategy { get; set; }

        /// <summary>
        /// Gets or sets the error codes that were recovered or attempted to be recovered.
        /// </summary>
        public string[] ErrorCodes { get; set; } = [];

        /// <summary>
        /// Gets or sets the number of attempts made during the recovery operation.
        /// </summary>
        public int AttemptCount { get; set; }

        /// <summary>
        /// Gets or sets the number of successful recoveries.
        /// </summary>
        public int SuccessCount { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the recovery operation was initiated.
        /// </summary>
        public DateTimeOffset Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of the last recovery attempt.
        /// </summary>
        public DateTimeOffset LastAttempt { get; set; }

        /// <summary>
        /// Gets or sets additional details about the recovery operation.
        /// </summary>
        public string? Details { get; set; }
    }
}
