namespace HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling.RecoveryStrategy
{
    public interface IResiliencePolicy
    {
        string Name { get; }
        Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken ct = default);
        Task ExecuteAsync(Func<Task> operation, CancellationToken ct = default);
    }
}
