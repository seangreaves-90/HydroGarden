using HydroGarden.Foundation.Abstractions.Interfaces.ErrorEventTransformation;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Logger.Abstractions;
using System.Collections.Concurrent;

namespace HydroGarden.Foundation.ErrorHandling
{
    /// <summary>
    /// Provides error monitoring and tracking functionality.
    /// </summary>
    public class ErrorMonitor : IErrorMonitor
    {
        private readonly ILogger _logger;
        private readonly IErrorEventTransformationService _transformationService;
        private readonly ConcurrentDictionary<string, IApplicationError> _activeErrors = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="ErrorMonitor"/> class.
        /// </summary>
        /// <param name="logger">The logger to use.</param>
        /// <param name="transformationService">The error event transformation service.</param>
        public ErrorMonitor(
            ILogger logger,
            IErrorEventTransformationService transformationService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _transformationService = transformationService ?? throw new ArgumentNullException(nameof(transformationService));
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
        public Task<IReadOnlyCollection<IApplicationError>> GetRecentErrorsAsync(
            int limit = 10, 
            CancellationToken ct = default)
        {
            var recentErrors = _activeErrors.Values
                .OrderByDescending(e => e.Timestamp)
                .Take(limit)
                .ToList();

            return Task.FromResult<IReadOnlyCollection<IApplicationError>>(recentErrors);
        }

        /// <inheritdoc/>
        public Task<bool> HasActiveErrorsAsync(
            ErrorSeverity minSeverity = ErrorSeverity.Warning, 
            CancellationToken ct = default)
        {
            bool hasErrors = _activeErrors.Values
                .Any(e => e.Severity >= minSeverity);

            return Task.FromResult(hasErrors);
        }

        /// <inheritdoc/>
        public Task<IReadOnlyCollection<IApplicationError>> GetActiveErrorsForDeviceAsync(
            Guid deviceId, 
            CancellationToken ct = default)
        {
            var deviceErrors = _activeErrors.Values
                .Where(e => e.DeviceId == deviceId)
                .ToList();

            return Task.FromResult<IReadOnlyCollection<IApplicationError>>(deviceErrors);
        }

        /// <inheritdoc/>
        public Task ClearErrorAsync(
            Guid deviceId, 
            string errorCode, 
            CancellationToken ct = default)
        {
            string errorKey = $"{deviceId}:{errorCode}";
            _activeErrors.TryRemove(errorKey, out _);

            _logger.Log($"Cleared error {errorCode} for device {deviceId}");
            
            return Task.CompletedTask;
        }
    }
}
