namespace HydroGarden.Foundation.ErrorHandling.Interfaces
{
    /// <summary>
    /// Interface for resilience policies that manage circuit breakers and other resiliency mechanisms.
    /// </summary>
    public interface IResiliencePolicy
    {
        /// <summary>
        /// Resets all circuit breakers for a specified service key.
        /// </summary>
        /// <param name="serviceKey">The service key identifying the circuit.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task ResetCircuitBreakersAsync(string serviceKey, CancellationToken cancellationToken);
        
        /// <summary>
        /// Checks if a service circuit is open (tripped).
        /// </summary>
        /// <param name="serviceKey">The service key identifying the circuit.</param>
        /// <returns>True if the circuit is open, false otherwise.</returns>
        bool IsCircuitOpen(string serviceKey);
    }
}