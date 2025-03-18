using System.Collections.Concurrent;
using System.Diagnostics;
using HydroGarden.Foundation.Abstractions.Interfaces;
using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Foundation.Abstractions.Interfaces.Events.Routing;
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
        private readonly IEventProcessingPipeline _pipeline;
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
            var transformer1 = transformer;

            // Initialize the event processing pipeline
            _pipeline = new DefaultEventProcessingPipeline(_logger);

            InitializeMiddleware(transformer1).GetAwaiter().GetResult();
        }

        private async Task InitializeMiddleware(IEventTransformer? transformer1)
        {
            // Add the validation middleware first (highest priority)
            await _pipeline.AddMiddleware(new EventValidationMiddleware(_logger));

            // Add the state change middleware to ensure proper handling of state change events
            await _pipeline.AddMiddleware(new StateChangeMiddleware(_logger));

            _logger.Log("EventBus initialized with router: " + _router.GetType().Name);

            if (transformer1 != null)
            {
                _logger.Log("Event transformer configured: " + transformer1.GetType().Name);

                // Register the transformer middleware - make it higher priority than state change middleware
                var transformerMiddleware = new DefaultTransformerMiddleware(transformer1, _logger);
                await _pipeline.AddMiddleware(transformerMiddleware);
            }
        }

        public EventBus(IEventProcessingPipeline pipeline, ILogger logger, IEventRouter router)
        {
            _pipeline = pipeline;
            _logger = logger;
            _router = router;
        }

        /// <inheritdoc/>
        public Guid Subscribe<TEvent>(IEventHandler<IEvent> handler, IEventSubscriptionOptions? options) where TEvent : IEvent
        {
            ArgumentNullException.ThrowIfNull(handler);

            // Create subscription with the handler
            var subscription = new EventSubscription(
                Guid.NewGuid(),
                handler,
                options ?? new EventSubscriptionOptions());

            _logger.Log($"Creating subscription for handler {handler.GetType().Name} with ID {subscription.Id}");

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
                    // For now, subscribe to all types since we don't have an instance
                    eventTypes = Enum.GetValues<EventType>();
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
                    [subscription],
                    (_, list) =>
                    {
                        // Thread-safe implementation to avoid enumeration errors
                        // Create a new list that contains all existing subscriptions plus this one
                        // but avoid duplicates by checking the ID
                        var newList = new List<EventSubscription>(list.Count + 1);
                        bool alreadyExists = false;
                        
                        // Copy existing subscriptions
                        foreach (var existing in list)
                        {
                            newList.Add(existing);
                            if (existing.Id == subscription.Id)
                                alreadyExists = true;
                        }
                        
                        // Add the new subscription if it's not already in the list
                        if (!alreadyExists)
                            newList.Add(subscription);
                            
                        return newList;
                    });

                _logger.Log($"Handler {handler.GetType().Name} subscribed to event type {eventType} with ID {subscription.Id}");
            }

            _logger.Log($"Handler {handler.GetType().Name} subscribed with ID {subscription.Id} for {eventTypes.Length} event types");
            return subscription.Id;
        }

        /// <inheritdoc/>
        public Guid Subscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : IEvent
        {

            ArgumentNullException.ThrowIfNull(handler);

            // Create adapter to convert IEventHandler<TEvent> to IEventHandler<IEvent>
            var adapter = new GenericEventHandlerAdapter<TEvent>(handler);

            // Build event types based on TEvent
            var eventTypes = new EventSubscriptionOptions();
            // Try to determine event type from TEvent
            var eventProperty = typeof(TEvent).GetProperty("EventType");
            if (eventProperty != null && eventProperty.PropertyType == typeof(EventType))
            {
                // If TEvent has an EventType property, subscribe to all types
                // since we don't know which type the event will be at runtime
                eventTypes.EventTypes = Enum.GetValues<EventType>();
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

            ArgumentNullException.ThrowIfNull(sender);
            ArgumentNullException.ThrowIfNull(evt);

            var stopwatch = Stopwatch.StartNew();

            try
            {
                _logger.Log($"Publishing event {evt.EventId} of type {evt.EventType}");

                // Use the pipeline to process the event (including transformation)
                IEvent eventToPublish = evt;
                Exception? pipelineException = null;

                try
                {
                    var pipelineResult = await _pipeline.ProcessEventAsync(sender, evt, ct);

                    // If the pipeline was successful, use the potentially transformed event
                    if (pipelineResult.IsSuccess)
                    {
                        eventToPublish = pipelineResult.ProcessedEvent;
                        _logger.Log(
                            $"Event {evt.EventId} preprocessed successfully by pipeline in {stopwatch.ElapsedMilliseconds}ms");
                    }
                    else if (pipelineResult.Exception != null)
                    {
                        pipelineException = pipelineResult.Exception;
                        _logger.Log(pipelineResult.Exception,
                            $"Pipeline preprocessing failed for event {evt.EventId}, " +
                            $"but continuing with event handling");
                    }
                }
                catch (Exception ex)
                {
                    pipelineException = ex;
                    _logger.Log(ex, $"Error in pipeline preprocessing for event {evt.EventId}");
                    // Continue with standard processing
                }

                // Create the result object to track delivery
                var result = new PublishResult
                {
                    EventId = evt.EventId,
                    HandlerCount = 0,
                    SuccessCount = 0
                };

                // Add any pipeline exception if it occurred
                if (pipelineException != null)
                {
                    result.AddError(pipelineException);

                    // If pipeline processing failed with an exception, don't proceed to handlers
                    _logger.Log($"Pipeline failed with exception for event {evt.EventId}, skipping handler processing");
                    return result;
                }

                // Find matching subscriptions using the router with the potentially transformed event
                var matchingSubscriptions = await GetMatchingSubscriptionsAsync(eventToPublish, ct);
                result.HandlerCount = matchingSubscriptions.Count;

                if (matchingSubscriptions.Count == 0)
                {
                    // If the event is configured to be persisted, do so
                    if (eventToPublish.RoutingData?.Persist == true && _eventStore is not null)
                    {
                        await _eventStore.PersistEventAsync(eventToPublish);
                        _logger.Log($"Event {evt.EventId} persisted with no matching handlers");
                    }

                    stopwatch.Stop();
                    _logger.Log(
                        $"No matching handlers found for event {evt.EventId} (completed in {stopwatch.ElapsedMilliseconds}ms)");

                    // If there were pipeline errors but no handlers to execute, ensure they're reported
                    if (pipelineException != null && !result.HasErrors)
                    {
                        result.AddError(pipelineException);
                    }

                    return result;
                }

                // Process synchronous subscriptions first
                var syncSubscriptions = matchingSubscriptions
                    .Where(s => s.Options.Synchronous)
                    // For state change events, prioritize them based on state transition order for proper testing
                    .OrderByDescending(s => evt.EventType == EventType.StateChange)
                    .ToList();

                bool hasErrors = false;

                foreach (var subscription in syncSubscriptions)
                {
                    try
                    {
                        await subscription.Handler.HandleEventAsync(sender, eventToPublish, ct);
                        result.SuccessCount++;
                    }
                    catch (Exception ex)
                    {
                        hasErrors = true;
                        _logger.Log(ex, $"Error in synchronous handler for event {evt.EventId}");
                        result.AddError(ex);
                    }
                }

                // Process asynchronous subscriptions
                var asyncSubscriptions = matchingSubscriptions
                    .Where(s => !s.Options.Synchronous)
                    .ToList();

                var asyncTasks = new List<Task>();

                foreach (var subscription in asyncSubscriptions)
                {
                    var task = HandleEventWithErrorCaptureAsync(sender, eventToPublish, subscription, result, ct);
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
                        await _eventStore.PersistEventAsync(eventToPublish);
                        _logger.Log($"Event {evt.EventId} persisted due to handler errors for potential retry");
                    }
                }
                // If the event is configured to be persisted, do so even if handled successfully
                else if (eventToPublish.RoutingData?.Persist == true && _eventStore is not null)
                {
                    await _eventStore.PersistEventAsync(eventToPublish);
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
        private Task<IReadOnlyList<IEventSubscription>> GetMatchingSubscriptionsAsync(IEvent evt, CancellationToken ct = default)
        {
            _logger.Log($"Finding matching subscriptions for event {evt.EventId} of type {evt.EventType}");

            // Get subscriptions for this specific event type
            bool hasTypeSpecificSubscriptions = _subscriptionsByType.TryGetValue(evt.EventType, out var typeSubscriptions);

            // Always consider generic subscriptions in addition to type-specific ones
            bool hasGenericSubscriptions = _subscriptionsByType.TryGetValue(EventType.Custom, out List<EventSubscription>? genericSubscriptions);

            if (!hasTypeSpecificSubscriptions && !hasGenericSubscriptions)
            {
                _logger.Log($"No subscriptions found for event type {evt.EventType}");
                return Task.FromResult<IReadOnlyList<IEventSubscription>>([]);
            }

            // Use a HashSet to ensure unique subscriptions by ID
            HashSet<Guid> includedIds = [];
            List<EventSubscription> mergedSubscriptions = [];
            
            // Add type-specific subscriptions first
            if (hasTypeSpecificSubscriptions && typeSubscriptions != null)
            {
                foreach (var subscription in typeSubscriptions)
                {
                    if (includedIds.Add(subscription.Id)) // Only add if not already included
                    {
                        mergedSubscriptions.Add(subscription);
                    }
                }
                _logger.Log($"Found {typeSubscriptions.Count} type-specific subscriptions for {evt.EventType}, added {mergedSubscriptions.Count} unique ones");
            }
            
            // Then add generic subscriptions that haven't been included yet
            if (hasGenericSubscriptions && genericSubscriptions != null)
            {
                int beforeCount = mergedSubscriptions.Count;
                foreach (var subscription in genericSubscriptions)
                {
                    if (includedIds.Add(subscription.Id)) // Only add if not already included
                    {
                        mergedSubscriptions.Add(subscription);
                    }
                }
                _logger.Log($"Found {genericSubscriptions.Count} generic subscriptions, added {mergedSubscriptions.Count - beforeCount} unique ones");
            }

            // Delegate subscription matching to the router
            return _router.GetMatchingSubscriptionsAsync(evt, mergedSubscriptions, ct);
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
                    result.AddError(ex);
                }
            }
        }

        /// <summary>
        /// Adds middleware to the event processing pipeline.
        /// </summary>
        /// <param name="middleware">The middleware to add.</param>
        public void AddPipelineMiddleware(IEventMiddleware middleware)
        {

            ArgumentNullException.ThrowIfNull(middleware);

            lock (_pipelineLock)
            {
                _pipeline.AddMiddleware(middleware);
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

            // Dispose pipeline
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
                }
            }

            // Clear subscriptions
            _subscriptions.Clear();
            _subscriptionsByType.Clear();

            GC.SuppressFinalize(this);
        }
        /// <summary>
        /// Disposes resources used by the event bus.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;

            // Dispose pipeline
            IAsyncDisposable? asyncDisposablePipeline = null;
            IDisposable? disposablePipeline = null;

            lock (_pipelineLock)
            {
                if (_pipeline is IAsyncDisposable asyncPipeline)
                {
                    asyncDisposablePipeline = asyncPipeline;
                }
                else if (_pipeline is IDisposable syncPipeline)
                {
                    disposablePipeline = syncPipeline;
                }
            }

            if (asyncDisposablePipeline != null)
            {
                try
                {
                    await asyncDisposablePipeline.DisposeAsync();
                }
                catch (Exception ex)
                {
                    _logger.Log(ex, "Error disposing event processing pipeline");
                }
            }
            else if (disposablePipeline != null)
            {
                try
                {
                    disposablePipeline.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.Log(ex, "Error disposing event processing pipeline");
                }
            }

            // Clear subscriptions
            _subscriptions.Clear();
            _subscriptionsByType.Clear();
        }
    }
    
}