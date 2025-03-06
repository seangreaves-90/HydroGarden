using HydroGarden.Foundation.Abstractions.Interfaces;
using HydroGarden.Foundation.Abstractions.Interfaces.Logging;

namespace HydroGarden.Foundation.Common.Policies
{
    public class CircuitBreakerPolicy<T>(
        string serviceName,
        ILogger logger,
        int maxFailures = 3,
        TimeSpan? resetTimeout = null,
        int halfOpenMaxAttempts = 1)
        : ICircuitBreakerPolicy<T>
    {
        private readonly TimeSpan _resetTimeout = resetTimeout ?? TimeSpan.FromMinutes(1);

        private int _failureCount;
        private int _successCount;
        private DateTimeOffset _lastStateChange = DateTimeOffset.UtcNow;
        private DateTimeOffset _lastFailureTime;
        private readonly SemaphoreSlim _stateChangeLock = new(1, 1);

        // State changed event to notify monitoring systems
        public event EventHandler<CircuitStateChangedEventArgs>? StateChanged;

        public CircuitState State { get; private set; } = CircuitState.Closed;

        public async Task<T> ExecuteAsync(Func<Task<T>> operation)
        {
            // Check if circuit is open
            if (State == CircuitState.Open)
            {
                // Check if we should try to reset
                if (DateTimeOffset.UtcNow - _lastStateChange > _resetTimeout)
                {
                    await TransitionToStateAsync(CircuitState.HalfOpen);
                }
                else
                {
                    throw new CircuitBreakerOpenException(serviceName, _lastFailureTime);
                }
            }

            // If half-open, only allow limited attempts
            if (State == CircuitState.HalfOpen)
            {
                // If we've reached max half-open attempts, fail fast
                if (_successCount >= halfOpenMaxAttempts)
                {
                    throw new CircuitBreakerOpenException(serviceName, _lastFailureTime);
                }
            }

            try
            {
                T result = await operation();

                // If successful, start closing circuit
                await RecordSuccessAsync();

                return result;
            }
            catch (Exception ex)
            {
                await RecordFailureAsync(ex);
                throw;
            }
        }

        private async Task RecordFailureAsync(Exception ex)
        {
            _lastFailureTime = DateTimeOffset.UtcNow;

            await _stateChangeLock.WaitAsync();
            try
            {
                _failureCount++;
                _successCount = 0;  // Reset success count on any failure

                logger.Log(ex, $"Circuit breaker '{serviceName}' failure {_failureCount}/{maxFailures}");

                if (State == CircuitState.Closed && _failureCount >= maxFailures)
                {
                    await TransitionToStateAsync(CircuitState.Open);
                }
                else if (State == CircuitState.HalfOpen)
                {
                    // Any failure in half-open state sends us back to open
                    await TransitionToStateAsync(CircuitState.Open);
                }
            }
            finally
            {
                _stateChangeLock.Release();
            }
        }

        private async Task RecordSuccessAsync()
        {
            await _stateChangeLock.WaitAsync();
            try
            {
                if (State == CircuitState.HalfOpen)
                {
                    _successCount++;
                    logger.Log($"Circuit breaker '{serviceName}' success {_successCount} in half-open state");

                    // If we've had enough successes, close the circuit
                    if (_successCount >= halfOpenMaxAttempts)
                    {
                        await TransitionToStateAsync(CircuitState.Closed);
                    }
                }
                else if (State == CircuitState.Closed)
                {
                    // Reset failure count after success
                    _failureCount = 0;
                }
            }
            finally
            {
                _stateChangeLock.Release();
            }
        }

        private async Task TransitionToStateAsync(CircuitState newState)
        {
            var oldState = State;
            State = newState;
            _lastStateChange = DateTimeOffset.UtcNow;

            // Reset counters on state change
            if (newState == CircuitState.Closed)
            {
                _failureCount = 0;
                _successCount = 0;
            }
            else if (newState == CircuitState.HalfOpen)
            {
                _successCount = 0;
            }

            logger.Log($"Circuit breaker '{serviceName}' state changed from {oldState} to {newState}");

            // Raise event
            StateChanged?.Invoke(this, new CircuitStateChangedEventArgs(
                serviceName, oldState, newState, _lastFailureTime));

            await Task.CompletedTask;
        }

        public void Dispose()
        {
            _stateChangeLock.Dispose();
        }
    }

    public class CircuitStateChangedEventArgs(
        string serviceName,
        CircuitState oldState,
        CircuitState newState,
        DateTimeOffset lastFailureTime)
        : EventArgs
    {
        public string ServiceName { get; } = serviceName;
        public CircuitState OldState { get; } = oldState;
        public CircuitState NewState { get; } = newState;
        public DateTimeOffset LastFailureTime { get; } = lastFailureTime;
    }

    public class CircuitBreakerOpenException(string serviceName, DateTimeOffset lastFailureTime)
        : Exception($"Circuit breaker is open for service: {serviceName}")
    {
        public string ServiceName { get; } = serviceName;
        public DateTimeOffset LastFailureTime { get; } = lastFailureTime;
    }
}
