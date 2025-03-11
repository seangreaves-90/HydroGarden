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
        Task<IRecoveryStatus> AttemptRecoveryAsync(IApplicationError? error, CancellationToken ct = default);

        /// <summary>
        /// Attempts to recover a device that may have multiple errors.
        /// </summary>
        /// <param name="deviceId">The ID of the device to recover.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Information about the recovery attempt.</returns>
        Task<IRecoveryStatus> RecoverDeviceAsync(Guid deviceId, CancellationToken ct = default);

        /// <summary>
        /// Creates a recovery plan for an error without executing it.
        /// </summary>
        /// <param name="error">The error to create a recovery plan for.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The recovery plan.</returns>
        Task<IRecoveryPlan> CreateRecoveryPlanAsync(IApplicationError? error, CancellationToken ct = default);

        /// <summary>
        /// Executes a previously created recovery plan.
        /// </summary>
        /// <param name="plan">The recovery plan to execute.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Information about the recovery attempt.</returns>
        Task<IRecoveryStatus> ExecuteRecoveryPlanAsync(IRecoveryPlan plan, CancellationToken ct = default);

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
        Task<IDictionary<string, IRecoveryMetrics>> GetRecoveryStatisticsAsync(DateTimeOffset since, CancellationToken ct = default);

        /// <summary>
        /// Gets the recovery history for a specific device.
        /// </summary>
        /// <param name="deviceId">The ID of the device.</param>
        /// <param name="maxEntries">Maximum number of entries to return.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A list of recovery records.</returns>
        Task<IReadOnlyList<IRecoveryRecord>> GetRecoveryHistoryAsync(Guid deviceId, int maxEntries = 50, CancellationToken ct = default);

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
        Task<IReadOnlyList<IActiveRecoveryOperation>> GetActiveRecoveriesAsync(CancellationToken ct = default);
    }

}
