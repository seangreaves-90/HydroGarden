namespace HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling.RecoveryStrategy
{
    public interface IResiliencePolicy
    {
        string Name { get; }
        Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken ct = default);
        Task ExecuteAsync(Func<Task> operation, CancellationToken ct = default);

        /// <summary>
        /// Resets all circuit breakers that match the specified pattern.
        /// </summary>
        /// <param name="namePattern">Pattern to match circuit breaker names.</param>
        /// <param name="ct">Cancellation token.</param>
        Task ResetCircuitBreakersAsync(string namePattern, CancellationToken ct = default);
    }
}
