

namespace HydroGarden.ErrorHandling.Core.Interfaces.RecoveryStrategy
{
    public interface IRecoveryStrategy
    {
        string Name { get; }
        bool CanRecover(IApplicationError error);
        Task<bool> AttemptRecoveryAsync(IApplicationError error, CancellationToken ct = default);
    }
}
