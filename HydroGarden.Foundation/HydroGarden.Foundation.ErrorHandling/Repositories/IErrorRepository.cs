using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;

namespace HydroGarden.Foundation.ErrorHandling.Repositories
{
    /// <summary>
    /// Repository interface for error persistence operations.
    /// </summary>
    public interface IErrorRepository
    {
        /// <summary>
        /// Saves an error to the repository.
        /// </summary>
        /// <param name="error">The error to save.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The error ID.</returns>
        Task<Guid> SaveErrorAsync(IApplicationError error, CancellationToken ct = default);

        /// <summary>
        /// Gets an error by its ID.
        /// </summary>
        /// <param name="errorId">The error ID to retrieve.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The error if found, null otherwise.</returns>
        Task<IApplicationError?> GetErrorByIdAsync(Guid errorId, CancellationToken ct = default);

        /// <summary>
        /// Gets all errors for a specific device.
        /// </summary>
        /// <param name="deviceId">The device ID to retrieve errors for.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A collection of errors for the specified device.</returns>
        Task<IReadOnlyCollection<IApplicationError>> GetErrorsByDeviceIdAsync(Guid deviceId, CancellationToken ct = default);

        /// <summary>
        /// Gets all errors with at least the specified minimum severity.
        /// </summary>
        /// <param name="minSeverity">The minimum severity level.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A collection of errors with at least the specified severity.</returns>
        Task<IReadOnlyCollection<IApplicationError>> GetErrorsBySeverityAsync(ErrorSeverity minSeverity, CancellationToken ct = default);

        /// <summary>
        /// Gets all errors for a specific component.
        /// </summary>
        /// <param name="componentId">The component ID to retrieve errors for.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A collection of errors for the specified component.</returns>
        Task<IReadOnlyCollection<IApplicationError>> GetErrorsByComponentIdAsync(Guid componentId, CancellationToken ct = default);

        /// <summary>
        /// Gets all errors with the specified error code.
        /// </summary>
        /// <param name="errorCode">The error code to retrieve errors for.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A collection of errors with the specified error code.</returns>
        Task<IReadOnlyCollection<IApplicationError>> GetErrorsByErrorCodeAsync(string errorCode, CancellationToken ct = default);

        /// <summary>
        /// Gets all errors that have not been resolved.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A collection of unresolved errors.</returns>
        Task<IReadOnlyCollection<IApplicationError>> GetUnresolvedErrorsAsync(CancellationToken ct = default);

        /// <summary>
        /// Gets all errors that have not been acknowledged.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A collection of unacknowledged errors.</returns>
        Task<IReadOnlyCollection<IApplicationError>> GetUnacknowledgedErrorsAsync(CancellationToken ct = default);

        /// <summary>
        /// Acknowledges an error.
        /// </summary>
        /// <param name="errorId">The error ID to acknowledge.</param>
        /// <param name="acknowledgedBy">The user acknowledging the error.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>True if the error was acknowledged, false if not found.</returns>
        Task<bool> AcknowledgeErrorAsync(Guid errorId, string acknowledgedBy, CancellationToken ct = default);

        /// <summary>
        /// Resolves an error.
        /// </summary>
        /// <param name="errorId">The error ID to resolve.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>True if the error was resolved, false if not found.</returns>
        Task<bool> ResolveErrorAsync(Guid errorId, CancellationToken ct = default);

        /// <summary>
        /// Deletes an error.
        /// </summary>
        /// <param name="errorId">The error ID to delete.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>True if the error was deleted, false if not found.</returns>
        Task<bool> DeleteErrorAsync(Guid errorId, CancellationToken ct = default);
    }
}