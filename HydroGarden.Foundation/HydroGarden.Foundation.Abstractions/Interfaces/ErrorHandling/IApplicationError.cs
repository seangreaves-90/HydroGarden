namespace HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling
{
    public enum ErrorSeverity
    {
        Warning,        // Operation can continue
        Error,          // Operation failed but component can recover
        Critical,       // Component needs external intervention
        Catastrophic    // System stability is at risk
    }

    public enum ErrorSource
    {
        Device,        // Hardware/IoT device errors
        Service,       // Service/application logic errors
        Communication, // Network/communication errors
        UI,            // User interface errors
        Database,      // Data persistence errors
        Unknown        // Uncategorized errors
    }
    public interface IApplicationError
    {
        public Guid DeviceId { get; }
        public string? ErrorCode { get; }
        public string Message { get; }
        public ErrorSeverity Severity { get; }
        public IDictionary<string, object> Context { get; }
        public DateTimeOffset Timestamp { get; }
        public Exception? Exception { get; }
        public Guid CorrelationId { get; }
        public ErrorSource Source { get; }
        public bool IsTransient { get; }
        public bool IsRecoverable { get; }
        public int RecoveryAttemptCount { get; }
        public DateTimeOffset? LastRecoveryAttempt { get; }
        public int MaxRecoveryAttempts { get; }

        /// <summary>
        /// Records the recovery attempt for this error
        /// </summary>
        void RecordRecoveryAttempt();

        /// <summary>
        /// Determines if recovery can be attempted based on backoff period and max attempts
        /// </summary>
        /// <param name="backoffPeriod">Minimum time between recovery attempts</param>
        /// <param name="maxAttempts">Override for maximum attempts, defaults to error's MaxRecoveryAttempts</param>
        /// <returns>True if recovery can be attempted now</returns>
        bool CanAttemptRecoveryNow(TimeSpan backoffPeriod, int? maxAttempts = null);
    }
}
