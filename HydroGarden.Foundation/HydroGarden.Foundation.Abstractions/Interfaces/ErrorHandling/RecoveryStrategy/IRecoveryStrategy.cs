

namespace HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling.RecoveryStrategy
{
    public interface IRecoveryStrategy
    {
        string Name { get; }
        bool CanRecover(IApplicationError error);
        Task<bool> AttemptRecoveryAsync(IApplicationError error, CancellationToken ct = default);
    }
}
