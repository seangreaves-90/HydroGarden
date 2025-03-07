

namespace HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling
{
    public enum CircuitState
    {
        Closed,    // Normal operation - requests pass through
        Open,      // Failure threshold exceeded - requests fail fast
        HalfOpen   // Testing recovery - limited requests allowed through
    }

    public interface ICircuitBreakerPolicy<T>
    {
        Task<T> ExecuteAsync(Func<Task<T>> operation);
    }
}
