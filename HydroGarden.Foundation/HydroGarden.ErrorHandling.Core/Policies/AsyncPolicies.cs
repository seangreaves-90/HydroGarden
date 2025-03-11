using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using HydroGarden.Logger.Abstractions;

namespace HydroGarden.Foundation.ErrorHandling.Policies
{
    /// <summary>
    /// Exception thrown when a circuit is open.
    /// </summary>
    public class BrokenCircuitException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BrokenCircuitException"/> class.
        /// </summary>
        /// <param name="message">The error message.</param>
        public BrokenCircuitException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// AsyncRetryPolicy implementation compatible with Polly.Core for .NET 8
    /// </summary>
    public class AsyncRetryPolicy
    {
        private readonly ILogger _logger;
        private readonly int _maxRetries;
        private readonly Func<int, TimeSpan> _sleepDurationProvider;
        private readonly Action<Exception, TimeSpan, int, Dictionary<string, object>> _onRetry;

        /// <summary>
        /// Initializes a new instance of the AsyncRetryPolicy class.
        /// </summary>
        public AsyncRetryPolicy(
            ILogger logger,
            int maxRetries,
            Func<int, TimeSpan> sleepDurationProvider,
            Action<Exception, TimeSpan, int, Dictionary<string, object>> onRetry)
        {
            _logger = logger;
            _maxRetries = maxRetries;
            _sleepDurationProvider = sleepDurationProvider;
            _onRetry = onRetry;
        }

        /// <summary>
        /// Executes the specified asynchronous action with retry logic.
        /// </summary>
        public async Task<T> ExecuteAsync<T>(Func<Dictionary<string, object>, CancellationToken, Task<T>> action, Dictionary<string, object> context, CancellationToken cancellationToken)
        {
            var exceptions = new List<Exception>();
            
            for (int attempt = 0; attempt <= _maxRetries; attempt++)
            {
                try
                {
                    return await action(context, cancellationToken);
                }
                catch (Exception ex) when (!(ex is OperationCanceledException) && attempt < _maxRetries)
                {
                    exceptions.Add(ex);
                    
                    var sleepDuration = _sleepDurationProvider(attempt);
                    _onRetry(ex, sleepDuration, attempt, context);
                    
                    try
                    {
                        await Task.Delay(sleepDuration, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                }
            }
            
            throw new AggregateException("All retry attempts failed", exceptions);
        }
    }

    /// <summary>
    /// Circuit state.
    /// </summary>
    public enum CircuitState
    {
        /// <summary>
        /// Circuit is closed, allowing operations to execute.
        /// </summary>
        Closed,

        /// <summary>
        /// Circuit is open, preventing operations from executing.
        /// </summary>
        Open,

        /// <summary>
        /// Circuit is half-open, allowing a test operation to execute.
        /// </summary>
        HalfOpen
    }

    /// <summary>
    /// AsyncCircuitBreakerPolicy implementation compatible with Polly.Core for .NET 8
    /// </summary>
    public class AsyncCircuitBreakerPolicy
    {
        private readonly ILogger _logger;
        private readonly int _exceptionsAllowedBeforeBreaking;
        private readonly TimeSpan _durationOfBreak;
        private readonly Action<Exception, TimeSpan> _onBreak;
        private readonly Action _onReset;
        private readonly Action _onHalfOpen;
        
        private int _consecutiveFailures;
        private DateTime _circuitOpenTime;
        private CircuitState _circuitState = CircuitState.Closed;
        private readonly object _lockObject = new object();
        private bool _halfOpenFirstAttemptInFlight;

        /// <summary>
        /// Initializes a new instance of the AsyncCircuitBreakerPolicy class.
        /// </summary>
        public AsyncCircuitBreakerPolicy(
            ILogger logger,
            int exceptionsAllowedBeforeBreaking,
            TimeSpan durationOfBreak,
            Action<Exception, TimeSpan> onBreak,
            Action onReset,
            Action onHalfOpen)
        {
            _logger = logger;
            _exceptionsAllowedBeforeBreaking = exceptionsAllowedBeforeBreaking;
            _durationOfBreak = durationOfBreak;
            _onBreak = onBreak;
            _onReset = onReset;
            _onHalfOpen = onHalfOpen;
        }

        /// <summary>
        /// Gets the current circuit state.
        /// </summary>
        public CircuitState State => _circuitState;

        /// <summary>
        /// Executes the specified asynchronous action with circuit breaker logic.
        /// </summary>
        public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            bool lockTaken = false;
            try
            {
                Monitor.TryEnter(_lockObject, ref lockTaken);
                
                if (lockTaken)
                {
                    switch (_circuitState)
                    {
                        case CircuitState.Open:
                            if (DateTime.UtcNow >= _circuitOpenTime.Add(_durationOfBreak))
                            {
                                _circuitState = CircuitState.HalfOpen;
                                _onHalfOpen();
                            }
                            else
                            {
                                throw new BrokenCircuitException($"Circuit is open for {_durationOfBreak.TotalSeconds} seconds");
                            }
                            break;
                            
                        case CircuitState.HalfOpen:
                            if (_halfOpenFirstAttemptInFlight)
                            {
                                throw new BrokenCircuitException("Circuit is half-open and test operation is in flight");
                            }
                            _halfOpenFirstAttemptInFlight = true;
                            break;
                    }
                }
                else
                {
                    // If we couldn't acquire the lock, just check if the circuit is open
                    if (_circuitState == CircuitState.Open && DateTime.UtcNow < _circuitOpenTime.Add(_durationOfBreak))
                    {
                        throw new BrokenCircuitException($"Circuit is open for {_durationOfBreak.TotalSeconds} seconds");
                    }
                }
            }
            finally
            {
                if (lockTaken)
                {
                    Monitor.Exit(_lockObject);
                }
            }

            try
            {
                T result = await action(cancellationToken);
                
                // If we get here, the action succeeded
                lock (_lockObject)
                {
                    if (_circuitState == CircuitState.HalfOpen)
                    {
                        // Reset circuit breaker
                        _circuitState = CircuitState.Closed;
                        _consecutiveFailures = 0;
                        _onReset();
                    }
                    else if (_circuitState == CircuitState.Closed)
                    {
                        _consecutiveFailures = 0;
                    }
                    
                    _halfOpenFirstAttemptInFlight = false;
                }
                
                return result;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                lock (_lockObject)
                {
                    if (_circuitState == CircuitState.HalfOpen)
                    {
                        // Failed test, keep circuit open
                        _circuitState = CircuitState.Open;
                        _circuitOpenTime = DateTime.UtcNow;
                        _onBreak(ex, _durationOfBreak);
                    }
                    else if (_circuitState == CircuitState.Closed)
                    {
                        _consecutiveFailures++;
                        
                        if (_consecutiveFailures >= _exceptionsAllowedBeforeBreaking)
                        {
                            // Open the circuit
                            _circuitState = CircuitState.Open;
                            _circuitOpenTime = DateTime.UtcNow;
                            _onBreak(ex, _durationOfBreak);
                        }
                    }
                    
                    _halfOpenFirstAttemptInFlight = false;
                }
                
                throw;
            }
        }

        /// <summary>
        /// Resets the circuit breaker to closed state.
        /// </summary>
        public void Reset()
        {
            lock (_lockObject)
            {
                _circuitState = CircuitState.Closed;
                _consecutiveFailures = 0;
                _halfOpenFirstAttemptInFlight = false;
                _onReset();
            }
        }
    }

