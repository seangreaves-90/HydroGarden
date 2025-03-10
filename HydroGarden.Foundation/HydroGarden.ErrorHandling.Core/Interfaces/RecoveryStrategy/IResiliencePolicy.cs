namespace HydroGarden.ErrorHandling.Core.Interfaces.RecoveryStrategy
{
    public interface IResiliencePolicy
    {
        string Name { get; }
        Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken ct = default);
        Task ExecuteAsync(Func<Task> operation, CancellationToken ct = default);
    }
}
