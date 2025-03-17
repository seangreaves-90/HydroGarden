using System.Collections.Concurrent;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.ErrorHandling.Models;
using HydroGarden.Logger.Abstractions;

namespace HydroGarden.Foundation.ErrorHandling.Repositories
{
    /// <summary>
    /// An in-memory implementation of the error repository for testing and development.
    /// </summary>
    public class InMemoryErrorRepository : IErrorRepository
    {
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<Guid, ErrorRecord> _errors = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="InMemoryErrorRepository"/> class.
        /// </summary>
        /// <param name="logger">The logger to use.</param>
        public InMemoryErrorRepository(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _logger.Log("InMemoryErrorRepository initialized");
        }

        /// <inheritdoc/>
        public Task<Guid> SaveErrorAsync(IApplicationError error, CancellationToken ct = default)
        {

            ArgumentNullException.ThrowIfNull(error);

            // Create a record from the error
            var record = ErrorRecord.FromApplicationError(error);

            // Ensure it has an ID
            if (record.ErrorId == Guid.Empty)
            {
                record.ErrorId = Guid.NewGuid();
            }

            // Add or update in the dictionary
            _errors[record.ErrorId] = record;
            _logger.Log($"Error {record.ErrorId} saved to repository");

            return Task.FromResult(record.ErrorId);
        }

        /// <inheritdoc/>
        public Task<IApplicationError?> GetErrorByIdAsync(Guid errorId, CancellationToken ct = default)
        {
            _errors.TryGetValue(errorId, out var error);
            return Task.FromResult<IApplicationError?>(error);
        }

        /// <inheritdoc/>
        public Task<IReadOnlyCollection<IApplicationError>> GetErrorsByDeviceIdAsync(Guid deviceId, CancellationToken ct = default)
        {
            var result = _errors.Values
                .Where(e => e.DeviceId == deviceId)
                .OrderByDescending(e => e.Timestamp)
                .ToList<IApplicationError>();

            return Task.FromResult<IReadOnlyCollection<IApplicationError>>(result);
        }

        /// <inheritdoc/>
        public Task<IReadOnlyCollection<IApplicationError>> GetErrorsBySeverityAsync(ErrorSeverity minSeverity, CancellationToken ct = default)
        {
            var result = _errors.Values
                .Where(e => e.Severity >= minSeverity)
                .OrderByDescending(e => e.Timestamp)
                .ToList<IApplicationError>();

            return Task.FromResult<IReadOnlyCollection<IApplicationError>>(result);
        }

        /// <inheritdoc/>
        public Task<IReadOnlyCollection<IApplicationError>> GetErrorsByComponentIdAsync(Guid componentId, CancellationToken ct = default)
        {
            var result = _errors.Values
                .Where(e => e.ComponentId == componentId)
                .OrderByDescending(e => e.Timestamp)
                .ToList<IApplicationError>();

            return Task.FromResult<IReadOnlyCollection<IApplicationError>>(result);
        }

        /// <inheritdoc/>
        public Task<IReadOnlyCollection<IApplicationError>> GetErrorsByErrorCodeAsync(string errorCode, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(errorCode))
                throw new ArgumentNullException(nameof(errorCode));

            var result = _errors.Values
                .Where(e => e.ErrorCode == errorCode)
                .OrderByDescending(e => e.Timestamp)
                .ToList<IApplicationError>();

            return Task.FromResult<IReadOnlyCollection<IApplicationError>>(result);
        }

        /// <inheritdoc/>
        public Task<IReadOnlyCollection<IApplicationError>> GetUnresolvedErrorsAsync(CancellationToken ct = default)
        {
            var result = _errors.Values
                .Where(e => !e.IsResolved)
                .OrderByDescending(e => e.Timestamp)
                .ToList<IApplicationError>();

            return Task.FromResult<IReadOnlyCollection<IApplicationError>>(result);
        }

        /// <inheritdoc/>
        public Task<IReadOnlyCollection<IApplicationError>> GetUnacknowledgedErrorsAsync(CancellationToken ct = default)
        {
            var result = _errors.Values
                .Where(e => !e.IsAcknowledged)
                .OrderByDescending(e => e.Timestamp)
                .ToList<IApplicationError>();

            return Task.FromResult<IReadOnlyCollection<IApplicationError>>(result);
        }

        /// <inheritdoc/>
        public Task<bool> AcknowledgeErrorAsync(Guid errorId, string acknowledgedBy, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(acknowledgedBy))
                throw new ArgumentNullException(nameof(acknowledgedBy));

            if (_errors.TryGetValue(errorId, out var error))
            {
                error.IsAcknowledged = true;
                error.AcknowledgedTimestamp = DateTimeOffset.UtcNow;
                error.AcknowledgedBy = acknowledgedBy;
                _logger.Log($"Error {errorId} acknowledged by {acknowledgedBy}");
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }

        /// <inheritdoc/>
        public Task<bool> ResolveErrorAsync(Guid errorId, CancellationToken ct = default)
        {
            if (_errors.TryGetValue(errorId, out var error))
            {
                error.IsResolved = true;
                error.ResolvedTimestamp = DateTimeOffset.UtcNow;
                _logger.Log($"Error {errorId} resolved");
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }

        /// <inheritdoc/>
        public Task<bool> DeleteErrorAsync(Guid errorId, CancellationToken ct = default)
        {
            bool result = _errors.TryRemove(errorId, out _);
            if (result)
            {
                _logger.Log($"Error {errorId} deleted from repository");
            }
            return Task.FromResult(result);
        }
    }
}