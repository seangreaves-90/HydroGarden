using System;
using System.Threading;
using System.Threading.Tasks;
using HydroGarden.Foundation.Abstractions.Interfaces;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.Abstractions.Interfaces.Logging;
using HydroGarden.Foundation.Common.ErrorHandling;
using HydroGarden.Foundation.Common.ErrorHandling.Constants;
using HydroGarden.Foundation.Common.Events;
using HydroGarden.Foundation.Common.Extensions;

namespace HydroGarden.Foundation.Common.Policies
{
    /// <summary>
    /// Enhanced circuit breaker policy with health checks and configurable parameters.
    /// </summary>
    /// <typeparam name="T">The type of operation result.</typeparam>
    public class CircuitBreakerPolicy<T> : ICircuitBreakerPolicy<T>, IDisposable
    {
        private readonly string _serviceName;
        private readonly ILogger _logger;
        private readonly IErrorMonitor _errorMonitor;
        private readonly CircuitBreakerConfig _config;

        private int _failureCount;
        private int _successCount;
        private DateTimeOffset _lastStateChange = DateTimeOffset.UtcNow;
        private DateTimeOffset _lastFailureTime;
        private readonly SemaphoreSlim _stateChangeLock = new(1, 1);
        private readonly Timer _healthCheckTimer;
        private readonly object _healthCheckTimerLock = new();
        private Func<Task<bool>>? _healthCheckCallback;

        /// <summary>
        /// Event raised when circuit state changes.
        /// </summary>
        public event EventHandler<CircuitStateChangedEventArgs>? StateChanged;

        /// <summary>
        /// Gets the current state of the circuit.
        /// </summary>
        public CircuitState State { get; private set; } = CircuitState.Closed;

        /// <summary>
        /// Gets the name of the service protected by this circuit breaker.
        /// </summary>
        public string ServiceName => _serviceName;

        /// <summary>
        /// Gets the configuration of this circuit breaker.
        /// </summary>
        public CircuitBreakerConfig Config => _config;

        /// <summary>
        /// Creates a new circuit breaker policy.
        /// </summary>
        public CircuitBreakerPolicy(
            string serviceName,
            ILogger logger,
            IErrorMonitor errorMonitor,
            CircuitBreakerConfig? config = null)
        {
            _serviceName = serviceName ?? throw new ArgumentNullException(nameof(serviceName));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _errorMonitor = errorMonitor ?? throw new ArgumentNullException(nameof(errorMonitor));
            _config = config ?? new CircuitBreakerConfig();

            // Initialize health check timer but don't start it yet
            _healthCheckTimer = new Timer(
                HealthCheckCallback,
                null,
                Timeout.InfiniteTimeSpan,
                Timeout.InfiniteTimeSpan);

            _logger.Log($"Circuit breaker '{serviceName}' initialized with config: " +
                        $"MaxFailures={_config.MaxFailures}, " +
                        $"ResetTimeout={_config.ResetTimeout}, " +
                        $"HalfOpenMaxAttempts={_config.HalfOpenMaxAttempts}");
        }

        /// <summary>
        /// Executes the operation with circuit breaker protection.
        /// </summary>
        public async Task<T> ExecuteAsync(Func<Task<T>> operation)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            await CheckCircuitState();

            try
            {
                T result = await operation();
                await RecordSuccessAsync();
                return result;
            }
            catch (Exception ex)
            {
                await RecordFailureAsync(ex);

                // Create and report an error so it can be tracked
                var error = new ComponentError(
                    Guid.Empty, // No device ID for service circuit breakers
                    ErrorCodes.Recovery.CIRCUIT_OPEN,
                    $"Circuit breaker open for service: {_serviceName}",
                    ErrorSeverity.Error,
                    true,
                    ErrorSource.Service,
                    true,
                    new Dictionary<string, object>
                    {
                        ["ServiceName"] = _serviceName,
                        ["CircuitState"] = State.ToString(),
                        ["LastFailureTime"] = _lastFailureTime
                    },
                    ex);

                await _errorMonitor.ReportErrorAsync(error);

                throw new CircuitBreakerOpenException(_serviceName, _lastFailureTime, ex);
            }
        }

