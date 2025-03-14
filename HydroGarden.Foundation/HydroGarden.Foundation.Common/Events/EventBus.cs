using HydroGarden.Foundation.Abstractions.Interfaces;
using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Foundation.Abstractions.Interfaces.Services;
using HydroGarden.Foundation.Common.Events.Pipeline;
using HydroGarden.Logger.Abstractions;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace HydroGarden.Foundation.Common.Events
{
    /// <summary>
    /// A clean, focused implementation of the event bus for pub/sub functionality.
    /// </summary>
    public class EventBus : IEventBus, IDisposable
    {
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<Guid, EventSubscription> _subscriptions = new();
        private readonly ConcurrentDictionary<EventType, List<EventSubscription>> _subscriptionsByType = new();
        private readonly ITopologyService? _topologyService;
        private readonly IEventStore? _eventStore;
        private readonly IEventRetryPolicy? _retryPolicy;
        private readonly IEventTransformer? _transformer;
        private IEventProcessingPipeline? _pipeline;
        private readonly object _pipelineLock = new();
        private bool _isDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventBus"/> class with optional services.
        /// </summary>
        /// <param name="logger">The logger to use.</param>
        /// <param name="topologyService">Optional topology service for event routing based on component relationships.</param>
        /// <param name="eventStore">Optional event store for persisting events.</param>
        /// <param name="retryPolicy">Optional retry policy for failed events.</param>
        /// <param name="transformer">Optional event transformer.</param>
        public EventBus(
            ILogger logger,
            ITopologyService? topologyService = null,
            IEventStore? eventStore = null,
            IEventRetryPolicy? retryPolicy = null,
            IEventTransformer? transformer = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _topologyService = topologyService;
            _eventStore = eventStore;
            _retryPolicy = retryPolicy;
            _transformer = transformer;
            
            if (_topologyService != null)
            {
                _logger.Log("EventBus initialized with topology service");
            }
            else
            {
                _logger.Log("EventBus initialized without topology service (routing limited to direct targets)");
            }
        }

        /// <inheritdoc/>
        public void SetTopologyService(ITopologyService topologyService)
        {
            // This is already handled in the constructor, but we implement for interface compatibility
            if (_topologyService == null)
            {
                // We can't actually update the reference since it's readonly, so log a warning
                _logger.Log("Warning: Attempting to set topology service after initialization. This has no effect - use constructor injection instead.");
            }
        }

        /// <inheritdoc/>
        public ITopologyService? GetTopologyService()
        {
            return _topologyService;
        }

        /// <summary>
        /// Sets the event processing pipeline for the EventBus.
        /// </summary>
        /// <param name="pipeline">The event processing pipeline to use.</param>
        public void SetEventProcessingPipeline(IEventProcessingPipeline pipeline)
        {
            lock (_pipelineLock)
            {
                _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
                _logger.Log($"Event processing pipeline configured for EventBus");
            }
        }

        /// <summary>
        /// Gets the event processing pipeline used by the EventBus.
        /// </summary>
        /// <returns>The event processing pipeline, or null if none is configured.</returns>
        public IEventProcessingPipeline? GetEventProcessingPipeline()
        {
            lock (_pipelineLock)
            {
                return _pipeline;
            }
        }

        /// <inheritdoc/>
        public Guid Subscribe<T>(T handler, IEventSubscriptionOptions? options = null) where T : IEventHandler
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            var subscription = new EventSubscription(
                Guid.NewGuid(),
                handler,
                options ?? new EventSubscriptionOptions());

            _subscriptions[subscription.Id] = subscription;

            // If no event types specified, subscribe to all event types
            var eventTypes = subscription.Options.EventTypes.Length > 0
                ? subscription.Options.EventTypes
                : Enum.GetValues<EventType>();

            // Add to type-based lookup for faster matching
            foreach (var eventType in eventTypes)
            {
                _subscriptionsByType.AddOrUpdate(
                    eventType,
                    new List<EventSubscription> { subscription },
                    (_, list) =>
                    {
                        list.Add(subscription);
                        return list;
                    });
            }

            _logger.Log($"Handler {handler.GetType().Name} subscribed with ID {subscription.Id}");
            return subscription.Id;
        }

        /// <inheritdoc/>
        public bool Unsubscribe(Guid subscriptionId)
        {
            if (!_subscriptions.TryRemove(subscriptionId, out var subscription))
            {
                return false;
            }

            // If no event types specified, it was subscribed to all event types
            var eventTypes = subscription.Options.EventTypes.Length > 0
                ? subscription.Options.EventTypes
                : Enum.GetValues<EventType>();

            // Remove from type-based lookup
            foreach (var eventType in eventTypes)
            {
                if (_subscriptionsByType.TryGetValue(eventType, out var list))
                {
                    list.Remove(subscription);
                }
            }

            _logger.Log($"Handler with ID {subscriptionId} unsubscribed");
            return true;
        }

        /// <inheritdoc/>
        public async Task<IPublishResult?> PublishAsync(object? sender, IEvent evt, CancellationToken ct = default)
        {
            if (sender == null)
                throw new ArgumentNullException(nameof(sender));
            if (evt == null)
                throw new ArgumentNullException(nameof(evt));

            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                _logger.Log($"Publishing event {evt.EventId} of type {evt.EventType}");

                // Check if we have a pipeline configured
                IEventProcessingPipeline? pipeline;
                lock (_pipelineLock)
                {
                    pipeline = _pipeline;
                }

                // If we have a pipeline, use it first
                if (pipeline != null)
                {
                    try
                    {
                        var pipelineResult = await pipeline.ProcessEventAsync(sender, evt, ct);

                        // If the pipeline processed the event successfully, we're done
                        if (pipelineResult.IsSuccess)
                        {
                            stopwatch.Stop();
                            _logger.Log($"Event {evt.EventId} processed successfully by pipeline in {stopwatch.ElapsedMilliseconds}ms");

                            // Return a result that indicates success
                            return new PublishResult
                            {
                                EventId = evt.EventId,
                                HandlerCount = 1, // We don't know exactly how many handlers processed it
                                SuccessCount = 1
                            };
                        }

                        // Log the pipeline failure but continue with standard processing
                        if (pipelineResult.Exception != null)
                        {
                            _logger.Log(pipelineResult.Exception,
                                $"Pipeline processing failed for event {evt.EventId}, " +
                                $"falling back to standard event handling");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Log(ex, $"Error in pipeline processing for event {evt.EventId}");
                        // Continue with standard processing
                    }
                }

                // Create the result object to track delivery
                var result = new PublishResult
                {
                    EventId = evt.EventId,
                    HandlerCount = 0,
                    SuccessCount = 0
                };

                // Find matching subscriptions
                var matchingSubscriptions = await GetMatchingSubscriptionsAsync(evt, ct);
                result.HandlerCount = matchingSubscriptions.Count;

                if (matchingSubscriptions.Count == 0)
                {
                    // If the event is configured to be persisted, do so
                    if (evt.RoutingData?.Persist == true && _eventStore != null)
                    {
                        await _eventStore.PersistEventAsync(evt);
                        _logger.Log($"Event {evt.EventId} persisted with no matching handlers");
                    }
                    
                    stopwatch.Stop();
                    _logger.Log($"No matching handlers found for event {evt.EventId} (completed in {stopwatch.ElapsedMilliseconds}ms)");
                    return result;
                }

                // Process synchronous subscriptions first
                var syncSubscriptions = matchingSubscriptions
                    .Where(s => s.Options.Synchronous)
                    .ToList();

                bool hasErrors = false;
                
                foreach (var subscription in syncSubscriptions)
                {
                    try
                    {
                        await subscription.Handler.HandleEventAsync(sender, evt, ct);
                        result.SuccessCount++;
                    }
                    catch (Exception ex)
                    {
                        hasErrors = true;
                        _logger.Log(ex, $"Error in synchronous handler for event {evt.EventId}");
                        result.Errors.Add(ex);
                    }
                }

                // Process asynchronous subscriptions
                var asyncSubscriptions = matchingSubscriptions
                    .Where(s => !s.Options.Synchronous)
                    .ToList();

                var asyncTasks = new List<Task>();

                foreach (var subscription in asyncSubscriptions)
                {
                    var task = HandleEventWithErrorCaptureAsync(sender, evt, subscription, result, ct);
                    asyncTasks.Add(task);
                    result.HandlerTasks.Add(task);
                }

                // If there's a timeout specified, respect it
                if (asyncTasks.Count > 0 && evt.RoutingData?.Timeout.HasValue == true)
                {
                    var timeout = evt.RoutingData.Timeout.Value;

                    if (await Task.WhenAny(
                        Task.WhenAll(asyncTasks),
                        Task.Delay(timeout, ct)) == Task.Delay(timeout, ct))
                    {
                        result.TimedOut = true;
                        _logger.Log($"Async handlers for event {evt.EventId} timed out after {timeout.TotalMilliseconds}ms");
                    }
                }
                else if (asyncTasks.Count > 0)
                {
                    // Wait for all async tasks to complete if no timeout
                    await Task.WhenAll(asyncTasks);
                }

                // If event had errors and we have an event store, persist for retry
                if (hasErrors || result.HasErrors)
                {
                    if (_eventStore != null)
                    {
                        await _eventStore.PersistEventAsync(evt);
                        _logger.Log($"Event {evt.EventId} persisted due to handler errors for potential retry");
                    }
                }
                
                // If the event is configured to be persisted, do so even if handled successfully
                else if (evt.RoutingData?.Persist == true && _eventStore != null)
                {
                    await _eventStore.PersistEventAsync(evt);
                    _logger.Log($"Event {evt.EventId} persisted as specified in routing data");
                }

                stopwatch.Stop();
                _logger.Log($"Event {evt.EventId} published to {result.HandlerCount} handlers " +
                            $"with {result.SuccessCount} successful in {stopwatch.ElapsedMilliseconds}ms");
                
                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.Log(ex, $"Error publishing event {evt.EventId} after {stopwatch.ElapsedMilliseconds}ms");
                return PublishResult.Failure(evt.EventId, ex);
            }
        }

        /// <summary>
        /// Processes any failed events that were stored for retry.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task ProcessFailedEventsAsync(CancellationToken ct = default)
        {
            if (_eventStore == null || _retryPolicy == null)
            {
                _logger.Log("Cannot process failed events: event store or retry policy is not configured");
                return;
            }

            try
            {
                var failedEvent = await _eventStore.RetrieveFailedEventAsync();
                if (failedEvent == null)
                {
                    return;
                }

                _logger.Log($"Retrieved failed event {failedEvent.EventId} for retry processing");

                // Check if we should retry
                bool shouldRetry = await _retryPolicy.ShouldRetryAsync(failedEvent, 1);
                if (shouldRetry)
                {
                    // Apply transformation if transformer is available
                    IEvent eventToPublish = failedEvent;
                    if (_transformer != null)
                    {
                        eventToPublish = _transformer.Transform(failedEvent);
                    }

                    _logger.Log($"Retrying failed event {failedEvent.EventId}");
                    await PublishAsync(this, eventToPublish, ct);
                }
                else
                {
                    _logger.Log($"Failed event {failedEvent.EventId} will not be retried based on policy decision");
                }
            }
            catch (Exception ex)
            {
                _logger.Log(ex, $"Error processing failed events");
            }
        }

        /// <summary>
        /// Gets matching subscriptions for an event based on event type, source ID, and topology.
        /// </summary>
        private async Task<List<EventSubscription>> GetMatchingSubscriptionsAsync(
            IEvent evt,
            CancellationToken ct = default)
        {
            var results = new List<EventSubscription>();

            // Check if we have subscriptions for this event type
            if (!_subscriptionsByType.TryGetValue(evt.EventType, out var typeSubscriptions))
            {
                return results;
            }

            // Check if the event has specific targets
            if (evt.RoutingData?.TargetIds.Length > 0)
            {
                // The event has explicit targets, only deliver to those
                var targetSet = new HashSet<Guid>(evt.RoutingData.TargetIds);

                foreach (var subscription in typeSubscriptions)
                {
                    // Check if any subscription source ID matches a target
                    if (subscription.Options.SourceIds.Length > 0)
                    {
                        foreach (var sourceId in subscription.Options.SourceIds)
                        {
                            if (targetSet.Contains(sourceId))
                            {
                                // Apply custom filter if specified
                                if (subscription.Options.Filter != null && !subscription.Options.Filter(evt))
                                {
                                    continue;
                                }

                                results.Add(subscription);
                                break;
                            }
                        }
                    }
                }

                return results;
            }

            // No explicit targets, use standard subscription filtering
            foreach (var subscription in typeSubscriptions)
            {
                // Check source ID filter
                if (subscription.Options.SourceIds.Length > 0)
                {
                    bool sourceMatch = subscription.Options.SourceIds.Contains(evt.SourceId);

                    if (!sourceMatch)
                    {
                        // If not a direct match and we shouldn't include connected sources, skip
                        if (!subscription.Options.IncludeConnectedSources)
                        {
                            continue;
                        }

                        // Check topology connections if we have a topology service
                        if (_topologyService != null)
                        {
                            bool connected = await CheckTopologyConnectionAsync(
                                evt.SourceId,
                                subscription.Options.SourceIds,
                                ct);

                            if (!connected)
                            {
                                continue;
                            }
                        }
                        else
                        {
                            // No topology service but we need it, so no match
                            continue;
                        }
                    }
                }

                // Apply custom filter if specified
                if (subscription.Options.Filter != null && !subscription.Options.Filter(evt))
                {
                    continue;
                }

                results.Add(subscription);
            }

            return results;
        }

        /// <summary>
        /// Checks if the source is connected to any of the target IDs via the topology.
        /// </summary>
        private async Task<bool> CheckTopologyConnectionAsync(
            Guid sourceId,
            Guid[] targetIds,
            CancellationToken ct)
        {
            if (_topologyService == null)
            {
                return false;
            }

            foreach (var targetId in targetIds)
            {
                try
                {
                    if (sourceId == targetId)
                    {
                        return true; // Self-connection is always true
                    }

                    // Check for direct connection
                    var connections = await _topologyService.GetConnectionsForSourceAsync(sourceId, ct);
                    foreach (var connection in connections)
                    {
                        if (!connection.IsEnabled)
                            continue;

                        if (connection.TargetId == targetId)
                        {
                            // Evaluate condition if present
                            if (string.IsNullOrEmpty(connection.Condition) || 
                                await _topologyService.EvaluateConnectionConditionAsync(connection, ct))
                            {
                                return true;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Log(ex, $"Error checking topology connection between {sourceId} and {targetId}");
                }
            }

            return false;
        }

        /// <summary>
        /// Handles an event with error capture for asynchronous handlers.
        /// </summary>
        private async Task HandleEventWithErrorCaptureAsync(
            object sender,
            IEvent evt,
            EventSubscription subscription,
            PublishResult result,
            CancellationToken ct)
        {
            try
            {
                await subscription.Handler.HandleEventAsync(sender, evt, ct);

                lock (result)
                {
                    result.SuccessCount++;
                }
            }
            catch (Exception ex)
            {
                _logger.Log(ex, $"Error in async handler for event {evt.EventId}");

                lock (result)
                {
                    result.Errors.Add(ex);
                }
            }
        }

        /// <summary>
        /// Disposes resources used by the event bus.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;

            // Dispose pipeline if disposable
            lock (_pipelineLock)
            {
                if (_pipeline is IDisposable disposablePipeline)
                {
                    disposablePipeline.Dispose();
                    _pipeline = null;
                }
            }

            // Clear subscriptions
            _subscriptions.Clear();
            _subscriptionsByType.Clear();

            GC.SuppressFinalize(this);
        }
    }
}