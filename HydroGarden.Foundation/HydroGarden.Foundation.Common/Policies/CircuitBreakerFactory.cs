
using System.Collections.Concurrent;
using HydroGarden.Foundation.Abstractions.Interfaces;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Logger.Abstractions;

namespace HydroGarden.Foundation.Common.Policies
{
    /// <summary>
    /// Factory for creating and managing circuit breaker policies.
    /// </summary>
    public class CircuitBreakerFactory : IDisposable
    {
        private readonly ILogger _logger;
        private readonly IErrorMonitor _errorMonitor;
        private readonly ConcurrentDictionary<string, object> _circuitBreakers = new();
        private readonly ConcurrentDictionary<string, CircuitBreakerConfig> _configurations = new();
        private bool _isDisposed;

        /// <summary>
        /// Creates a new circuit breaker factory.
        /// </summary>
        public CircuitBreakerFactory(ILogger logger, IErrorMonitor errorMonitor)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _errorMonitor = errorMonitor ?? throw new ArgumentNullException(nameof(errorMonitor));
        }

        /// <summary>
        /// Gets or creates a circuit breaker with the specified name and result type.
        /// </summary>
        public ICircuitBreakerPolicy<T> GetOrCreate<T>(string serviceName, CircuitBreakerConfig? config = null)
        {
            if (string.IsNullOrEmpty(serviceName))
                throw new ArgumentException("Service name cannot be null or empty", nameof(serviceName));

            return (ICircuitBreakerPolicy<T>)_circuitBreakers.GetOrAdd(
                GetCircuitBreakerKey(serviceName, typeof(T)),
                _ =>
                {
                    // Use configured config or default
                    var effectiveConfig = config ?? _configurations.GetValueOrDefault(serviceName) ?? new CircuitBreakerConfig();
                    var circuitBreaker = new CircuitBreakerPolicy<T>(serviceName, _logger, _errorMonitor, effectiveConfig);

                    _logger.Log($"Created circuit breaker for service '{serviceName}' with result type {typeof(T).Name}");
                    return circuitBreaker;
                });
        }

        /// <summary>
        /// Registers a global configuration for a service circuit breaker.
        /// </summary>
        public void RegisterConfiguration(string serviceName, CircuitBreakerConfig config)
        {
            if (string.IsNullOrEmpty(serviceName))
                throw new ArgumentException("Service name cannot be null or empty", nameof(serviceName));

            if (config == null)
                throw new ArgumentNullException(nameof(config));

            _configurations[serviceName] = config;
            _logger.Log($"Registered configuration for service '{serviceName}'");
        }

        /// <summary>
        /// Trips a circuit breaker to open state.
        /// </summary>
        public async Task TripCircuitBreakerAsync<T>(string serviceName, string reason)
        {
            var key = GetCircuitBreakerKey(serviceName, typeof(T));

            if (_circuitBreakers.TryGetValue(key, out var circuitBreaker) &&
                circuitBreaker is CircuitBreakerPolicy<T> typedBreaker)
            {
                await typedBreaker.TripAsync(reason);
                _logger.Log($"Manually tripped circuit breaker for service '{serviceName}'. Reason: {reason}");
            }
        }

        /// <summary>
        /// Resets a circuit breaker to closed state.
        /// </summary>
        public async Task ResetCircuitBreakerAsync<T>(string serviceName)
        {
            var key = GetCircuitBreakerKey(serviceName, typeof(T));

            if (_circuitBreakers.TryGetValue(key, out var circuitBreaker) &&
                circuitBreaker is CircuitBreakerPolicy<T> typedBreaker)
            {
                await typedBreaker.ResetAsync();
                _logger.Log($"Manually reset circuit breaker for service '{serviceName}'");
            }
        }

        /// <summary>
        /// Gets the state of a circuit breaker.
        /// </summary>
        public CircuitState? GetCircuitBreakerState<T>(string serviceName)
        {
            var key = GetCircuitBreakerKey(serviceName, typeof(T));

            if (_circuitBreakers.TryGetValue(key, out var circuitBreaker) &&
                circuitBreaker is CircuitBreakerPolicy<T> typedBreaker)
            {
                return typedBreaker.State;
            }

            return null;
        }

        /// <summary>
        /// Registers a health check for a circuit breaker.
        /// </summary>
        public void RegisterHealthCheck<T>(string serviceName, Func<Task<bool>> healthCheck)
        {
            var circuitBreaker = GetOrCreate<T>(serviceName);

            if (circuitBreaker is CircuitBreakerPolicy<T> enhancedBreaker)
            {
                enhancedBreaker.RegisterHealthCheck(healthCheck);
                _logger.Log($"Registered health check for circuit breaker '{serviceName}'");
            }
        }

        /// <summary>
        /// Gets a unique key for a circuit breaker.
        /// </summary>
        private static string GetCircuitBreakerKey(string serviceName, Type resultType)
        {
            return $"{serviceName}:{resultType.FullName}";
        }

        /// <summary>
        /// Disposes all circuit breakers.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;

            foreach (var circuitBreaker in _circuitBreakers.Values)
            {
                if (circuitBreaker is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }

            _circuitBreakers.Clear();
            _configurations.Clear();
            _isDisposed = true;

            GC.SuppressFinalize(this);
        }
    }
}