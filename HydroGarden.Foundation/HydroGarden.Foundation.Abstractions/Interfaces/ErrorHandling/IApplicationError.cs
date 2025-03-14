namespace HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling
{
    /// <summary>
    /// Defines the severity level of an error.
    /// </summary>
    public enum ErrorSeverity
    {
        Warning,        // Operation can continue
        Error,          // Operation failed but component can recover
        Critical,       // Component needs external intervention
        Catastrophic    // System stability is at risk
    }

    /// <summary>
    /// Defines the source of an error.
    /// </summary>
    public enum ErrorSource
    {
        Device,        // Hardware/IoT device errors
        Service,       // Service/application logic errors
        Communication, // Network/communication errors
        UI,            // User interface errors
        Database,      // Data persistence errors
        Unknown        // Uncategorized errors
    }
    
    /// <summary>
    /// Categorizes errors for better grouping and analysis.
    /// </summary>
    public enum ErrorCategory
    {
        Unknown = 0,
        Device = 10,
        Service = 20,
        Communication = 30,
        EventSystem = 40,
        Storage = 50,
        Security = 60
    }
    
    /// <summary>
    /// Defines the contract for representing application errors.
    /// </summary>
    public interface IApplicationError
    {
        /// <summary>
        /// Gets the ID of the device associated with this error.
        /// </summary>
        public Guid DeviceId { get; }
        
        /// <summary>
        /// Gets the error code that identifies the type of error.
        /// </summary>
        public string? ErrorCode { get; }
        
        /// <summary>
        /// Gets the human-readable error message.
        /// </summary>
        public string Message { get; }
        
        /// <summary>
        /// Gets the severity level of the error.
        /// </summary>
        public ErrorSeverity Severity { get; }
        
        /// <summary>
        /// Gets additional contextual information about the error.
        /// </summary>
        public IDictionary<string, object> Context { get; }
        
        /// <summary>
        /// Gets the timestamp when the error occurred.
        /// </summary>
        public DateTimeOffset Timestamp { get; }
        
        /// <summary>
        /// Gets the exception associated with this error, if any.
        /// </summary>
        public Exception? Exception { get; }
        
        /// <summary>
        /// Gets the correlation ID for tracking related errors.
        /// </summary>
        public Guid CorrelationId { get; }
        
        /// <summary>
        /// Gets the source of the error.
        /// </summary>
        public ErrorSource Source { get; }
        
        /// <summary>
        /// Gets the category of the error.
        /// </summary>
        public ErrorCategory Category { get; }
    }
}
