using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.Abstractions.Interfaces.Events;

namespace HydroGarden.Foundation.Common.ErrorHandling
{
    public class ComponentError(Guid deviceId, string? errorCode, string message,
        ErrorSeverity severity, IDictionary<string, object>? context = null,
        Exception? exception = null) : IApplicationError
    {
        public Guid DeviceId { get; } = deviceId;
        public string? ErrorCode { get; } = errorCode;
        public string Message { get; } = message;
        public ErrorSeverity Severity { get; } = severity;
        public IDictionary<string, object> Context { get; } = context ?? new Dictionary<string, object>();
        public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
        public Exception? Exception { get; } = exception;
    }

}
