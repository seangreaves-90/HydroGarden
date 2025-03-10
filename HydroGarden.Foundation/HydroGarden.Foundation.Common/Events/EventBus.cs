using HydroGarden.Foundation.Abstractions.Interfaces;
using HydroGarden.Foundation.Abstractions.Interfaces.Components;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Foundation.Abstractions.Interfaces.Services;
using HydroGarden.Foundation.Common.Extensions;
using HydroGarden.Foundation.Common.QueueProcessor;
using HydroGarden.Foundation.Common.Results;
using System.Collections.Concurrent;
using HydroGarden.Logger.Abstractions;

namespace HydroGarden.Foundation.Common.Events
{
    /// <summary>
    /// Implements a robust EventBus with support for:
    /// - Event prioritization and parallel processing.
    /// - Retry policies for failed event handling.
    /// - Event transformation before delivery.
    /// - Dead-letter queue for failed events.
    /// - Dynamic runtime subscription management.
    /// - Event correlation for tracking event chains.
    /// - Topology-aware routing for component integration.
    /// </summary>
    public class EventBus : IEventBus, IDisposable
    {
        private ITopologyService? _topologyService;
        private readonly ILogger _logger;
        private readonly IEventStore _eventStore;
        private readonly IEventRetryPolicy _retryPolicy;
        private readonly IEventTransformer _transformer;
        private readonly IErrorMonitor _errorMonitor;
        private readonly EventQueueProcessor _eventQueueProcessor;
        private readonly ConcurrentDictionary<Guid, IEventSubscription> _subscriptions = new();
        private readonly ConcurrentDictionary<EventType, List<IEventSubscription>> _subscriptionsByType = new();
        private bool _isDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventBus"/> class.
        /// </summary>
        public EventBus(
            ILogger logger,
            IEventStore eventStore,
            IEventRetryPolicy retryPolicy,
            IEventTransformer transformer,
            IErrorMonitor errorMonitor,
            int maxConcurrentProcessing = 4)
        {
            _logger = logger;
            _eventStore = eventStore;
            _retryPolicy = retryPolicy;
            _transformer = transformer;
            _eventQueueProcessor = new EventQueueProcessor(logger, maxConcurrentProcessing);
            _errorMonitor = errorMonitor;
        }

        /// <summary>
        /// Sets the topology service for the EventBus.
        /// This allows routing events based on component relationships.
        /// </summary>
        /// <param name="topologyService">The topology service to use.</param>
        public void SetTopologyService(ITopologyService topologyService)
        {
            _topologyService = topologyService ?? throw new ArgumentNullException(nameof(topologyService));
        }

        /// <summary>
        /// Subscribes an event handler to the bus.
        /// </summary>
        public Guid Subscribe<T>(T handler, IEventSubscriptionOptions? options = null) where T : IEventHandler
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

                _logger.Log(this, $"Handler with ID {subscriptionId} unsubscribed");
                return true;
            }
            return false;
        }

        /// <summary>
        /// Publishes an event to the event bus.
        /// </summary>
        public async Task<IPublishResult?> PublishAsync(object sender, IEvent evt, CancellationToken ct = default)
        {
            return await this.ExecuteWithErrorHandlingAsync(
                _errorMonitor,
                async () =>
                {
                    if (sender == null) throw new ArgumentNullException(nameof(sender));
                    if (evt == null) throw new ArgumentNullException(nameof(evt));

                    var transformedEvent = _transformer.Transform(evt);
                    var result = new PublishResult { EventId = evt.EventId };

                    var matchingSubscriptions = await GetMatchingSubscriptionsAsync(transformedEvent, ct);
                    result.HandlerCount = matchingSubscriptions.Count;

                    if (matchingSubscriptions.Count == 0)
                    {
                        if (evt.RoutingData?.Persist == true)
                        {
                            await _eventStore.PersistEventAsync(evt);
                        }
                        return result;
                    }

                    var syncSubscriptions = matchingSubscriptions.Where(s => s.Options.Synchronous).ToList();
                    var asyncSubscriptions = matchingSubscriptions.Where(s => !s.Options.Synchronous).ToList();

                    foreach (var subscription in syncSubscriptions)
                    {
                        try
                        {
                            await subscription.Handler.HandleEventAsync(sender, transformedEvent, ct);
                            result.SuccessCount++;
                        }
                        catch (Exception ex)
                        {
                            await _errorMonitor.ReportExceptionAsync(
                                this,
                                ex,
                                "EVENT_HANDLER_FAILED",
                                $"Error in synchronous handler for event {transformedEvent.EventId}",
                                ErrorSeverity.Error,
                                ErrorSource.Service,
                                new Dictionary<string, object>
                                {
                                    ["EventId"] = transformedEvent.EventId,
                                    ["EventType"] = transformedEvent.EventType.ToString(),
                                    ["SubscriptionId"] = subscription.Id
                                });

                            result.Errors.Add(ex);
                            await _eventStore.PersistEventAsync(transformedEvent);
                        }
                    }

                    // Process async subscriptions...

                    return result;
                },
                "EVENT_PUBLISHING_FAILED",
                $"Failed to publish event {evt.EventId}",
                ErrorSource.Service,
                new Dictionary<string, object>
                {
                    ["EventType"] = evt.EventType.ToString(),
                    ["SourceId"] = evt.SourceId
                }, ct: ct);
        }

        /// <summary>
        /// Gets all subscriptions that match the given event based on event type, source ID,
        /// and topology relationships.
        /// </summary>
        private async Task<List<IEventSubscription>> GetMatchingSubscriptionsAsync(
            IEvent evt,
            CancellationToken ct = default)
        {
            var matchingSubs = new List<IEventSubscription>();

            // Check if we have any subscriptions for this event type
            if (!_subscriptionsByType.TryGetValue(evt.EventType, out var typeSubscriptions))
            {
                return matchingSubs;
            }

            // Check for explicit target routing
            if (evt.RoutingData != null && evt.RoutingData.TargetIds.Length > 0)
            {
                // Filter subscriptions based on target IDs
                var targetIds = new HashSet<Guid>(evt.RoutingData.TargetIds);

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
                                matchingSubs.Add(subscription);
                                break;
                            }
                        }
                    }
                }

                return matchingSubs;
            }

            // Standard subscription filtering based on source ID, etc.
            foreach (var subscription in typeSubscriptions)
            {
                var options = subscription.Options;

                // Check source ID filter if provided
                if (options.SourceIds != null && options.SourceIds.Length > 0)
                {
                    bool sourceMatch = false;

                    foreach (var sourceId in options.SourceIds)
                    {
                        if (sourceId == evt.SourceId)
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
                        var connections = await _topologyService.GetConnectionsForSourceAsync(
                            evt.SourceId, ct);

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
                                    var conditionMet = await _topologyService.EvaluateConnectionConditionAsync(
                                        connection, ct);

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
                if (options.Filter != null && !options.Filter(evt))
                {
                    continue;
                }

                // This subscription matches
                matchingSubs.Add(subscription);
            }

            return matchingSubs;
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
                    // CHANGE: Don't transform here, PublishAsync will handle it
                    await PublishAsync(this, failedEvent, ct);
                }
            }
        }

        /// <summary>
        /// Disposes the event bus, ensuring all resources are properly released.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            _eventQueueProcessor.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}