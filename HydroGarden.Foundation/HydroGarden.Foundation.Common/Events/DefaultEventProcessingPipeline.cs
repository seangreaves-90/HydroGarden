using System.Collections.Concurrent;
using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Logger.Abstractions;

namespace HydroGarden.Foundation.Common.Events
{
    /// <summary>
    /// Default implementation of the event processing pipeline.
    /// </summary>
    public class DefaultEventProcessingPipeline : IEventProcessingPipeline, IDisposable
    {
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<Guid, MiddlewareRegistration> _middleware = new();
        private bool _isDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultEventProcessingPipeline"/> class.
        /// </summary>
        /// <param name="logger">The logger to use.</param>
        public DefaultEventProcessingPipeline(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public void AddMiddleware(IEventMiddleware middleware)
        {
            if (middleware == null)
                throw new ArgumentNullException(nameof(middleware));

            var registration = new MiddlewareRegistration(middleware, null);
            _middleware[middleware.Id] = registration;
        }

        /// <inheritdoc/>
        public void AddMiddleware(IEventMiddleware middleware, params EventType[]? eventTypes)
        {
            if (middleware == null)
                throw new ArgumentNullException(nameof(middleware));

            var registration = new MiddlewareRegistration(middleware, eventTypes);
            _middleware[middleware.Id] = registration;
        }

        /// <inheritdoc/>
        public bool RemoveMiddleware(Guid middlewareId)
        {
            return _middleware.TryRemove(middlewareId, out _);
        }

        /// <inheritdoc/>
        public async Task<IEventProcessingResult> ProcessEventAsync(object? sender, IEvent @event, CancellationToken cancellationToken = default)
        {
            if (@event == null)
                throw new ArgumentNullException(nameof(@event));

            try
            {
                // Create a mutable copy of the event for processing
                IEvent processedEvent = @event;

                // Get middleware for this event type, sorted by priority
                var applicableMiddleware = _middleware.Values
                    .Where(m => m.EventTypes == null || m.EventTypes.Length == 0 || m.EventTypes.Contains(@event.EventType))
                    .OrderByDescending(m => m.Middleware.Priority)
                    .ToList();

                if (applicableMiddleware.Count == 0)
                {
                    _logger.Log($"No applicable middleware for event {processedEvent.EventId}");
                    return new EventProcessingResult(processedEvent, processedEvent, true, null);
                }

                // Process event through each middleware in order
                foreach (var middleware in applicableMiddleware)
                {
                    try
                    {
                        var middlewareResult = await middleware.Middleware.ProcessEventAsync(sender, processedEvent, cancellationToken);
                        
                        // If middleware indicates processing should stop, return its result
                        if (middlewareResult.ShouldStopProcessing)
                        {
                            _logger.Log($"Middleware {middleware.Middleware.Id} stopped processing for event {processedEvent.EventId}");
                            return new EventProcessingResult(
                                @event, 
                                middlewareResult.Event, 
                                middlewareResult.Success, 
                                middlewareResult.Exception);
                        }

                        // Update the event for the next middleware
                        processedEvent = middlewareResult.Event;
                    }
                    catch (Exception ex)
                    {
                        _logger.Log(ex, $"Error in middleware {middleware.Middleware.Id} for event {processedEvent.EventId}");
                        return new EventProcessingResult(@event, processedEvent, false, ex);
                    }
                }

                // All middleware processed successfully
                return new EventProcessingResult(@event, processedEvent, true, null);
            }
            catch (Exception ex)
            {
                _logger.Log(ex, $"Error in event processing pipeline for event {@event.EventId}");
                return new EventProcessingResult(@event, @event, false, ex);
            }
        }

        /// <summary>
        /// Disposes the pipeline resources.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            _middleware.Clear();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Represents a middleware registration.
        /// </summary>
        private class MiddlewareRegistration
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="MiddlewareRegistration"/> class.
            /// </summary>
            /// <param name="middleware">The middleware instance.</param>
            /// <param name="eventTypes">The event types this middleware handles.</param>
            public MiddlewareRegistration(IEventMiddleware middleware, EventType[]? eventTypes)
            {
                Middleware = middleware;
                EventTypes = eventTypes;
            }

            /// <summary>
            /// Gets the middleware instance.
            /// </summary>
            public IEventMiddleware Middleware { get; }

            /// <summary>
            /// Gets the event types this middleware handles.
            /// </summary>
            public EventType[]? EventTypes { get; }
        }
    }

    /// <summary>
    /// Represents the result of event processing.
    /// </summary>
    public class EventProcessingResult : IEventProcessingResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EventProcessingResult"/> class.
        /// </summary>
        /// <param name="originalEvent">The original event.</param>
        /// <param name="processedEvent">The processed event.</param>
        /// <param name="isSuccess">Whether processing was successful.</param>
        /// <param name="exception">Any exception that occurred.</param>
        public EventProcessingResult(
            IEvent originalEvent,
            IEvent processedEvent,
            bool isSuccess,
            Exception? exception)
        {
            Event = originalEvent;
            ProcessedEvent = processedEvent;
            IsSuccess = isSuccess;
            Exception = exception;
        }

        /// <inheritdoc/>
        public IEvent Event { get; }

        /// <inheritdoc/>
        public IEvent ProcessedEvent { get; }

        /// <inheritdoc/>
        public bool IsSuccess { get; }

        /// <inheritdoc/>
        public Exception? Exception { get; }

        /// <inheritdoc/>
        [Obsolete("Retry functionality is deprecated and will be removed in a future version.")]
        public bool ShouldRetry => false;

        /// <inheritdoc/>
        [Obsolete("Retry functionality is deprecated and will be removed in a future version.")]
        public int RetryCount => 0;

        /// <inheritdoc/>
        [Obsolete("Retry functionality is deprecated and will be removed in a future version.")]
        public TimeSpan RetryDelay => TimeSpan.Zero;
    }
}