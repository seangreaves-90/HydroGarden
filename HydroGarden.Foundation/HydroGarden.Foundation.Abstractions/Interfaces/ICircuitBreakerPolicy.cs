

namespace HydroGarden.Foundation.Abstractions.Interfaces
{
    public interface ICircuitBreakerPolicy<T>
    {
        Task<T> ExecuteAsync(Func<Task<T>> operation);
    }
}
