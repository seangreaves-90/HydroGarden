

namespace HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling
{
    public enum ErrorSeverity
    {
        Warning,        // Operation can continue
        Error,          // Operation failed but component can recover
        Critical,       // Component needs external intervention
        Catastrophic    // System stability is at risk
    }
    public interface IComponentError
    {
        public Guid DeviceId { get; }
        public string ErrorCode { get; }
        public string Message { get; }
        public ErrorSeverity Severity { get; }
        public IDictionary<string, object> Context { get; }
        public DateTimeOffset Timestamp { get; }
        public Exception? Exception { get; }
    }
}
