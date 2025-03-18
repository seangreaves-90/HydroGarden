using System.Collections.Concurrent;
using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Logger.Abstractions;

namespace HydroGarden.Foundation.Common.Events
{
    /// <summary>
    /// Default implementation of the event processing pipeline.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="DefaultEventProcessingPipeline"/> class.
    /// </remarks>
    /// <param name="logger">The logger to use.</param>
    public class DefaultEventProcessingPipeline(ILogger logger) : IEventProcessingPipeline, IDisposable
    {
        private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private readonly ConcurrentDictionary<Guid, MiddlewareRegistration> _middleware = new();
        private bool _isDisposed;

        /// <inheritdoc/>
        public Task AddMiddleware(IEventMiddleware middleware)
        {
            ArgumentNullException.ThrowIfNull(middleware);

            var registration = new MiddlewareRegistration(middleware, null);
            _middleware[middleware.Id] = registration;
            return Task.FromResult(Task.CompletedTask);
        }

        /// <inheritdoc/>
        public Task AddMiddleware(IEventMiddleware middleware, params EventType[]? eventTypes)
        {
            ArgumentNullException.ThrowIfNull(middleware);

            var registration = new MiddlewareRegistration(middleware, eventTypes);
            _middleware[middleware.Id] = registration;
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task<bool> RemoveMiddleware(Guid middlewareId)
        {
            return Task.FromResult(_middleware.TryRemove(middlewareId, out _));
        }

        /// <inheritdoc/>
        public async Task<IEventProcessingResult> ProcessEventAsync(object? sender, IEvent @event, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(@event);

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
                    return new EventProcessingResult(@event, processedEvent, true, null);
                }

                // Process event through each middleware in order
                foreach (var middleware in applicableMiddleware)
                {
                    try
                    {
                        _logger.Log($"Processing event {processedEvent.EventId} with middleware {middleware.Middleware.GetType().Name}");
                        var middlewareResult = await middleware.Middleware.ProcessEventAsync(sender, processedEvent, cancellationToken);
                        
                        // If middleware indicates processing should stop, return its result
                        if (middlewareResult.ShouldStopProcessing)
                        {
                            _logger.Log($"Middleware {middleware.Middleware.GetType().Name} stopped processing for event {processedEvent.EventId}");
                            return new EventProcessingResult(
                                @event, 
                                middlewareResult.Event, 
                                middlewareResult.Success, 
                                middlewareResult.Exception);
                        }

                        // Update the event for the next middleware
                        if (!ReferenceEquals(processedEvent, middlewareResult.Event))
                        {
                            _logger.Log($"Event {processedEvent.EventId} was transformed by {middleware.Middleware.GetType().Name}");
                            // Event was changed - use the new event for further processing
                            processedEvent = middlewareResult.Event;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Log(ex, $"Error in middleware {middleware.Middleware.GetType().Name} for event {processedEvent.EventId}");
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
        private class MiddlewareRegistration(IEventMiddleware middleware, EventType[]? eventTypes)
        {
        /// <summary>
        /// Gets the middleware instance.
        /// </summary>
        public IEventMiddleware Middleware { get; } = middleware;

        /// <summary>
        /// Gets the event types this middleware handles.
        /// </summary>
        public EventType[]? EventTypes { get; } = eventTypes;
        }
    }

    /// <summary>
    /// Represents the result of event processing.
    /// </summary>
    public class EventProcessingResult(IEvent originalEvent, IEvent processedEvent, bool isSuccess, Exception? exception) : IEventProcessingResult
    {

        /// <inheritdoc/>
        public IEvent Event { get; } = originalEvent;

        /// <inheritdoc/>
        public IEvent ProcessedEvent { get; } = processedEvent;

        /// <inheritdoc/>
        public bool IsSuccess { get; } = isSuccess;

        /// <inheritdoc/>
        public Exception? Exception { get; } = exception;

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