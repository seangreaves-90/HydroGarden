using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.Abstractions.Interfaces.Logging;
using System.Collections.Concurrent;

namespace HydroGarden.Foundation.Common.ErrorHandling
{
    public abstract class ErrorMonitorBase(ILogger logger, int maxErrorQueueSize = 1000) : IErrorMonitor
    {
        protected readonly ILogger Logger = logger;
        protected readonly ConcurrentQueue<IApplicationError> ErrorQueue = new();
        protected readonly int MaxErrorQueueSize = maxErrorQueueSize;

        public virtual Task ReportErrorAsync(IApplicationError error, CancellationToken ct = default)
        {
            Logger.Log(error.Exception, $"[{error.Severity}] {error.ErrorCode}: {error.Message}");

            ErrorQueue.Enqueue(error);
            while (ErrorQueue.Count > MaxErrorQueueSize && ErrorQueue.TryDequeue(out _)) { }

            return Task.CompletedTask;
        }

        public virtual Task<IReadOnlyList<IApplicationError>> GetRecentErrorsAsync(int count = 10, CancellationToken ct = default)
        {
            var result = ErrorQueue.TakeLast(Math.Min(count, ErrorQueue.Count)).ToList();
            return Task.FromResult<IReadOnlyList<IApplicationError>>(result);
        }

        public virtual Task<bool> HasActiveErrorsAsync(ErrorSeverity minimumSeverity = ErrorSeverity.Error, CancellationToken ct = default)
        {
            return Task.FromResult(ErrorQueue.Any(e => e.Severity >= minimumSeverity));
        }
    }
}
