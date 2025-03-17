using System.Collections.Concurrent;
using System.Diagnostics;
using HydroGarden.Foundation.Abstractions.Interfaces;
using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Foundation.Abstractions.Interfaces.Events.Routing;
using HydroGarden.Foundation.Abstractions.Interfaces.Services;
using HydroGarden.Foundation.Common.Events.Adapters;
using HydroGarden.Logger.Abstractions;

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
        private readonly IEventRouter _router;
        private readonly IEventStore? _eventStore;
        private readonly IEventTransformer? _transformer;
        private IEventProcessingPipeline? _pipeline;
        private ITopologyService? _topologyService;
        private readonly object _pipelineLock = new();
        private bool _isDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventBus"/> class with optional services.
        /// </summary>
        /// <param name="logger">The logger to use.</param>
        /// <param name="router">The event router to use for subscription matching.</param>
        /// <param name="eventStore">Optional event store for persisting events.</param>
        /// <param name="transformer">Optional event transformer that will be applied during event publishing.</param>
        public EventBus(
            ILogger logger,
            IEventRouter router,
            IEventStore? eventStore = null,
            IEventTransformer? transformer = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _router = router ?? throw new ArgumentNullException(nameof(router));
            _eventStore = eventStore;
            _transformer = transformer;


            _logger.Log("EventBus initialized with router: " + _router.GetType().Name);

            if (_transformer != null)
            {
                _logger.Log("Event transformer configured: " + _transformer.GetType().Name);
            }
        }

        /// <inheritdoc/>
        public Guid Subscribe<TEvent>(IEventHandler<IEvent> handler, IEventSubscriptionOptions? options) where TEvent : IEvent
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            // Create subscription with the handler
            var subscription = new EventSubscription(
                Guid.NewGuid(),
                handler,
                options ?? new EventSubscriptionOptions());

            _subscriptions[subscription.Id] = subscription;

            // If no event types specified in options, determine based on TEvent
            EventType[] eventTypes;
            if (options?.EventTypes == null || options.EventTypes.Length == 0)
            {
                // Try to determine event type from TEvent
                var eventProperty = typeof(TEvent).GetProperty("EventType");
                if (eventProperty != null && eventProperty.PropertyType == typeof(EventType))
                {
                    // If TEvent has an EventType property, we'll use it at runtime
                    // For now, default to Custom since we don't have an instance
                    eventTypes = new[] { EventType.Custom };
                }
                else
                {
                    // If we can't determine event type, subscribe to all types
                    eventTypes = Enum.GetValues<EventType>();
                }
            }
            else
            {
                // Use the event types specified in options
                eventTypes = options.EventTypes;
            }

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
        public Guid Subscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : IEvent
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));
            
            // Create adapter to convert IEventHandler<TEvent> to IEventHandler<IEvent>
            var adapter = new GenericEventHandlerAdapter<TEvent>(handler);
            
            // Build event types based on TEvent
            var eventTypes = new EventSubscriptionOptions();
            // Try to determine event type from TEvent
            var eventProperty = typeof(TEvent).GetProperty("EventType");
            if (eventProperty != null && eventProperty.PropertyType == typeof(EventType))
            {
                // If TEvent has an EventType property, we'll use it at runtime
                // For now, default to Custom since we don't have an instance
                eventTypes.EventTypes = new[] { EventType.Custom };
            }
            else
            {
                // If we can't determine event type, subscribe to all types
                eventTypes.EventTypes = Enum.GetValues<EventType>();
            }
            
            return Subscribe<TEvent>(adapter, eventTypes);
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

                // Apply transformation if transformer is available
                if (_transformer != null)
                {
                    try
                    {
                        evt = _transformer.Transform(evt);
                        _logger.Log($"Event {evt.EventId} transformed");
                    }
                    catch (Exception ex)
                    {
                        _logger.Log(ex, $"Error transforming event {evt.EventId}");
                        return PublishResult.Failure(evt.EventId, ex);
                    }
                }

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
                            _logger.Log(
                                $"Event {evt.EventId} processed successfully by pipeline in {stopwatch.ElapsedMilliseconds}ms");

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

                // Find matching subscriptions using the router
                var matchingSubscriptions = await GetMatchingSubscriptionsAsync(evt, ct);
                result.HandlerCount = matchingSubscriptions.Count;

                if (matchingSubscriptions.Count == 0)
                {
                    // If the event is configured to be persisted, do so
                    if (evt.RoutingData?.Persist == true && _eventStore is not null)
                    {
                        await _eventStore.PersistEventAsync(evt);
                        _logger.Log($"Event {evt.EventId} persisted with no matching handlers");
                    }

                    stopwatch.Stop();
                    _logger.Log(
                        $"No matching handlers found for event {evt.EventId} (completed in {stopwatch.ElapsedMilliseconds}ms)");
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

                    // Create a delay task outside the WhenAny call
                    var delayTask = Task.Delay(timeout, ct);

                    // Compare task references, not results
                    var completedTask = await Task.WhenAny(
                        Task.WhenAll(asyncTasks),
                        delayTask);

                    if (completedTask == delayTask)
                    {
                        result.TimedOut = true;
                        _logger.Log(
                            $"Async handlers for event {evt.EventId} timed out after {timeout.TotalMilliseconds}ms");
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
                    if (_eventStore is not null)
                    {
                        await _eventStore.PersistEventAsync(evt);
                        _logger.Log($"Event {evt.EventId} persisted due to handler errors for potential retry");
                    }
                }
                // If the event is configured to be persisted, do so even if handled successfully
                else if (evt.RoutingData?.Persist == true && _eventStore is not null)
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
        /// Processes any failed events that were stored.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task ProcessFailedEventsAsync(CancellationToken ct = default)
        {
            if (_eventStore is null)
            {
                _logger.Log("Cannot process failed events: event store is not configured");
                return;
            }

            try
            {
                var failedEvent = await _eventStore.RetrieveFailedEventAsync();
                if (failedEvent == null)
                {
                    return;
                }

                _logger.Log($"Retrieved failed event {failedEvent.EventId} for processing");

                // Just publish the event - the transformer will be applied in PublishAsync
                _logger.Log($"Publishing failed event {failedEvent.EventId}");
                await PublishAsync(this, failedEvent, ct);
            }
            catch (Exception ex)
            {
                _logger.Log(ex, $"Error processing failed events");
            }
        }

        /// <summary>
        /// Gets matching subscriptions for an event using the EventRouter.
        /// </summary>
        private Task<IReadOnlyList<IEventSubscription>> GetMatchingSubscriptionsAsync(
            IEvent evt,
            CancellationToken ct = default)
        {
            // Get all subscriptions for this event type as a performance optimization
            if (!_subscriptionsByType.TryGetValue(evt.EventType, out var typeSubscriptions))
            {
                return Task.FromResult<IReadOnlyList<IEventSubscription>>(Array.Empty<IEventSubscription>());
            }

            // Delegate subscription matching to the router
            return _router.GetMatchingSubscriptionsAsync(evt, typeSubscriptions, ct);
        }

        /// <summary>
        /// Handles an event with error capture for asynchronous handlers.
        /// </summary>
        private async Task HandleEventWithErrorCaptureAsync(
            object sender,
            IEvent evt,
            IEventSubscription subscription,
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
        /// Sets the topology service for event routing.
        /// </summary>
        /// <param name="topologyService">The topology service to use for routing events.</param>
        public void SetTopologyService(ITopologyService topologyService)
        {
            _topologyService = topologyService ?? throw new ArgumentNullException(nameof(topologyService));
            _logger.Log($"EventBus configured with topology service: {topologyService.GetType().Name}");
        }

        /// <summary>
        /// Gets the topology service used by this event bus.
        /// </summary>
        /// <returns>The topology service, or null if not configured.</returns>
        public ITopologyService? GetTopologyService()
        {
            return _topologyService;
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
                    try
                    {
                        disposablePipeline.Dispose();
                    }
                    catch (Exception ex)
                    {
                        _logger.Log(ex, "Error disposing event processing pipeline");
                    }
                    finally
                    {
                        _pipeline = null;
                    }
                }
            }

            // Clear subscriptions
            _subscriptions.Clear();
            _subscriptionsByType.Clear();

            GC.SuppressFinalize(this);
        }
    }
}