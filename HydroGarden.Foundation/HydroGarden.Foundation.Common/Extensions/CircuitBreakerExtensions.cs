using System;
using System.Threading.Tasks;
using HydroGarden.Foundation.Abstractions.Interfaces;
using HydroGarden.Foundation.Common.Policies;

namespace HydroGarden.Foundation.Common.Extensions
{
    /// <summary>
    /// Extension methods for applying circuit breakers to operations.
    /// </summary>
    public static class CircuitBreakerExtensions
    {
        /// <summary>
        /// Executes an operation with circuit breaker protection.
        /// </summary>
        /// <typeparam name="T">The type of the operation result.</typeparam>
        /// <param name="factory">The circuit breaker factory.</param>
        /// <param name="serviceName">The name of the service to protect.</param>
        /// <param name="operation">The operation to execute.</param>
        /// <returns>The result of the operation.</returns>
        public static Task<T> ExecuteWithCircuitBreakerAsync<T>(
            this CircuitBreakerFactory factory,
            string serviceName,
            Func<Task<T>> operation)
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            var circuitBreaker = factory.GetOrCreate<T>(serviceName);
            return circuitBreaker.ExecuteAsync(operation);
        }

        /// <summary>
        /// Executes a void operation with circuit breaker protection.
        /// </summary>
        /// <param name="factory">The circuit breaker factory.</param>
        /// <param name="serviceName">The name of the service to protect.</param>
        /// <param name="operation">The operation to execute.</param>
        public static async Task ExecuteWithCircuitBreakerAsync(
            this CircuitBreakerFactory factory,
            string serviceName,
            Func<Task> operation)
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            var circuitBreaker = factory.GetOrCreate<bool>(serviceName);

            await circuitBreaker.ExecuteAsync(async () =>
            {
                await operation();
                return true;
            });
        }

        /// <summary>
        /// Executes an operation with circuit breaker protection and fallback.
        /// </summary>
        /// <typeparam name="T">The type of the operation result.</typeparam>
        /// <param name="factory">The circuit breaker factory.</param>
        /// <param name="serviceName">The name of the service to protect.</param>
        /// <param name="operation">The operation to execute.</param>
        /// <param name="fallback">The fallback value to return if the circuit is open.</param>
        /// <returns>The result of the operation or the fallback value.</returns>
        public static async Task<T> ExecuteWithCircuitBreakerAndFallbackAsync<T>(
            this CircuitBreakerFactory factory,
            string serviceName,
            Func<Task<T>> operation,
            T fallback)
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            try
            {
                return await factory.ExecuteWithCircuitBreakerAsync(serviceName, operation);
            }
            catch (CircuitBreakerOpenException)
            {
                return fallback;
            }
        }

        /// <summary>
        /// Executes an operation with circuit breaker protection and dynamic fallback.
        /// </summary>
        /// <typeparam name="T">The type of the operation result.</typeparam>
        /// <param name="factory">The circuit breaker factory.</param>
        /// <param name="serviceName">The name of the service to protect.</param>
        /// <param name="operation">The operation to execute.</param>
        /// <param name="fallbackOperation">The fallback operation to execute if the circuit is open.</param>
        /// <returns>The result of the operation or the fallback operation.</returns>
        public static async Task<T> ExecuteWithCircuitBreakerAndFallbackAsync<T>(
            this CircuitBreakerFactory factory,
            string serviceName,
            Func<Task<T>> operation,
            Func<CircuitBreakerOpenException, Task<T>> fallbackOperation)
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            if (fallbackOperation == null)
                throw new ArgumentNullException(nameof(fallbackOperation));

            try
            {
                return await factory.ExecuteWithCircuitBreakerAsync(serviceName, operation);
            }
            catch (CircuitBreakerOpenException ex)
            {
                return await fallbackOperation(ex);
            }
        }
    }
}