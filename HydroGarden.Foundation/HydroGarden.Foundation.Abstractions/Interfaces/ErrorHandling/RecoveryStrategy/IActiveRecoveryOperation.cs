namespace HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling.RecoveryStrategy
{
    /// <summary>
    /// Represents an active recovery operation.
    /// </summary>
    public interface IActiveRecoveryOperation
    {
        /// <summary>
        /// Gets or sets the unique identifier for this operation.
        /// </summary>
        public Guid OperationId { get; set; }

        /// <summary>
        /// Gets or sets the device ID being recovered.
        /// </summary>
        public Guid DeviceId { get; set; }

        /// <summary>
        /// Gets or sets the error being recovered.
        /// </summary>
        public IApplicationError? Error { get; set; }

        /// <summary>
        /// Gets or sets the start time of the operation.
        /// </summary>
        public DateTimeOffset StartTime { get; set; }

        /// <summary>
        /// Gets or sets the current strategy being executed.
        /// </summary>
        public string? CurrentStrategy { get; set; }

        /// <summary>
        /// Gets or sets the current attempt number.
        /// </summary>
        public int CurrentAttempt { get; set; }

        /// <summary>
        /// Gets or sets the recovery plan being executed.
        /// </summary>
        public IRecoveryPlan? Plan { get; set; }
    }
}