    /// <summary>
    /// Provides extension methods for working with async policies.
    /// </summary>
    public static class PolicyExtensions
    {
        /// <summary>
        /// Creates an instance of AsyncRetryPolicy.
        /// </summary>
        public static AsyncRetryPolicy CreateRetryPolicy(
            ILogger logger,
            int maxRetries = 3,
            Func<int, TimeSpan>? sleepDurationProvider = null,
            Action<Exception, TimeSpan, int, Dictionary<string, object>>? onRetry = null)
        {
            sleepDurationProvider ??= retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt));
            onRetry ??= (ex, ts, rc, ctx) => { };
            
            return new AsyncRetryPolicy(logger, maxRetries, sleepDurationProvider, onRetry);
        }

        /// <summary>
        /// Creates an instance of AsyncCircuitBreakerPolicy.
        /// </summary>
        public static AsyncCircuitBreakerPolicy CreateCircuitBreakerPolicy(
            ILogger logger,
            int exceptionsAllowedBeforeBreaking = 5,
            TimeSpan? durationOfBreak = null,
            Action<Exception, TimeSpan>? onBreak = null,
            Action? onReset = null,
            Action? onHalfOpen = null)
        {
            durationOfBreak ??= TimeSpan.FromMinutes(5);
            onBreak ??= (ex, ts) => { };
            onReset ??= () => { };
            onHalfOpen ??= () => { };
            
            return new AsyncCircuitBreakerPolicy(
                logger,
                exceptionsAllowedBeforeBreaking,
                durationOfBreak.Value,
                onBreak,
                onReset,
                onHalfOpen);
        }

        /// <summary>
        /// Wraps two async policies together.
        /// </summary>
        public static async Task<T> ExecuteWithPoliciesAsync<T>(
            this AsyncRetryPolicy retryPolicy,
            AsyncCircuitBreakerPolicy circuitBreakerPolicy,
            Func<Dictionary<string, object>, CancellationToken, Task<T>> action,
            Dictionary<string, object> context,
            CancellationToken cancellationToken)
        {
            return await retryPolicy.ExecuteAsync(
                async (ctx, ct) => await circuitBreakerPolicy.ExecuteAsync(
                    async innerCt => await action(ctx, innerCt),
                    ct),
                context,
                cancellationToken);
        }
    }
}
