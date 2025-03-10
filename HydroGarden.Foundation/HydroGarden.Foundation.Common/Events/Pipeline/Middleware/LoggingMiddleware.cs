using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Logger.Abstractions;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace HydroGarden.Foundation.Common.Events.Pipeline.Middleware
{
    /// <summary>
    /// Middleware that logs event processing.
    /// </summary>
    public class LoggingMiddleware : IEventMiddleware
    {
        private readonly ILogger _logger;
        private readonly LoggingLevel _loggingLevel;

        /// <summary>
        /// Logging levels for the middleware.
        /// </summary>
        public enum LoggingLevel
        {
            /// <summary>
            /// Log basic information.
            /// </summary>
            Basic,

            /// <summary>
            /// Log detailed information including timing.
            /// </summary>
            Detailed,

            /// <summary>
            /// Log diagnostic information including event details.
            /// </summary>
            Diagnostic
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LoggingMiddleware"/> class.
        /// </summary>
        /// <param name="logger">The logger to use.</param>
        /// <param name="loggingLevel">The logging level.</param>
        public LoggingMiddleware(
            ILogger logger,
            LoggingLevel loggingLevel = LoggingLevel.Detailed)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _loggingLevel = loggingLevel;
            Id = Guid.NewGuid();
            Name = "Logging Middleware";
            Order = 0; // Run first in the pipeline
        }

        /// <inheritdoc />
        public Guid Id { get; }

        /// <inheritdoc />
        public string Name { get; }

        /// <inheritdoc />
        public int Order { get; }

        /// <inheritdoc />
        public async Task<IEventProcessingResult> ProcessAsync(
            object sender,
            IEvent @event,
            Func<object, IEvent, CancellationToken, Task<IEventProcessingResult>> next,
            CancellationToken cancellationToken = default)
        {
            if (_loggingLevel == LoggingLevel.Basic)
            {
                _logger.Log($"Processing event {@event.EventId} of type {@event.EventType}");
                
                var result = await next(sender, @event, cancellationToken);
                
                _logger.Log($"Processed event {@event.EventId} with result {(result.IsSuccess ? "success" : "failure")}");
                
                return result;
            }
            else
            {
                var stopwatch = Stopwatch.StartNew();
                string senderType = sender?.GetType().Name ?? "Unknown";
                
                if (_loggingLevel == LoggingLevel.Diagnostic)
                {
                    _logger.Log($"Processing event {@event.EventId} of type {@event.EventType} from sender {senderType} with source ID {@event.SourceId}");
                }
                else
                {
                    _logger.Log($"Processing event {@event.EventId} of type {@event.EventType} from sender {senderType}");
                }
                
                try
                {
                    var result = await next(sender, @event, cancellationToken);
                    
                    stopwatch.Stop();
                    
                    if (_loggingLevel == LoggingLevel.Diagnostic)
                    {
                        if (result.IsSuccess)
                        {
                            _logger.Log($"Successfully processed event {@event.EventId} in {stopwatch.ElapsedMilliseconds}ms");
                        }
                        else
                        {
                            _logger.Log(result.Exception, $"Failed to process event {@event.EventId} after {stopwatch.ElapsedMilliseconds}ms");
                            
                            if (result.ShouldRetry)
                            {
                                _logger.Log($"Event {@event.EventId} will be retried (attempt {result.RetryCount}) after {result.RetryDelay.TotalMilliseconds}ms");
                            }
                        }
                    }
                    else
                    {
                        _logger.Log($"Processed event {@event.EventId} in {stopwatch.ElapsedMilliseconds}ms with result {(result.IsSuccess ? "success" : "failure")}");
                    }
                    
                    return result;
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    _logger.Log(ex, $"Unhandled exception processing event {@event.EventId} after {stopwatch.ElapsedMilliseconds}ms");
                    throw;
                }
            }
        }

        /// <inheritdoc />
        public bool ShouldApply(IEvent @event)
        {
            // Apply to all events
            return true;
        }
    }
}