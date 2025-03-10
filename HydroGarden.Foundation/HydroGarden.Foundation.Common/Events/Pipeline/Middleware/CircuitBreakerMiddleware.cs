using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Logger.Abstractions;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace HydroGarden.Foundation.Common.Events.Pipeline.Middleware
{
    /// <summary>
    /// Circuit breaker states.
    /// </summary>
    public enum CircuitState
    {
        /// <summary>
        /// Circuit is closed, allowing events to flow through.
        /// </summary>
        Closed,

        /// <summary>
        /// Circuit is open, preventing events from flowing through.
        /// </summary>
        Open,

        /// <summary>
        /// Circuit is half-open, allowing a test event to flow through.
        /// </summary>
        HalfOpen
    }

    /// <summary>
    /// Middleware that implements a circuit breaker pattern to prevent cascading failures.
    /// </summary>
    public class CircuitBreakerMiddleware : IEventMiddleware, IDisposable
    {
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<EventType, CircuitBreakerState> _circuitBreakers = new();
        private readonly int _failureThreshold;
        private readonly TimeSpan _resetTimeout;
        private readonly Timer _cleanupTimer;
        private readonly object _cleanupLock = new();
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="CircuitBreakerMiddleware"/> class.
        /// </summary>
        /// <param name="logger">The logger to use.</param>
        /// <param name="failureThreshold">The number of consecutive failures before opening the circuit.</param>
        /// <param name="resetTimeout">The time to wait before attempting to close the circuit.</param>
        public CircuitBreakerMiddleware(
            ILogger logger,
            int failureThreshold = 5,
            TimeSpan? resetTimeout = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _failureThreshold = failureThreshold > 0 ? failureThreshold : throw new ArgumentOutOfRangeException(nameof(failureThreshold), "Must be greater than 0");
            _resetTimeout = resetTimeout ?? TimeSpan.FromMinutes(1);
            
            // Create a timer to periodically check and reset expired circuit breakers
            _cleanupTimer = new Timer(CleanupExpiredCircuits, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
            
            Id = Guid.NewGuid();
            Name = "Circuit Breaker Middleware";
            Order = 200; // Run early in the pipeline
        }

        /// <inheritdoc />
        public Guid Id { get; }

        /// <inheritdoc />
        public string Name { get; }

        /// <inheritdoc />
        public int Order { get; }

        /// <summary>
        /// Gets the current state of the circuit for a specific event type.
        /// </summary>
        /// <param name="eventType">The event type.</param>
        /// <returns>The current circuit state.</returns>
        public CircuitState GetCircuitState(EventType eventType)
        {
            if (_circuitBreakers.TryGetValue(eventType, out var state))
            {
                return state.State;
            }
            return CircuitState.Closed;
        }

        /// <summary>
        /// Gets the failure count for a specific event type.
        /// </summary>
        /// <param name="eventType">The event type.</param>
        /// <returns>The current failure count.</returns>
        public int GetFailureCount(EventType eventType)
        {
            if (_circuitBreakers.TryGetValue(eventType, out var state))
            {
                return state.FailureCount;
            }
            return 0;
        }

        /// <summary>
        /// Manually resets the circuit for a specific event type.
        /// </summary>
        /// <param name="eventType">The event type.</param>
        public void ResetCircuit(EventType eventType)
        {
            if (_circuitBreakers.TryGetValue(eventType, out var state))
            {
                state.Reset();
                _logger.Log($"Circuit for event type {eventType} manually reset to Closed state");
            }
        }

        /// <inheritdoc />
        public async Task<IEventProcessingResult> ProcessAsync(
            object sender,
            IEvent @event,
            Func<object, IEvent, CancellationToken, Task<IEventProcessingResult>> next,
            CancellationToken cancellationToken = default)
        {
            var eventType = @event.EventType;
            
            // Get or create the circuit breaker state for this event type
            var circuitBreaker = _circuitBreakers.GetOrAdd(eventType, _ => new CircuitBreakerState(_failureThreshold));
            
            // Check the current circuit state
            switch (circuitBreaker.State)
            {
                case CircuitState.Open:
                    // Check if we should try to close the circuit
                    if (circuitBreaker.ShouldAttemptReset())
                    {
                        _logger.Log($"Circuit for event type {eventType} transitioning to half-open state");
                        circuitBreaker.State = CircuitState.HalfOpen;
                        // Fall through to half-open case
                    }
                    else
                    {
                        // Circuit is open, reject the event
                        _logger.Log($"Circuit for event type {eventType} is open, rejecting event {@event.EventId}");
                        return EventProcessingResult.Failure(
                            @event,
                            new CircuitBreakerOpenException($"Circuit for event type {eventType} is open"),
                            shouldRetry: false);
                    }
                    break;
                
                case CircuitState.HalfOpen:
                    // Only allow one event through in half-open state
                    if (!circuitBreaker.TryAcquireTestSlot())
                    {
                        _logger.Log($"Circuit for event type {eventType} is half-open and test slot is in use, rejecting event {@event.EventId}");
                        return EventProcessingResult.Failure(
                            @event,
                            new CircuitBreakerOpenException($"Circuit for event type {eventType} is half-open and test slot is in use"),
                            shouldRetry: true);
                    }
                    break;
            }

            try
            {
                // Process the event through the rest of the pipeline
                var result = await next(sender, @event, cancellationToken);

                // Handle the result based on success/failure
                if (result.IsSuccess)
                {
                    // Success, reset failure count and close circuit if half-open
                    if (circuitBreaker.State == CircuitState.HalfOpen)
                    {
                        circuitBreaker.Reset();
                        _logger.Log($"Test event succeeded, circuit for event type {eventType} is now closed");
                    }
                    else
                    {
                        circuitBreaker.RecordSuccess();
                    }
                }
                else
                {
                    // Failure, increment count and potentially open circuit
                    bool circuitOpened = circuitBreaker.RecordFailure();
                    
                    if (circuitOpened)
                    {
                        _logger.Log($"Failure threshold reached, circuit for event type {eventType} is now open until {DateTime.UtcNow.Add(_resetTimeout)}");
                        
                        // Set the reset time
                        circuitBreaker.SetResetTime(_resetTimeout);
                    }
                    
                    if (circuitBreaker.State == CircuitState.HalfOpen)
                    {
                        _logger.Log($"Test event failed, circuit for event type {eventType} remains open");
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                // Unhandled exception, treat as a failure
                bool circuitOpened = circuitBreaker.RecordFailure();
                
                if (circuitOpened)
                {
                    _logger.Log($"Unhandled exception caused circuit for event type {eventType} to open: {ex.Message}");
                    
                    // Set the reset time
                    circuitBreaker.SetResetTime(_resetTimeout);
                }
                
                throw;
            }
            finally
            {
                // Release the test slot if we were in half-open state
                if (circuitBreaker.State == CircuitState.HalfOpen)
                {
                    circuitBreaker.ReleaseTestSlot();
                }
            }
        }

        /// <inheritdoc />
        public bool ShouldApply(IEvent @event)
        {
            // Apply to all events, but we'll maintain separate circuit breakers per event type
            return true;
        }

        private void CleanupExpiredCircuits(object state)
        {
            // Avoid running the cleanup multiple times concurrently
            if (!Monitor.TryEnter(_cleanupLock))
            {
                return;
            }

            try
            {
                foreach (var kvp in _circuitBreakers)
                {
                    var eventType = kvp.Key;
                    var circuit = kvp.Value;

                    // If circuit is open and reset time has passed, transition to half-open
                    if (circuit.State == CircuitState.Open && circuit.ShouldAttemptReset())
                    {
                        circuit.State = CircuitState.HalfOpen;
                        _logger.Log($"Circuit for event type {eventType} auto-transitioned to half-open state");
                    }
                }
            }
            finally
            {
                Monitor.Exit(_cleanupLock);
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _cleanupTimer.Dispose();
            _circuitBreakers.Clear();

            GC.SuppressFinalize(this);
        }

        private class CircuitBreakerState
        {
            private readonly int _failureThreshold;
            private int _testSlotInUse;
            private DateTime _resetTime;

            public CircuitBreakerState(int failureThreshold)
            {
                _failureThreshold = failureThreshold;
                State = CircuitState.Closed;
                FailureCount = 0;
                _resetTime = DateTime.MinValue;
            }

            public CircuitState State { get; set; }
            public int FailureCount { get; private set; }

            public bool RecordFailure()
            {
                FailureCount++;
                
                if (State == CircuitState.Closed && FailureCount >= _failureThreshold)
                {
                    State = CircuitState.Open;
                    return true;
                }
                
                if (State == CircuitState.HalfOpen)
                {
                    State = CircuitState.Open;
                    return true;
                }
                
                return false;
            }

            public void RecordSuccess()
            {
                if (FailureCount > 0)
                {
                    FailureCount--;
                }
            }

            public void Reset()
            {
                FailureCount = 0;
                State = CircuitState.Closed;
                _resetTime = DateTime.MinValue;
            }

            public void SetResetTime(TimeSpan timeout)
            {
                _resetTime = DateTime.UtcNow.Add(timeout);
            }

            public bool ShouldAttemptReset()
            {
                return State == CircuitState.Open && DateTime.UtcNow >= _resetTime;
            }

            public bool TryAcquireTestSlot()
            {
                return Interlocked.CompareExchange(ref _testSlotInUse, 1, 0) == 0;
            }

            public void ReleaseTestSlot()
            {
                Interlocked.Exchange(ref _testSlotInUse, 0);
            }
        }
    }

    /// <summary>
    /// Exception thrown when a circuit is open.
    /// </summary>
    public class CircuitBreakerOpenException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CircuitBreakerOpenException"/> class.
        /// </summary>
        /// <param name="message">The error message.</param>
        public CircuitBreakerOpenException(string message) : base(message)
        {
        }
    }
}