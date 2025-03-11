namespace HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling.RecoveryStrategy
{
    /// <summary>
    /// Status of a recovery operation.
    /// </summary>
    public interface IRecoveryStatus
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
        public string[] ErrorCodes { get; set; }

        /// <summary>
        /// Timestamp of the recovery operation.
        /// </summary>
        public DateTimeOffset Timestamp { get; set; }

        public int SuccessCount { get; set; }

        public DateTimeOffset LastAttempt { get; set; }
    }
}
