using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.Abstractions.Interfaces.Logging;

namespace HydroGarden.Foundation.Tests.Integration
{
    public class TestErrorMonitor(ILogger logger) : IErrorMonitor
    {
        private readonly List<IApplicationError> _errors = new();

        public Task ReportErrorAsync(IApplicationError error, CancellationToken ct = default)
        {
            _errors.Add(error);
            logger.Log(error.Exception, $"[TEST] Error reported: {error.ErrorCode} - {error.Message}");
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<IApplicationError>> GetRecentErrorsAsync(int count = 10, CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<IApplicationError>>(_errors.TakeLast(count).ToList());
        }

        public Task<bool> HasActiveErrorsAsync(ErrorSeverity minimumSeverity = ErrorSeverity.Error, CancellationToken ct = default)
        {
            return Task.FromResult(_errors.Any(e => e.Severity >= minimumSeverity));
        }
    }
}