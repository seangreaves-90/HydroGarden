using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling.RecoveryStrategy;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;

namespace HydroGarden.Foundation.Common.ErrorHandling.RecoveryStrategy
{
    public class ComponentRestartRecoveryStrategy : IRecoveryStrategy
    {
        // Implementation of device restart recovery
        public string Name => "Component Restart";

        public bool CanRecover(IApplicationError error)
        {
            return true;
        }

        public async Task<bool> AttemptRecoveryAsync(IApplicationError error, CancellationToken ct = default)
        {
            return true;
        }
    }
}