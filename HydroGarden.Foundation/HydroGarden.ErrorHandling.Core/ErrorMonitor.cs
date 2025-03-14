using HydroGarden.Foundation.Abstractions.Interfaces.Errors;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorEventTransformation;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.ErrorHandling.Core.Repositories;
using HydroGarden.Logger.Abstractions;
using System.Collections.Concurrent;

namespace HydroGarden.ErrorHandling.Core
{
    /// <summary>
    /// Provides error monitoring and tracking functionality.
    /// </summary>
    public class ErrorMonitor : IErrorMonitor
    {
        private readonly ILogger _logger;
        private readonly IErrorEventTransformationService _transformationService;
        private readonly IErrorRepository? _errorRepository;
        private readonly ConcurrentDictionary<string, IApplicationError> _activeErrors = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="ErrorMonitor"/> class.
        /// </summary>
        /// <param name="logger">The logger to use.</param>
        /// <param name="transformationService">The error event transformation service.</param>
        /// <param name="errorRepository">Optional error repository for persistence.</param>
        public ErrorMonitor(
            ILogger logger,
            IErrorEventTransformationService transformationService,
            IErrorRepository? errorRepository = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _transformationService = transformationService ?? throw new ArgumentNullException(nameof(transformationService));
            _errorRepository = errorRepository;
            
            if (_errorRepository != null)
            {
                _logger.Log("ErrorMonitor initialized with repository for error persistence");
            }
        }

        /// <inheritdoc/>
        public async Task ReportErrorAsync(IApplicationError error, CancellationToken ct = default)
        {
            if (error == null)
                throw new ArgumentNullException(nameof(error));

            // Store the error in active errors
            string errorKey = $"{error.DeviceId}:{error.ErrorCode}";
            _activeErrors[errorKey] = error;

            _logger.Log($"Error reported: {error}");
            
            // Persist the error if we have a repository
            if (_errorRepository != null)
            {
                try
                {
                    await _errorRepository.SaveErrorAsync(error, ct);
                    _logger.Log($"Error {error.ErrorId} persisted to repository");
                }
                catch (Exception ex)
                {
                    _logger.Log(ex, $"Failed to persist error {error.ErrorId} to repository");
                }
            }

            // Publish the error as an event
            try
            {
                await _transformationService.PublishErrorAsEventAsync(error, ct);
            }
            catch (Exception ex)
            {
                _logger.Log(ex, "Failed to publish error as event");
            }
        }

        /// <inheritdoc/>
        public async Task ReportExceptionAsync(
            object source,
            Exception exception,
            string errorCode,
            string message,
            ErrorSeverity severity = ErrorSeverity.Error,
            ErrorSource errorSource = ErrorSource.Unknown,
            IDictionary<string, object>? context = null,
            CancellationToken ct = default)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (exception == null)
                throw new ArgumentNullException(nameof(exception));

            // Determine device ID from source or context
            Guid deviceId = Guid.Empty;
            
            // Try to get device ID from context
            if (context != null && context.TryGetValue("DeviceId", out var deviceIdObj) && 
                deviceIdObj is string deviceIdStr && Guid.TryParse(deviceIdStr, out var parsedId))
            {
                deviceId = parsedId;
            }
            
            // Create error from exception
            var error = new ComponentError(
                deviceId,
                errorCode,
                message,
                severity,
                errorSource,
                context,
                exception);

            // Report the error
            await ReportErrorAsync(error, ct);
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyCollection<IApplicationError>> GetRecentErrorsAsync(
            int limit = 10, 
            CancellationToken ct = default)
        {
            // If we have a repository, use it to get recent errors
            if (_errorRepository != null)
            {
                try
                {
                    // Get unresolved errors from repository with higher precedence
                    var unresolvedErrors = await _errorRepository.GetUnresolvedErrorsAsync(ct);
                    return unresolvedErrors.OrderByDescending(e => e.Timestamp)
                        .Take(limit)
                        .ToList();
                }
                catch (Exception ex)
                {
                    _logger.Log(ex, "Failed to get recent errors from repository");
                }
            }
            
            // Fall back to in-memory errors
            var recentErrors = _activeErrors.Values
                .OrderByDescending(e => e.Timestamp)
                .Take(limit)
                .ToList();

            return recentErrors;
        }

        /// <inheritdoc/>
        public async Task<bool> HasActiveErrorsAsync(
            ErrorSeverity minSeverity = ErrorSeverity.Warning, 
            CancellationToken ct = default)
        {
            // If we have a repository, check it for active errors
            if (_errorRepository != null)
            {
                try
                {
                    var unresolvedErrors = await _errorRepository.GetUnresolvedErrorsAsync(ct);
                    return unresolvedErrors.Any(e => e.Severity >= minSeverity);
                }
                catch (Exception ex)
                {
                    _logger.Log(ex, "Failed to check for active errors in repository");
                }
            }
            
            // Fall back to in-memory errors
            bool hasErrors = _activeErrors.Values
                .Any(e => e.Severity >= minSeverity);

            return hasErrors;
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyCollection<IApplicationError>> GetActiveErrorsForDeviceAsync(
            Guid deviceId, 
            CancellationToken ct = default)
        {
            // If we have a repository, use it to get device errors
            if (_errorRepository != null)
            {
                try
                {
                    return await _errorRepository.GetErrorsByDeviceIdAsync(deviceId, ct);
                }
                catch (Exception ex)
                {
                    _logger.Log(ex, $"Failed to get active errors for device {deviceId} from repository");
                }
            }
            
            // Fall back to in-memory errors
            var deviceErrors = _activeErrors.Values
                .Where(e => e.DeviceId == deviceId)
                .ToList();

            return deviceErrors;
        }

        /// <inheritdoc/>
        public async Task ClearErrorAsync(
            Guid deviceId, 
            string errorCode, 
            CancellationToken ct = default)
        {
            string errorKey = $"{deviceId}:{errorCode}";
            
            // Remove from in-memory cache
            if (_activeErrors.TryRemove(errorKey, out var error))
            {
                _logger.Log($"Cleared error {errorCode} for device {deviceId} from memory");
                
                // Mark as resolved in repository if available
                if (_errorRepository != null && error != null)
                {
                    try
                    {
                        // Get all matching errors and resolve them
                        var matchingErrors = await _errorRepository.GetErrorsByDeviceIdAsync(deviceId, ct);
                        foreach (var matchingError in matchingErrors.Where(e => e.ErrorCode == errorCode))
                        {
                            await _errorRepository.ResolveErrorAsync(matchingError.ErrorId, ct);
                            _logger.Log($"Resolved error {matchingError.ErrorId} in repository");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Log(ex, $"Failed to resolve error {errorCode} for device {deviceId} in repository");
                    }
                }
            }
        }
    }
}
