using HydroGarden.Foundation.ErrorHandling.Exceptions;
using HydroGarden.Foundation.ErrorHandling.Interfaces;

namespace HydroGarden.Foundation.Tests.ErrorHandling.Mocks
{
    /// <summary>
    /// A testable implementation of CircuitBreakerMiddleware for use in tests.
    /// This implementation allows for better control in tests without relying on internal methods.
    /// </summary>
    public class TestableCircuitBreakerMiddleware : ICircuitBreakerMiddleware
    {
        public enum CircuitState
        {
            Closed = 0,
            HalfOpen = 1,
            Open = 2
        }

        private readonly Dictionary<string, CircuitState> _circuits = [];
        private readonly Dictionary<string, DateTimeOffset> _lastFailure = [];
        
        /// <summary>
        /// Gets the current state of a named circuit.
        /// </summary>
        public virtual object GetCircuitState(string serviceKey)
        {
            if (string.IsNullOrEmpty(serviceKey))
                return CircuitState.Closed;
                
            if (_circuits.TryGetValue(serviceKey, out var state))
                return state;
                
            return CircuitState.Closed; // Default state
        }
        
        /// <summary>
        /// Sets the circuit to a specific state.
        /// </summary>
        public virtual void SetCircuitState(string serviceKey, CircuitState state)
        {
            if (string.IsNullOrEmpty(serviceKey))
                return;
                
            _circuits[serviceKey] = state;
            
            if (state == CircuitState.Open && !_lastFailure.ContainsKey(serviceKey))
            {
                _lastFailure[serviceKey] = DateTimeOffset.UtcNow;
            }
        }
        
        /// <summary>
        /// Attempts to reset an open circuit.
        /// </summary>
        public virtual void ResetCircuit(string serviceKey)
        {
            if (string.IsNullOrEmpty(serviceKey))
                return;
                
            if (_circuits.TryGetValue(serviceKey, out var state))
            {
                if (state == CircuitState.Open)
                {
                    // Transition to half-open or closed
                    _circuits[serviceKey] = CircuitState.HalfOpen;
                }
                else if (state == CircuitState.HalfOpen)
                {
                    // Move to fully closed
                    _circuits[serviceKey] = CircuitState.Closed;
                }
            }
        }
        
        /// <summary>
        /// Opens a circuit due to a failure.
        /// </summary>
        public virtual void OpenCircuit(string serviceKey)
        {
            if (string.IsNullOrEmpty(serviceKey))
                return;
                
            _circuits[serviceKey] = CircuitState.Open;
            _lastFailure[serviceKey] = DateTimeOffset.UtcNow;
        }
        
        /// <summary>
        /// Gets the time of the last failure for a circuit.
        /// </summary>
        public virtual DateTimeOffset? GetLastFailureTime(string serviceKey)
        {
            if (string.IsNullOrEmpty(serviceKey) || !_lastFailure.TryGetValue(serviceKey, out var time))
                return null;
                
            return time;
        }
        
        /// <summary>
        /// Checks if a circuit allows operations or should be blocked.
        /// </summary>
        public virtual bool AllowOperation(string serviceKey)
        {
            var state = GetCircuitState(serviceKey);
            
            if (state == (object)CircuitState.Closed)
                return true;
                
            if (state == (object)CircuitState.HalfOpen)
                return true; // Allow one test operation
                
            // If open, throw an exception
            var lastFailure = GetLastFailureTime(serviceKey) ?? DateTimeOffset.UtcNow;
            throw new CircuitBreakerOpenException($"Circuit for service '{serviceKey}' is open");
        }
    }
}