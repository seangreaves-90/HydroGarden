using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling.RecoveryStrategy;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling.Taxonomy;

namespace HydroGarden.Foundation.Tests.ErrorHandling.Mocks
{
    /// <summary>
    /// A mock implementation of IRecoveryStrategy for testing purposes.
    /// </summary>
    public class MockRecoveryStrategy : ITestableRecoveryStrategy
    {
        /// <summary>
        /// Gets the name of this recovery strategy.
        /// </summary>
        public virtual string Name { get; } = "MockRecoveryStrategy";

        /// <summary>
        /// Gets the execution priority of this strategy.
        /// </summary>
        public virtual int Priority { get; set; } = 100;

        /// <summary>
        /// Gets the complexity level this strategy can handle.
        /// </summary>
        public virtual ErrorTaxonomy.RecoveryComplexity ComplexityLevel { get; set; } = ErrorTaxonomy.RecoveryComplexity.Simple;

        /// <summary>
        /// Gets the root causes this strategy can address.
        /// </summary>
        public virtual ErrorTaxonomy.RootCause[] SupportedRootCauses { get; set; } = { ErrorTaxonomy.RootCause.Unknown };

        /// <summary>
        /// Gets or sets a value indicating whether the strategy can recover from errors.
        /// </summary>
        public bool CanRecoverValue { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether the recovery attempt will succeed.
        /// </summary>
        public bool RecoverySuccessful { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether the recovery attempt should throw an exception.
        /// </summary>
        public bool ThrowExceptionOnRecovery { get; set; } = false;

        /// <summary>
        /// Gets the list of errors that this strategy has attempted to recover from.
        /// </summary>
        public List<IApplicationError?> AttemptedRecoveries { get; } = new();

        /// <summary>
        /// Records that a recovery attempt was made for this error.
        /// </summary>
        public void RecordAttemptedRecovery(IApplicationError? error)
        {
            AttemptedRecoveries.Add(error);
        }

        /// <summary>
        /// Determines if this strategy supports the given error type.
        /// </summary>
        public virtual bool SupportsErrorType(IApplicationError? error) => CanRecoverValue;

        /// <summary>
        /// Determines if this strategy can recover from the specified error.
        /// </summary>
        public bool CanRecover(IApplicationError? error) => CanRecoverValue;

        /// <summary>
        /// Attempts to recover from the error.
        /// </summary>
        public Task<bool> AttemptRecoveryAsync(IApplicationError? error, CancellationToken ct = default)
        {
            // Note: We don't need to add to AttemptedRecoveries here as it's handled by RecordAttemptedRecovery
            // which is called by the orchestrator before this method

            if (ThrowExceptionOnRecovery)
            {
                throw new InvalidOperationException("Simulated recovery failure");
            }

            return Task.FromResult(RecoverySuccessful);
        }
    }

    /// <summary>
    /// A configurable mock recovery strategy that can simulate different recovery behaviors.
    /// </summary>
    public class ConfigurableMockRecoveryStrategy : ITestableRecoveryStrategy
    {
        private readonly Func<IApplicationError?, bool> _canRecoverFunc;
        private readonly Func<IApplicationError?, CancellationToken, Task<bool>> _attemptRecoveryFunc;

        public string Name { get; }
        public int Priority { get; set; } = 100;
        public ErrorTaxonomy.RecoveryComplexity ComplexityLevel { get; set; } = ErrorTaxonomy.RecoveryComplexity.Simple;
        public ErrorTaxonomy.RootCause[] SupportedRootCauses { get; set; } = { ErrorTaxonomy.RootCause.Unknown };
    public List<IApplicationError?> AttemptedRecoveries { get; } = new();

        public ConfigurableMockRecoveryStrategy(
            string name,
            Func<IApplicationError?, bool> canRecoverFunc,
            Func<IApplicationError?, CancellationToken, Task<bool>> attemptRecoveryFunc)
        {
            Name = name;
            _canRecoverFunc = canRecoverFunc;
            _attemptRecoveryFunc = attemptRecoveryFunc;
        }

        public bool SupportsErrorType(IApplicationError? error) => _canRecoverFunc(error);

        public bool CanRecover(IApplicationError? error) => _canRecoverFunc(error);

        public Task<bool> AttemptRecoveryAsync(IApplicationError? error, CancellationToken ct = default) =>
            _attemptRecoveryFunc(error, ct);
            
        public void RecordAttemptedRecovery(IApplicationError? error)
        {
            AttemptedRecoveries.Add(error);
        }
    }
}
