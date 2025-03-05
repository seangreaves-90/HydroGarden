using HydroGarden.Foundation.Abstractions.Interfaces;
using HydroGarden.Foundation.Abstractions.Interfaces.Logging;

namespace HydroGarden.Foundation.Common.CircuitBreakerPolicy
{
    public class CircuitBreakerPolicy<T>(
        string serviceName,
        ILogger logger,
        int maxFailures = 3,
        TimeSpan? resetTimeout = null)
        : ICircuitBreakerPolicy<T>
    {
        private readonly TimeSpan _resetTimeout = resetTimeout ?? TimeSpan.FromMinutes(1);

        private int _failureCount;
        private bool _isOpen;
        private DateTimeOffset _lastFailureTime;

        public async Task<T> ExecuteAsync(Func<Task<T>> operation)
        {
            if (_isOpen)
            {
                // Check if we should try to reset
                if (DateTimeOffset.UtcNow - _lastFailureTime > _resetTimeout)
                {
                    logger.Log($"Circuit half-open for {serviceName}, attempting reset");
                    _isOpen = false;
                }
                else
                {
                    throw new CircuitBreakerOpenException(serviceName, _lastFailureTime);
                }
            }

            try
            {
                var result = await operation();
                Reset();
                return result;
            }
            catch (Exception ex)
            {
                RecordFailure(ex);
                throw;
            }
        }

        private void RecordFailure(Exception ex)
        {
            _lastFailureTime = DateTimeOffset.UtcNow;
            _failureCount++;

            if (_failureCount >= maxFailures)
            {
                _isOpen = true;
                logger.Log(ex, $"Circuit breaker opened for {serviceName} after {_failureCount} failures");
            }
        }

        private void Reset()
        {
            _failureCount = 0;
            _isOpen = false;
        }
    }

    public class CircuitBreakerOpenException(string serviceName, DateTimeOffset lastFailureTime)
        : Exception($"Circuit breaker is open for service: {serviceName}")
    {
        public string ServiceName { get; } = serviceName;
        public DateTimeOffset LastFailureTime { get; } = lastFailureTime;
    }
}
