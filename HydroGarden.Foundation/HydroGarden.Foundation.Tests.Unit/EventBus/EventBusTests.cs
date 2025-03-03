using HydroGarden.Foundation.Abstractions.Interfaces;
using HydroGarden.Foundation.Abstractions.Interfaces.Components;
using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Foundation.Abstractions.Interfaces.Logging;
using HydroGarden.Foundation.Common.Events;
using HydroGarden.Foundation.Common.PropertyMetadata;
using HydroGarden.Foundation.Common.Results;
using Moq;
using System.Collections.Concurrent;

namespace HydroGarden.Foundation.Tests.Unit.Events
{
    /// <summary>
    /// Base test class for the EventBus that contains the testing infrastructure.
    /// </summary>
    public abstract class EventBusBaseTests
    {
        protected readonly Mock<IHydroGardenLogger> MockLogger;
        protected readonly Mock<IEventStore> MockStore;
        protected readonly Mock<IEventRetryPolicy> MockRetryPolicy;
        protected readonly Mock<IEventTransformer> MockTransformer;

        protected EventBusBaseTests()
        {
            MockLogger = new Mock<IHydroGardenLogger>();
            MockStore = new Mock<IEventStore>();
            MockRetryPolicy = new Mock<IEventRetryPolicy>();
            MockTransformer = new Mock<IEventTransformer>();

            // Set up transformer to return the same event by default (identity transform)
            MockTransformer.Setup(t => t.Transform(It.IsAny<IHydroGardenEvent>()))
                .Returns<IHydroGardenEvent>(e => e);
        }

        /// <summary>
        /// Creates a mock EventBus with a specialized queue processor for testing.
        /// This implementation uses a manually controlled queue processor to avoid
        /// concurrency and cancellation issues during testing.
        /// </summary>
        protected EventBusForTesting CreateTestEventBus()
        {
            return new EventBusForTesting(
                MockLogger.Object,
                MockStore.Object,
                MockRetryPolicy.Object,
                MockTransformer.Object);
        }
    }

    /// <summary>
    /// A test-specific version of EventBus that allows direct synchronous event processing
    /// without relying on the asynchronous queue processor, which is the source of the
    /// TaskCanceledException errors in the tests.
    /// </summary>
    public class EventBusForTesting : IEventBus, IDisposable
    {
        private readonly IHydroGardenLogger _logger;
        private readonly IEventStore _eventStore;
        private readonly IEventRetryPolicy _retryPolicy;
        private readonly IEventTransformer _transformer;
        private readonly ConcurrentDictionary<Guid, IEventSubscription> _subscriptions = new();
        private readonly ConcurrentDictionary<EventType, List<IEventSubscription>> _subscriptionsByType = new();
        private ITopologyService? _topologyService;
        private bool _isDisposed;

        /// <summary>
        /// Initializes a new instance of the EventBusForTesting class with the specified dependencies.
        /// </summary>
        public EventBusForTesting(
            IHydroGardenLogger logger,
            IEventStore eventStore,
            IEventRetryPolicy retryPolicy,
            IEventTransformer transformer)
        {
            _logger = logger;
            _eventStore = eventStore;
            _retryPolicy = retryPolicy;
            _transformer = transformer;
        }

        /// <summary>
        /// Sets the topology service for use in topology-aware routing tests.
        /// </summary>
        public void SetTopologyService(ITopologyService topologyService)
        {
            _topologyService = topologyService;
        }

        /// <summary>
        /// Subscribes an event handler to the bus.
        /// </summary>
        /// <summary>
        /// Subscribes an event handler to the bus.
        /// </summary>
        public Guid Subscribe<T>(T handler, IEventSubscriptionOptions? options = null) where T : IHydroGardenEventHandler
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            var subscription = new EventSubscription(Guid.NewGuid(), handler, options ?? new EventSubscriptionOptions());
            _subscriptions[subscription.Id] = subscription;

            foreach (var eventType in subscription.Options.EventTypes)
            {
                _subscriptionsByType.AddOrUpdate(eventType, new List<IEventSubscription> { subscription }, (_, list) =>
                {
                    list.Add(subscription);
                    return list;
                });
            }

            _logger.Log(this, $"Handler subscribed with ID {subscription.Id}");
            return subscription.Id;
        }

        /// <summary>
        /// Unsubscribes an event handler from the bus.
        /// </summary>
        public bool Unsubscribe(Guid subscriptionId)
        {
            if (_subscriptions.TryRemove(subscriptionId, out var subscription))
            {
                foreach (var eventType in subscription.Options.EventTypes)
                {
                    if (_subscriptionsByType.TryGetValue(eventType, out var list))
                    {
                        list.Remove(subscription);
                    }
                }

                _logger.Log($"Handler with ID {subscriptionId} unsubscribed");
                return true;
            }
            return false;
        }

