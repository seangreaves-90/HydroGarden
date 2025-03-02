using HydroGarden.Foundation.Abstractions.Interfaces;
using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Foundation.Abstractions.Interfaces.Logging;
using HydroGarden.Foundation.Abstractions.Interfaces.Services;
using HydroGarden.Foundation.Common.QueueProcessor;
using HydroGarden.Foundation.Common.Results;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;

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
    /// </summary>
    public class EventBus : IEventBus, IDisposable
    {
        private readonly IHydroGardenLogger _logger;
        private readonly IEventStore _eventStore;
        private readonly IEventRetryPolicy _retryPolicy;
        private readonly IEventTransformer _transformer;
        private readonly EventQueueProcessor _eventQueueProcessor;
        private readonly ConcurrentDictionary<Guid, IEventSubscription> _subscriptions = new();
        private readonly ConcurrentDictionary<EventType, List<IEventSubscription>> _subscriptionsByType = new();
        private bool _isDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventBus"/> class.
        /// </summary>
        public EventBus(
            IHydroGardenLogger logger,
            IEventStore eventStore,
            IEventRetryPolicy retryPolicy,
            IEventTransformer transformer,
            int maxConcurrentProcessing = 4)
        {
            _logger = logger;
            _eventStore = eventStore;
            _retryPolicy = retryPolicy;
            _transformer = transformer;
            _eventQueueProcessor = new EventQueueProcessor(logger, maxConcurrentProcessing);
        }

        /// <summary>
        /// Subscribes an event handler to the bus.
        /// </summary>
        public Guid Subscribe(IHydroGardenPropertyChangedEventHandler handler, IEventSubscriptionOptions? options = null)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            var subscription = new EventSubscription(Guid.NewGuid(), handler, options ?? new EventSubscriptionOptions());
            _subscriptions[subscription.Id] = subscription;

            foreach (var eventType in subscription.Options.EventTypes)
            {
                _subscriptionsByType.AddOrUpdate(eventType, _ => new List<IEventSubscription> { subscription }, (_, list) =>
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
        public async Task<IPublishResult> PublishAsync(object sender, IHydroGardenEvent evt, CancellationToken ct = default)
        {
            if (sender == null) throw new ArgumentNullException(nameof(sender));
            if (evt == null) throw new ArgumentNullException(nameof(evt));

            var transformedEvent = _transformer.Transform(evt);
            var result = new PublishResult { EventId = evt.EventId };

            try
            {
                foreach (var subscription in _subscriptions.Values)
                {
                    _eventQueueProcessor.Enqueue(new EventQueueItem
                    {
                        Sender = sender,
                        Event = transformedEvent,
                        Subscription = subscription,
                        Result = result,
                        CompletionSource = new TaskCompletionSource<bool>()
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.Log(ex, $"[EventBus] Event {evt.EventId} failed.");
                await _eventStore.PersistEventAsync(evt);
            }

            return result;
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
