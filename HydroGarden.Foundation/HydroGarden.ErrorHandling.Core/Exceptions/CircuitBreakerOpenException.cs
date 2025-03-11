using System;

namespace HydroGarden.Foundation.ErrorHandling.Exceptions
{
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
            LastFailureTime = DateTime.UtcNow;
        }
        
        /// <summary>
        /// Gets the time of the last failure that caused the circuit to open.
        /// </summary>
        public DateTime LastFailureTime { get; }
    }
}
