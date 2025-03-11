using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;

namespace HydroGarden.Foundation.Common.Events
{
    /// <summary>
    /// Event arguments for circuit breaker state changes.
    /// </summary>
    /// <remarks>
    /// Creates a new circuit state changed event args.
    /// </remarks>
    public class CircuitStateChangedEventArgs(
        string serviceName,
        CircuitState oldState,
        CircuitState newState,
        DateTimeOffset lastFailureTime,
        string reason = "") : EventArgs
    {
        /// <summary>
        /// Name of the service protected by the circuit breaker.
        /// </summary>
        public string ServiceName { get; } = serviceName;

        /// <summary>
        /// Previous state of the circuit breaker.
        /// </summary>
        public CircuitState OldState { get; } = oldState;

        /// <summary>
        /// New state of the circuit breaker.
        /// </summary>
        public CircuitState NewState { get; } = newState;

        /// <summary>
        /// Time of the last failure that influenced the state change.
        /// </summary>
        public DateTimeOffset LastFailureTime { get; } = lastFailureTime;

        /// <summary>
        /// Reason for the state change.
        /// </summary>
        public string Reason { get; } = reason;
    }
}