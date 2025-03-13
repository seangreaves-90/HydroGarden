using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling.Taxonomy;

namespace HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling.RecoveryStrategy
{
    public interface IRecoveryStrategy
    {
        /// <summary>
        /// Gets the name of this recovery strategy.
        /// </summary>
        string Name { get; }
        
        /// <summary>
        /// Gets the execution priority of this strategy (lower values run first).
        /// </summary>
        int Priority { get; }
        
        /// <summary>
        /// Gets the complexity level this strategy can handle.
        /// </summary>
        ErrorTaxonomy.RecoveryComplexity ComplexityLevel { get; }
        
        /// <summary>
        /// Gets the root causes this strategy can address.
        /// </summary>
        ErrorTaxonomy.RootCause[] SupportedRootCauses { get; }
        
        /// <summary>
        /// Determines if this strategy supports the given error type.
        /// This only evaluates capability, not current state.
        /// </summary>
        /// <param name="error">The error to evaluate</param>
        /// <returns>True if this strategy supports this error type</returns>
        bool SupportsErrorType(IApplicationError? error);
        
        /// <summary>
        /// Legacy method for backward compatibility.
        /// </summary>
        bool CanRecover(IApplicationError? error);
        
        /// <summary>
        /// Attempts to recover from the specified error.
        /// </summary>
        /// <param name="error">The error to recover from</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>True if recovery was successful</returns>
        Task<bool> AttemptRecoveryAsync(IApplicationError? error, CancellationToken ct = default);
    }
}
