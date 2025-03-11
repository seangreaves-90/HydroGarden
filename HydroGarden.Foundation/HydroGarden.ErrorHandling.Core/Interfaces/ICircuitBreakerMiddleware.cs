namespace HydroGarden.Foundation.ErrorHandling.Interfaces
{
    /// <summary>
    /// Interface for circuit breaker middleware components that control the flow of operations
    /// based on failure states. This enables better testability and decoupling.
    /// </summary>
    public interface ICircuitBreakerMiddleware
    {
        /// <summary>
        /// Gets the current state of a circuit for a specific service.
        /// </summary>
        /// <param name="serviceKey">The service identifier.</param>
        /// <returns>The current state of the circuit.</returns>
        object GetCircuitState(string serviceKey);

        /// <summary>
        /// Attempts to reset a circuit from open to half-open or closed state.
        /// </summary>
        /// <param name="serviceKey">The service identifier.</param>
        void ResetCircuit(string serviceKey);
    }
}