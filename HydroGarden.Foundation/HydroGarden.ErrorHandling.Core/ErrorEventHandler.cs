using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Foundation.ErrorHandling.Events;
using HydroGarden.Logger.Abstractions;

namespace HydroGarden.Foundation.ErrorHandling
{
    /// <summary>
    /// Handles error events from the event bus.
    /// </summary>
    /// <param name="errorMonitor">The error monitor to report errors to.</param>
    /// <param name="logger">The logger.</param>
    public class ErrorEventHandler(IErrorMonitor errorMonitor, ILogger logger) : IEventHandler
    {
        private readonly IErrorMonitor _errorMonitor = errorMonitor ?? throw new ArgumentNullException(nameof(errorMonitor));
        private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        /// <inheritdoc/>
        public async Task HandleEventAsync<T>(object? sender, T evt, CancellationToken ct = default) where T : IEvent
        {
            if (evt is not IEvent @event)
                return;

            // Extract error data if this is an error event
            var errorData = @event.ExtractErrorData();
            if (errorData == null)
                return;

            _logger.Log($"Processing error event: {errorData.ErrorCode} - {errorData.Message}");

            // Create a component error from the error event
            var componentError = new  ComponentError(
                errorData.DeviceId,
                errorData.ErrorCode,
                errorData.Message,
                errorData.Severity,
                errorData.Source,
                errorData.Context,
                errorData.ExceptionDetails != null ? new Exception(errorData.ExceptionDetails) : null);

            // Report the error to the monitor
            await _errorMonitor.ReportErrorAsync(componentError, ct);
        }

        /// <inheritdoc/>
        public ValueTask DisposeAsync()
        {
            GC.SuppressFinalize(this);
            return ValueTask.CompletedTask;
        }
    }
}