namespace HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling
{
    public interface IErrorMonitor
    {
        Task ReportErrorAsync(IApplicationError error, CancellationToken ct = default);
        Task<IReadOnlyList<IApplicationError>> GetRecentErrorsAsync(int count = 10, CancellationToken ct = default);
        Task<bool> HasActiveErrorsAsync(ErrorSeverity minimumSeverity = ErrorSeverity.Error, CancellationToken ct = default);
    }
}