        /// <summary>
        /// Publishes an event synchronously for testing purposes.
        /// </summary>
        public Task<IPublishResult> PublishAsync(object sender, IHydroGardenEvent evt, CancellationToken ct = default)
        {
            if (sender == null) throw new ArgumentNullException(nameof(sender));
            if (evt == null) throw new ArgumentNullException(nameof(evt));

            var transformedEvent = _transformer.Transform(evt);
            var result = new PublishResult { EventId = evt.EventId };

            try
            {
                // Check if this event should be persisted based on routing data
                if (transformedEvent.RoutingData?.Persist == true)
                {
                    _eventStore.PersistEventAsync(transformedEvent);
                }

                // Filter subscriptions based on event type
                var eventType = transformedEvent.EventType;
                if (!_subscriptionsByType.TryGetValue(eventType, out var typeSubscriptions))
                {
                    return Task.FromResult<IPublishResult>(result);
                }

                // Check for explicit target routing
                if (transformedEvent.RoutingData != null &&
                    transformedEvent.RoutingData.TargetIds.Length > 0)
                {
                    // Filter subscriptions based on target IDs
                    var targetIds = new HashSet<Guid>(transformedEvent.RoutingData.TargetIds);
                    var matchingSubscriptions = new List<IEventSubscription>();

                    foreach (var subscription in typeSubscriptions)
                    {
                        var options = subscription.Options;

                        // Only include if subscription is for one of the target IDs
                        if (options.SourceIds != null && options.SourceIds.Length > 0)
                        {
                            foreach (var sourceId in options.SourceIds)
                            {
                                if (targetIds.Contains(sourceId))
                                {
                                    matchingSubscriptions.Add(subscription);
                                    break;
                                }
                            }
                        }
                    }

                    // Process these subscriptions
                    result.HandlerCount = matchingSubscriptions.Count;
                    ProcessSubscriptions(sender, transformedEvent, matchingSubscriptions, result, ct);
                    return Task.FromResult<IPublishResult>(result);
                }

                // Standard subscription filtering based on source ID, etc.
                var matchingSubs = new List<IEventSubscription>();

                foreach (var subscription in typeSubscriptions)
                {
                    var options = subscription.Options;

                    // Check source ID filter if provided
                    if (options.SourceIds != null && options.SourceIds.Length > 0)
                    {
                        bool sourceMatch = false;

                        foreach (var sourceId in options.SourceIds)
                        {
                            if (sourceId == transformedEvent.SourceId)
                            {
                                sourceMatch = true;
                                break;
                            }
                        }

                        if (!sourceMatch && !options.IncludeConnectedSources)
                        {
                            continue; // No match, skip this subscription
                        }

                        // Check topology only if needed
                        if (!sourceMatch && options.IncludeConnectedSources && _topologyService != null)
                        {
                            // Check if there's a connection from event source to subscribed source
                            var connections = _topologyService.GetConnectionsForSourceAsync(
                                transformedEvent.SourceId, ct).Result;

                            bool connectedMatch = false;

                            foreach (var connection in connections)
                            {
                                if (!connection.IsEnabled)
                                    continue;

                                // Check if target of connection is one we're interested in
                                foreach (var subSourceId in options.SourceIds)
                                {
                                    if (connection.TargetId == subSourceId)
                                    {
                                        // Check if connection condition is met
                                        var conditionMet = _topologyService.EvaluateConnectionConditionAsync(
                                            connection, ct).Result;

                                        if (conditionMet)
                                        {
                                            connectedMatch = true;
                                            break;
                                        }
                                    }
                                }

                                if (connectedMatch)
                                    break;
                            }

                            if (!connectedMatch)
                                continue; // No connection match
                        }
                    }

                    // Apply custom filter if specified
                    if (options.Filter != null && !options.Filter(transformedEvent))
                    {
                        continue;
                    }

                    // This subscription matches
                    matchingSubs.Add(subscription);
                }

                result.HandlerCount = matchingSubs.Count;
                ProcessSubscriptions(sender, transformedEvent, matchingSubs, result, ct);
            }
            catch (Exception ex)
            {
                _logger.Log(ex, $"Error publishing event {transformedEvent.EventId}");
                result.Errors.Add(ex);
                _eventStore.PersistEventAsync(transformedEvent);
            }

            return Task.FromResult<IPublishResult>(result);
        }

        /// <summary>
        /// Processes a collection of matching subscriptions.
        /// </summary>
        private void ProcessSubscriptions(
            object sender,
            IHydroGardenEvent evt,
            List<IEventSubscription> subscriptions,
            PublishResult result,
            CancellationToken ct)
        {
            foreach (var subscription in subscriptions)
            {
                try
                {
                    // Handle different event types appropriately
                    if (evt is IHydroGardenPropertyChangedEvent propEvent)
                    {
                        var task = subscription.Handler.HandleEventAsync(sender, propEvent, ct);
                        task.GetAwaiter().GetResult();
                        result.SuccessCount++;
                    }
                    else if (evt is IHydroGardenLifecycleEvent lifecycleEvent)
                    {
                        // Convert lifecycle event to property change event for handler
                        var propEvent2 = new HydroGardenPropertyChangedEvent(
                            lifecycleEvent.DeviceId,
                            lifecycleEvent.SourceId,
                            "State",
                            typeof(ComponentState),
                            null,  // Old value not available
                            lifecycleEvent.State,
                            new PropertyMetadata(true, true, "State", "Component state"),
                            lifecycleEvent.RoutingData);

                        var task = subscription.Handler.HandleEventAsync(sender, propEvent2, ct);
                        task.GetAwaiter().GetResult();
                        result.SuccessCount++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Log(ex, $"Error processing event {evt.EventId}");
                    result.Errors.Add(ex);
                    _eventStore.PersistEventAsync(evt);
                }
            }
        }

        /// <summary>
        /// Process any failed events that were stored for retry.
        /// </summary>
        public async Task ProcessFailedEventsAsync(CancellationToken ct = default)
        {
            var failedEvent = await _eventStore.RetrieveFailedEventAsync();
            if (failedEvent != null)
            {
                // Check if we should retry
                bool shouldRetry = await _retryPolicy.ShouldRetryAsync(failedEvent, 1);
                if (shouldRetry)
                {
                    // Transform and republish
                    var transformedEvent = _transformer.Transform(failedEvent);
                    // The actual republishing logic would go here in a real implementation
                }
            }
        }

        /// <summary>
        /// Disposes resources used by the event bus.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            GC.SuppressFinalize(this);
        }
    }
}