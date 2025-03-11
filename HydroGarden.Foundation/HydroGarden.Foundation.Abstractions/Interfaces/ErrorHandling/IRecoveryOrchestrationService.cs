using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling.RecoveryStrategy;

namespace HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling
{
    /// <summary>
    /// Defines a service that orchestrates recovery operations across components.
    /// This service coordinates recovery activities, tracks recovery state, and manages recovery strategies.
    /// </summary>
    public interface IRecoveryOrchestrationService
    {
        /// <summary>
        /// Attempts to recover from an error using appropriate recovery strategies.
        /// </summary>
        /// <param name="error">The error to recover from.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Information about the recovery attempt.</returns>
        Task<RecoveryStatus> AttemptRecoveryAsync(IApplicationError? error, CancellationToken ct = default);

        /// <summary>
        /// Attempts to recover a device that may have multiple errors.
        /// </summary>
        /// <param name="deviceId">The ID of the device to recover.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Information about the recovery attempt.</returns>
        Task<RecoveryStatus> RecoverDeviceAsync(Guid deviceId, CancellationToken ct = default);

        /// <summary>
        /// Creates a recovery plan for an error without executing it.
        /// </summary>
        /// <param name="error">The error to create a recovery plan for.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The recovery plan.</returns>
        Task<RecoveryPlan> CreateRecoveryPlanAsync(IApplicationError? error, CancellationToken ct = default);

        /// <summary>
        /// Executes a previously created recovery plan.
        /// </summary>
        /// <param name="plan">The recovery plan to execute.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Information about the recovery attempt.</returns>
        Task<RecoveryStatus> ExecuteRecoveryPlanAsync(RecoveryPlan plan, CancellationToken ct = default);

        /// <summary>
        /// Registers a recovery strategy with the orchestration service.
        /// </summary>
        /// <param name="strategy">The strategy to register.</param>
        void RegisterStrategy(IRecoveryStrategy strategy);

        /// <summary>
        /// Gets recovery statistics for a specific period.
        /// </summary>
        /// <param name="since">The start time for statistics.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A dictionary mapping error codes to recovery count.</returns>
        Task<IDictionary<string, RecoveryMetrics>> GetRecoveryStatisticsAsync(DateTimeOffset since, CancellationToken ct = default);

        /// <summary>
        /// Gets the recovery history for a specific device.
        /// </summary>
        /// <param name="deviceId">The ID of the device.</param>
        /// <param name="maxEntries">Maximum number of entries to return.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A list of recovery records.</returns>
        Task<IReadOnlyList<RecoveryRecord>> GetRecoveryHistoryAsync(Guid deviceId, int maxEntries = 50, CancellationToken ct = default);

        /// <summary>
        /// Checks if a device is currently undergoing recovery.
        /// </summary>
        /// <param name="deviceId">The ID of the device to check.</param>
        /// <returns>True if the device is being recovered, false otherwise.</returns>
        bool IsDeviceRecovering(Guid deviceId);

        /// <summary>
        /// Gets the active recovery operations currently in progress.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A list of active recovery operations.</returns>
        Task<IReadOnlyList<ActiveRecoveryOperation>> GetActiveRecoveriesAsync(CancellationToken ct = default);
    }

    /// <summary>
    /// Represents a plan for recovering from an error.
    /// </summary>
    public class RecoveryPlan
    {
        /// <summary>
        /// Gets or sets the unique identifier for this recovery plan.
        /// </summary>
        public Guid PlanId { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Gets or sets the timestamp when this plan was created.
        /// </summary>
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// Gets or sets the error this plan is designed to recover from.
        /// </summary>
        public IApplicationError? Error { get; set; } = null!;

        /// <summary>
        /// Gets or sets the list of strategies to try, in order of priority.
        /// </summary>
        public List<IRecoveryStrategy> Strategies { get; set; } = new();

        /// <summary>
        /// Gets or sets additional context for recovery.
        /// </summary>
        public IDictionary<string, object> Context { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Gets or sets the maximum number of attempts for each strategy.
        /// </summary>
        public int MaxAttemptsPerStrategy { get; set; } = 3;

        /// <summary>
        /// Gets or sets the timeout for the entire recovery operation.
        /// </summary>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Gets or sets whether to continue trying strategies after one succeeds.
        /// </summary>
        public bool ContinueAfterSuccess { get; set; } = false;
    }

    /// <summary>
    /// Contains metrics about recovery operations.
    /// </summary>
    public class RecoveryMetrics
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

    /// <summary>
    /// Represents a record of a recovery operation.
    /// </summary>
    public class RecoveryRecord
    {
        /// <summary>
        /// Gets or sets the unique identifier for this recovery record.
        /// </summary>
        public Guid RecordId { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Gets or sets the device ID that was recovered.
        /// </summary>
        public Guid DeviceId { get; set; }

        /// <summary>
        /// Gets or sets the error code that was recovered.
        /// </summary>
        public string? ErrorCode { get; set; }

        /// <summary>
        /// Gets or sets whether the recovery was successful.
        /// </summary>
        public bool IsSuccessful { get; set; }

        /// <summary>
        /// Gets or sets the strategy that was used for recovery.
        /// </summary>
        public string? StrategyUsed { get; set; }

        /// <summary>
        /// Gets or sets the number of attempts made.
        /// </summary>
        public int AttemptCount { get; set; }

        /// <summary>
        /// Gets or sets the correlation ID for tracking related operations.
        /// </summary>
        public Guid CorrelationId { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of the recovery operation.
        /// </summary>
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// Gets or sets the duration of the recovery operation.
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Gets or sets additional details about the recovery.
        /// </summary>
        public string? Details { get; set; }
    }

    /// <summary>
    /// Represents an active recovery operation.
    /// </summary>
    public class ActiveRecoveryOperation
    {
        /// <summary>
        /// Gets or sets the unique identifier for this operation.
        /// </summary>
        public Guid OperationId { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Gets or sets the device ID being recovered.
        /// </summary>
        public Guid DeviceId { get; set; }

        /// <summary>
        /// Gets or sets the error being recovered.
        /// </summary>
        public IApplicationError? Error { get; set; } = null!;

        /// <summary>
        /// Gets or sets the start time of the operation.
        /// </summary>
        public DateTimeOffset StartTime { get; set; } = DateTimeOffset.UtcNow;

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
        public RecoveryPlan? Plan { get; set; }
    }
}
