using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;

namespace HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling.RecoveryStrategy
{
    /// <summary>
    /// Extension interface for recovery strategies that need to expose testing functionality.
    /// This interface should only be implemented by test mocks, not production code.
    /// </summary>
    public interface ITestableRecoveryStrategy : IRecoveryStrategy
    {
        /// <summary>
        /// Records that a recovery attempt was made for this error.
        /// Used by test code to verify that the orchestrator called the strategy.
        /// </summary>
        void RecordAttemptedRecovery(IApplicationError? error);
    }
}