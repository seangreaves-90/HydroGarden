using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Logger.Abstractions;
using System.Collections.Concurrent;

namespace HydroGarden.Foundation.Common.Events.Pipeline
{
    /// <summary>
    /// Implementation of the event processing pipeline that manages middleware components 
    /// and orchestrates event processing through the pipeline.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="EventProcessingPipeline"/> class.
    /// </remarks>
    /// <param name="logger">The logger to use.</param>
    public class EventProcessingPipeline(ILogger logger) : IEventProcessingPipeline, IDisposable
    {
        private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private readonly List<MiddlewareEntry> _middleware = [];
        private readonly ConcurrentDictionary<Guid, IEvent> _inProcessEvents = new();
        private readonly object _middlewareLock = new();
        private bool _disposed;

        /// <inheritdoc />
        public void AddMiddleware(IEventMiddleware middleware)
        {
            ArgumentNullException.ThrowIfNull(middleware);

            lock (_middlewareLock)
            {
                _middleware.Add(new MiddlewareEntry(middleware, null));
                _middleware.Sort((a, b) => a.Middleware.Order.CompareTo(b.Middleware.Order));
            }

            _logger.Log($"Added middleware '{middleware.Name}' to pipeline with ID {middleware.Id}");
        }

        /// <inheritdoc />
        public void AddMiddleware(IEventMiddleware middleware, params EventType[]? eventTypes)
        {
            ArgumentNullException.ThrowIfNull(middleware);

            if (eventTypes == null || eventTypes.Length == 0)
            {
                AddMiddleware(middleware);
                return;
            }

            lock (_middlewareLock)
            {
                _middleware.Add(new MiddlewareEntry(middleware, eventTypes));
                _middleware.Sort((a, b) => a.Middleware.Order.CompareTo(b.Middleware.Order));
            }

            _logger.Log($"Added middleware '{middleware.Name}' to pipeline with ID {middleware.Id} for specific event types");
        }

        /// <inheritdoc />
        public bool RemoveMiddleware(Guid middlewareId)
        {
            lock (_middlewareLock)
            {
                var entry = _middleware.FirstOrDefault(m => m.Middleware.Id == middlewareId);
                if (entry != null)
                {
                    _middleware.Remove(entry);
                    _logger.Log($"Removed middleware with ID {middlewareId} from pipeline");
                    return true;
                }
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<IEventProcessingResult> ProcessEventAsync(object? sender, IEvent @event, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(sender);

            ArgumentNullException.ThrowIfNull(@event);

            ObjectDisposedException.ThrowIf(_disposed, nameof(EventProcessingPipeline));

            // Add event to in-process tracking
            _inProcessEvents.TryAdd(@event.EventId, @event);

            try
            {
                _logger.Log($"Processing event {@event.EventId} of type {@event.EventType} through pipeline");

                // Copy middleware to avoid issues if it changes during processing
                List<MiddlewareEntry> middlewareList;
                lock (_middlewareLock)
                {
                    middlewareList = _middleware;
                }

                // Create the middleware pipeline
                var pipeline = CreatePipeline(middlewareList);

                // Start the pipeline
                return await pipeline(sender, @event, cancellationToken);
            }
            catch (Exception? ex)
            {
                _logger.Log(ex, $"Unhandled exception in event processing pipeline for event {@event.EventId}");
                return EventProcessingResult.Failure(@event, ex);
            }
            finally
            {
                // Remove event from in-process tracking
                _inProcessEvents.TryRemove(@event.EventId, out _);
            }
        }

        private Func<object, IEvent, CancellationToken, Task<IEventProcessingResult>> CreatePipeline(List<MiddlewareEntry> middlewareList)
        {
            // Build the pipeline from the end to the beginning
            Func<object, IEvent, CancellationToken, Task<IEventProcessingResult>> pipeline = (_, @event, _) => 
                Task.FromResult(EventProcessingResult.Success(@event));

            // Add each middleware, starting from the last one
            for (int i = middlewareList.Count - 1; i >= 0; i--)
            {
                var currentEntry = middlewareList[i];
                var currentPipeline = pipeline; // Capture the current pipeline

                pipeline = async (sender, @event, ct) =>
                {
                    // Check if this middleware applies to this event
                    bool shouldApply = ShouldApplyMiddleware(currentEntry, @event);
                    if (!shouldApply)
                    {
                        // Skip this middleware
                        return await currentPipeline(sender, @event, ct);
                    }

                    try
                    {
                        // Apply the middleware
                        return await currentEntry.Middleware.ProcessAsync(sender, @event, currentPipeline, ct);
                    }
                    catch (Exception? ex)
                    {
                        _logger.Log(ex, $"Exception in middleware '{currentEntry.Middleware.Name}' for event {@event.EventId}");
                        return EventProcessingResult.Failure(@event, ex);
                    }
                };
            }

            return pipeline;
        }

        private static bool ShouldApplyMiddleware(MiddlewareEntry entry, IEvent @event)
        {
            // If the middleware itself says it shouldn't apply, respect that
            if (!entry.Middleware.ShouldApply(@event))
            {
                return false;
            }

            // If no event types are specified, apply to all
            if (entry.EventTypes == null || entry.EventTypes.Length == 0)
            {
                return true;
            }

            // Otherwise, check if the event type is in the list
            return entry.EventTypes.Contains(@event.EventType);
        }

        /// <summary>
        /// Disposes the pipeline resources.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            // Clear middleware
            lock (_middlewareLock)
            {
                _middleware.Clear();
            }

            // Clear in-process events
            _inProcessEvents.Clear();

            GC.SuppressFinalize(this);
        }

        private class MiddlewareEntry(IEventMiddleware middleware, EventType[]? eventTypes)
        {
            public IEventMiddleware Middleware { get; } = middleware;
            public EventType[]? EventTypes { get; } = eventTypes;
        }
    }
}