namespace HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling.RecoveryStrategy
{
    /// <summary>
    /// Represents a plan for recovering from an error.
    /// </summary>
    public interface IRecoveryPlan
    {
        /// <summary>
        /// Gets or sets the unique identifier for this recovery plan.
        /// </summary>
        public Guid PlanId { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when this plan was created.
        /// </summary>
        public DateTimeOffset CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets the error this plan is designed to recover from.
        /// </summary>
        public IApplicationError? Error { get; set; }
        /// <summary>
        /// Gets or sets the list of strategies to try, in order of priority.
        /// </summary>
        public List<IRecoveryStrategy> Strategies { get; set; }

        /// <summary>
        /// Gets or sets additional context for recovery.
        /// </summary>
        public IDictionary<string, object> Context { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of attempts for each strategy.
        /// </summary>
        public int MaxAttemptsPerStrategy { get; set; }

        /// <summary>
        /// Gets or sets the timeout for the entire recovery operation.
        /// </summary>
        public TimeSpan Timeout { get; set; }

        /// <summary>
        /// Gets or sets whether to continue trying strategies after one succeeds.
        /// </summary>
        public bool ContinueAfterSuccess { get; set; }
    }
}
