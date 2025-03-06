using HydroGarden.Foundation.Abstractions.Interfaces;

namespace HydroGarden.Foundation.Common.Events
{
    /// <summary>
    /// Event arguments for circuit breaker state changes.
    /// </summary>
    public class CircuitStateChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Name of the service protected by the circuit breaker.
        /// </summary>
        public string ServiceName { get; }

        /// <summary>
        /// Previous state of the circuit breaker.
        /// </summary>
        public CircuitState OldState { get; }

        /// <summary>
        /// New state of the circuit breaker.
        /// </summary>
        public CircuitState NewState { get; }

        /// <summary>
        /// Time of the last failure that influenced the state change.
        /// </summary>
        public DateTimeOffset LastFailureTime { get; }

        /// <summary>
        /// Reason for the state change.
        /// </summary>
        public string Reason { get; }

        /// <summary>
        /// Creates a new circuit state changed event args.
        /// </summary>
        public CircuitStateChangedEventArgs(
            string serviceName,
            CircuitState oldState,
            CircuitState newState,
            DateTimeOffset lastFailureTime,
            string reason = "")
        {
            ServiceName = serviceName;
            OldState = oldState;
            NewState = newState;
            LastFailureTime = lastFailureTime;
            Reason = reason;
        }
    }
}