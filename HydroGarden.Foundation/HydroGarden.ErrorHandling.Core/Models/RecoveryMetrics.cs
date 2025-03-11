using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling.RecoveryStrategy;

namespace HydroGarden.Foundation.ErrorHandling.Models
{
    /// <summary>
    /// Contains metrics about recovery operations.
    /// </summary>
    public class RecoveryMetrics : IRecoveryMetrics
    {
        /// <summary>
        /// Gets or sets the total number of recovery attempts.
        /// </summary>
        public int TotalAttempts { get; set; }

        /// <summary>
        /// Gets or sets the number of successful recovery attempts.
        /// </summary>
        public int SuccessfulAttempts { get; set; }

        /// <summary>
        /// Gets or sets the number of failed recovery attempts.
        /// </summary>
        public int FailedAttempts { get; set; }

        /// <summary>
        /// Gets the success rate as a percentage.
        /// </summary>
        public double SuccessRate => TotalAttempts > 0 ? (double)SuccessfulAttempts / TotalAttempts * 100 : 0;

        /// <summary>
        /// Gets or sets the most successful strategy name.
        /// </summary>
        public string? MostSuccessfulStrategy { get; set; }

        /// <summary>
        /// Gets or sets the average recovery time in milliseconds.
        /// </summary>
        public double AverageRecoveryTimeMs { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of the last recovery attempt.
        /// </summary>
        public DateTimeOffset? LastAttemptTimestamp { get; set; }
    }

}
