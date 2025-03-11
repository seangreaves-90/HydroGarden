using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;

namespace HydroGarden.Foundation.Tests.ErrorHandling.Mocks
{
    /// <summary>
    /// A mock implementation of IErrorMonitor for testing purposes.
    /// </summary>
    public class MockErrorMonitor : IErrorMonitor
    {
        public List<IApplicationError> ReportedErrors { get; } = new();
        public Dictionary<Guid, List<IApplicationError>> DeviceErrors { get; } = new();
        public Dictionary<string, int> ErrorStats { get; } = new();
        public Dictionary<string, bool> RecoveryAttempts { get; } = new();
        public Dictionary<Guid, Func<IApplicationError, Task>> Subscriptions { get; } = new();

        public Task ReportErrorAsync(IApplicationError error, CancellationToken ct = default)
        {
            ReportedErrors.Add(error);

            if (!DeviceErrors.TryGetValue(error.DeviceId, out var errors))
            {
                errors = new List<IApplicationError>();
                DeviceErrors[error.DeviceId] = errors;
            }

            errors.Add(error);

            if (error.ErrorCode != null)
            {
                if (!ErrorStats.TryGetValue(error.ErrorCode, out var count))
                {
                    ErrorStats[error.ErrorCode] = 1;
                }
                else
                {
                    ErrorStats[error.ErrorCode] = count + 1;
                }
            }

            // Notify subscribers
            if (Subscriptions.Count > 0)
            {
                foreach (var subscription in Subscriptions.Values)
                {
                    subscription(error);
                }
            }

            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<IApplicationError>> GetRecentErrorsAsync(int count = 10, CancellationToken ct = default)
        {
            var result = ReportedErrors.TakeLast(Math.Min(count, ReportedErrors.Count)).ToList();
            return Task.FromResult<IReadOnlyList<IApplicationError>>(result);
        }

        public Task<bool> HasActiveErrorsAsync(ErrorSeverity minimumSeverity = ErrorSeverity.Error, CancellationToken ct = default)
        {
            return Task.FromResult(ReportedErrors.Any(e => e.Severity >= minimumSeverity));
        }

        public Task<IReadOnlyList<IApplicationError?>> GetActiveErrorsForDeviceAsync(Guid deviceId, CancellationToken ct = default)
        {
            if (DeviceErrors.TryGetValue(deviceId, out var errors))
            {
                return Task.FromResult<IReadOnlyList<IApplicationError?>>(errors.Cast<IApplicationError?>().ToList());
            }

            return Task.FromResult<IReadOnlyList<IApplicationError?>>(new List<IApplicationError?>());
        }

        public Task MarkErrorHandledAsync(Guid deviceId, string errorCode, CancellationToken ct = default)
        {
            if (DeviceErrors.TryGetValue(deviceId, out var errors))
            {
                errors.RemoveAll(e => e.ErrorCode == errorCode);
            }

            return Task.CompletedTask;
        }

        public Task<IDictionary<string, int>> GetErrorStatisticsAsync(DateTimeOffset since, CancellationToken ct = default)
        {
            return Task.FromResult<IDictionary<string, int>>(new Dictionary<string, int>(ErrorStats));
        }

        public Task RegisterRecoveryAttemptAsync(Guid deviceId, string errorCode, bool successful, CancellationToken ct = default)
        {
            var key = $"{deviceId}:{errorCode}";
            RecoveryAttempts[key] = successful;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<IApplicationError>> GetErrorsByCorrelationIdAsync(Guid correlationId, CancellationToken ct = default)
        {
            var result = ReportedErrors.Where(e => e.CorrelationId == correlationId).ToList();
            return Task.FromResult<IReadOnlyList<IApplicationError>>(result);
        }

        public Task<IReadOnlyList<IApplicationError>> GetErrorsByCodeAsync(string errorCode, DateTimeOffset since, CancellationToken ct = default)
        {
            var result = ReportedErrors.Where(e => e.ErrorCode == errorCode && e.Timestamp >= since).ToList();
            return Task.FromResult<IReadOnlyList<IApplicationError>>(result);
        }

        public Task<Guid> SubscribeToErrorsAsync(Func<IApplicationError, Task> handler, Func<IApplicationError, bool>? filter = null, CancellationToken ct = default)
        {
            var subscriptionId = Guid.NewGuid();
            Subscriptions[subscriptionId] = handler;
            return Task.FromResult(subscriptionId);
        }

        public Task UnsubscribeFromErrorsAsync(Guid subscriptionId, CancellationToken ct = default)
        {
            Subscriptions.Remove(subscriptionId);
            return Task.CompletedTask;
        }
    }
}
