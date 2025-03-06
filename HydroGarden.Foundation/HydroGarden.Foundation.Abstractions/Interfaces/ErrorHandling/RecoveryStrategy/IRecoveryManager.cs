
namespace HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling.RecoveryStrategy
{
    public interface IRecoveryManager
    {
        Task<bool> AttemptRecoveryAsync(IApplicationError error, CancellationToken ct = default);
        Task<IReadOnlyList<IRecoveryStrategy>> GetApplicableStrategiesAsync(IApplicationError error, CancellationToken ct = default);
        Task RegisterRecoveryStrategyAsync(IRecoveryStrategy strategy);
        Task<IRecoveryStatus> GetRecoveryStatusAsync(Guid deviceId, CancellationToken ct = default);
    }
}
