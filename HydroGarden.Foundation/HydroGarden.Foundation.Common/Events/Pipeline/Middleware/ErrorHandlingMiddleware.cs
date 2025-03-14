using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Logger.Abstractions;

namespace HydroGarden.Foundation.Common.Events.Pipeline.Middleware
{
    /// <summary>
    /// Middleware for handling errors that occur during event processing.
    /// </summary>
    public class ErrorHandlingMiddleware : IEventMiddleware
    {
        private readonly ILogger _logger;
        private readonly IErrorMonitor _errorMonitor;

        /// <inheritdoc/>
        public Guid Id { get; } = Guid.NewGuid();

        /// <inheritdoc/>
        public string Name => "Error Handling Middleware";

        /// <inheritdoc/>
        public int Order => 100; // High priority to catch errors early

        /// <summary>
        /// Initializes a new instance of the <see cref="ErrorHandlingMiddleware"/> class.
        /// </summary>
        /// <param name="logger">The logger to use.</param>
        /// <param name="errorMonitor">The error monitor to report errors to.</param>
        public ErrorHandlingMiddleware(ILogger logger, IErrorMonitor errorMonitor)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _errorMonitor = errorMonitor ?? throw new ArgumentNullException(nameof(errorMonitor));
        }

        /// <inheritdoc/>
        public bool ShouldApply(IEvent @event)
        {
            // Apply to all events
            return true;
        }

        /// <inheritdoc/>
        public async Task<IEventProcessingResult> ProcessAsync(
            object? sender,
            IEvent @event,
            Func<object?, IEvent, CancellationToken, Task<IEventProcessingResult>> next,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Call the next middleware in the pipeline
                var result = await next(sender, @event, cancellationToken);

                // Check if an error occurred
                if (!result.IsSuccess && result.Exception != null)
                {
                    // Report the error
                    await ReportErrorAsync(@event, result.Exception, cancellationToken);
                }

                return result;
            }
            catch (Exception ex)
            {
                // Log and report the error
                _logger.Log(ex, $"Unhandled exception in event pipeline for event {@event.EventId}");
                
                await ReportErrorAsync(@event, ex, cancellationToken);
                
                // Return a failure result
                return EventProcessingResult.Failure(@event, ex);
            }
        }

        private async Task ReportErrorAsync(IEvent @event, Exception ex, CancellationToken ct)
        {
            try
            {
                // Create error context
                var context = new Dictionary<string, object>
                {
                    ["EventId"] = @event.EventId.ToString(),
                    ["EventType"] = @event.EventType.ToString(),
                    ["SourceId"] = @event.SourceId.ToString(),
                    ["Timestamp"] = @event.Timestamp.ToString("o")
                };

                // Report the error
                await _errorMonitor.ReportExceptionAsync(
                    this,
                    ex,
                    "EVENT_PROCESSING_ERROR",
                    $"Error processing event {@event.EventId} of type {@event.EventType}",
                    ErrorSeverity.Error,
                    ErrorSource.Service,
                    context,
                    ct);
            }
            catch (Exception reportEx)
            {
                // Log but don't throw to avoid crashing the pipeline
                _logger.Log(reportEx, $"Error reporting event processing error for event {@event.EventId}");
            }
        }
    }
}