        /// <summary>
        /// Executes a void operation with circuit breaker protection.
        /// </summary>
        public async Task ExecuteAsync(Func<Task> operation)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            await ExecuteAsync(async () =>
            {
                await operation();
            });
        }

        /// <summary>
        /// Registers a health check callback that will be called periodically when the circuit is open.
        /// </summary>
        public void RegisterHealthCheck(Func<Task<bool>> healthCheckCallback)
        {
            _healthCheckCallback = healthCheckCallback;
            _logger.Log($"Health check registered for circuit breaker '{_serviceName}'");
        }

        /// <summary>
        /// Manually trips the circuit to open state.
        /// </summary>
        public async Task TripAsync(string reason)
        {
            await _stateChangeLock.WaitAsync();
            try
            {
                if (State != CircuitState.Open)
                {
                    await TransitionToStateAsync(CircuitState.Open, reason);
                }
            }
            finally
            {
                _stateChangeLock.Release();
            }
        }

        /// <summary>
        /// Manually resets the circuit to closed state.
        /// </summary>
        public async Task ResetAsync()
        {
            await _stateChangeLock.WaitAsync();
            try
            {
                if (State != CircuitState.Closed)
                {
                    await TransitionToStateAsync(CircuitState.Closed, "Manual reset");
                }
            }
            finally
            {
                _stateChangeLock.Release();
            }
        }

        /// <summary>
        /// Records an operation success.
        /// </summary>
        private async Task RecordSuccessAsync()
        {
            await _stateChangeLock.WaitAsync();
            try
            {
                switch (State)
                {
                    case CircuitState.HalfOpen:
                        _successCount++;
                        _logger.Log($"Circuit breaker '{_serviceName}' success {_successCount} in half-open state");

                        if (_successCount >= _config.HalfOpenMaxAttempts)
                        {
                            await TransitionToStateAsync(CircuitState.Closed, "Success threshold reached");
                        }
                        break;

                    case CircuitState.Closed:
                        _failureCount = 0;
                        break;
                }
            }
            finally
            {
                _stateChangeLock.Release();
            }
        }

        /// <summary>
        /// Records an operation failure.
        /// </summary>
        private async Task RecordFailureAsync(Exception ex)
        {
            _lastFailureTime = DateTimeOffset.UtcNow;

            await _stateChangeLock.WaitAsync();
            try
            {
                switch (State)
                {
                    case CircuitState.Closed:
                        _failureCount++;
                        _successCount = 0;
                        _logger.Log(ex, $"Circuit breaker '{_serviceName}' failure {_failureCount}/{_config.MaxFailures}");

                        if (_failureCount >= _config.MaxFailures)
                        {
                            await TransitionToStateAsync(CircuitState.Open, $"Failure threshold reached: {ex.Message}");
                        }
                        break;

                    case CircuitState.HalfOpen:
                        _logger.Log(ex, $"Circuit breaker '{_serviceName}' failure in half-open state");
                        await TransitionToStateAsync(CircuitState.Open, $"Failure in half-open state: {ex.Message}");
                        break;
                }
            }
            finally
            {
                _stateChangeLock.Release();
            }
        }

        /// <summary>
        /// Checks the current circuit state and handles transitions.
        /// </summary>
        private async Task CheckCircuitState()
        {
            if (State == CircuitState.Open)
            {
                if (DateTimeOffset.UtcNow - _lastStateChange > _config.ResetTimeout)
                {
                    await _stateChangeLock.WaitAsync();
                    try
                    {
                        if (State == CircuitState.Open &&
                            DateTimeOffset.UtcNow - _lastStateChange > _config.ResetTimeout)
                        {
                            await TransitionToStateAsync(CircuitState.HalfOpen, "Reset timeout elapsed");
                        }
                    }
                    finally
                    {
                        _stateChangeLock.Release();
                    }
                }
                else
                {
                    throw new CircuitBreakerOpenException(_serviceName, _lastFailureTime);
                }
            }

            if (State == CircuitState.HalfOpen)
            {
                if (_successCount >= _config.HalfOpenMaxAttempts)
                {
                    throw new CircuitBreakerOpenException(_serviceName, _lastFailureTime);
                }
            }
        }

        /// <summary>
        /// Transitions the circuit to a new state.
        /// </summary>
        private async Task TransitionToStateAsync(CircuitState newState, string reason)
        {
            var oldState = State;
            State = newState;
            _lastStateChange = DateTimeOffset.UtcNow;

            switch (newState)
            {
                case CircuitState.Closed:
                    _failureCount = 0;
                    _successCount = 0;
                    StopHealthCheck();
                    break;

                case CircuitState.HalfOpen:
                    _successCount = 0;
                    break;

                case CircuitState.Open:
                    StartHealthCheck();
                    break;
            }

            _logger.Log($"Circuit breaker '{_serviceName}' state changed from {oldState} to {newState}. Reason: {reason}");

            StateChanged?.Invoke(this, new CircuitStateChangedEventArgs(
                _serviceName, oldState, newState, _lastFailureTime, reason));

            await Task.CompletedTask;
        }

        /// <summary>
        /// Starts the health check timer.
        /// </summary>
        private void StartHealthCheck()
        {
            if (_healthCheckCallback == null)
                return;

            lock (_healthCheckTimerLock)
            {
                _healthCheckTimer.Change(
                    _config.HealthCheckInterval,
                    _config.HealthCheckInterval);

                _logger.Log($"Health check timer started for circuit breaker '{_serviceName}'");
            }
        }

        /// <summary>
        /// Stops the health check timer.
        /// </summary>
        private void StopHealthCheck()
        {
            lock (_healthCheckTimerLock)
            {
                _healthCheckTimer.Change(
                    Timeout.InfiniteTimeSpan,
                    Timeout.InfiniteTimeSpan);

                _logger.Log($"Health check timer stopped for circuit breaker '{_serviceName}'");
            }
        }

        /// <summary>
        /// Callback for health check timer.
        /// </summary>
        private async void HealthCheckCallback(object? state)
        {
            if (_healthCheckCallback == null || State != CircuitState.Open)
                return;

            try
            {
                _logger.Log($"Performing health check for circuit breaker '{_serviceName}'");
                bool isHealthy = await _healthCheckCallback();

                if (isHealthy)
                {
                    _logger.Log($"Health check passed for circuit breaker '{_serviceName}'");
                    await _stateChangeLock.WaitAsync();
                    try
                    {
                        if (State == CircuitState.Open)
                        {
                            await TransitionToStateAsync(CircuitState.HalfOpen, "Health check passed");
                        }
                    }
                    finally
                    {
                        _stateChangeLock.Release();
                    }
                }
                else
                {
                    _logger.Log($"Health check failed for circuit breaker '{_serviceName}'");
                }
            }
            catch (Exception ex)
            {
                _logger.Log(ex, $"Health check failed with exception for circuit breaker '{_serviceName}'");
            }
        }

        /// <summary>
        /// Disposes the circuit breaker.
        /// </summary>
        public void Dispose()
        {
            _stateChangeLock.Dispose();
            _healthCheckTimer.Dispose();
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// Configuration for circuit breaker behavior.
    /// </summary>
    public class CircuitBreakerConfig
    {
        /// <summary>
        /// Maximum number of failures before opening the circuit.
        /// </summary>
        public int MaxFailures { get; set; } = 3;

        /// <summary>
        /// Time to wait before trying to reset the circuit.
        /// </summary>
        public TimeSpan ResetTimeout { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Number of successful operations in half-open state before closing the circuit.
        /// </summary>
        public int HalfOpenMaxAttempts { get; set; } = 2;

        /// <summary>
        /// Interval for health checks when the circuit is open.
        /// </summary>
        public TimeSpan HealthCheckInterval { get; set; } = TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// Exception thrown when a circuit breaker is open.
    /// </summary>
    public class CircuitBreakerOpenException : Exception
    {
        /// <summary>
        /// Name of the service protected by the circuit breaker.
        /// </summary>
        public string ServiceName { get; }

        /// <summary>
        /// Time of the last failure that caused the circuit to open.
        /// </summary>
        public DateTimeOffset LastFailureTime { get; }

        /// <summary>
        /// Creates a new circuit breaker open exception.
        /// </summary>
        public CircuitBreakerOpenException(string serviceName, DateTimeOffset lastFailureTime)
            : base($"Circuit breaker is open for service: {serviceName}")
        {
            ServiceName = serviceName;
            LastFailureTime = lastFailureTime;
        }

        /// <summary>
        /// Creates a new circuit breaker open exception with inner exception.
        /// </summary>
        public CircuitBreakerOpenException(string serviceName, DateTimeOffset lastFailureTime, Exception innerException)
            : base($"Circuit breaker is open for service: {serviceName}", innerException)
        {
            ServiceName = serviceName;
            LastFailureTime = lastFailureTime;
        }
    }
}