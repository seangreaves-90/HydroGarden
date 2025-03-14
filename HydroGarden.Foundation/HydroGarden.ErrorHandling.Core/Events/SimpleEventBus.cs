using HydroGarden.Foundation.Abstractions.Interfaces;
using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Foundation.Abstractions.Interfaces.Services;
using HydroGarden.Logger.Abstractions;

namespace HydroGarden.Foundation.ErrorHandling.Events
{
    /// <summary>
    /// A simplified implementation of IEventBus for testing and basic usage.
    /// </summary>
    public class SimpleEventBus : IEventBus
    {
        private readonly ILogger _logger;
        private readonly Dictionary<Guid, Subscription> _subscriptions = new();
        private ITopologyService? _topologyService;

        /// <summary>
        /// Initializes a new instance of the <see cref="SimpleEventBus"/> class.
        /// </summary>
        /// <param name="logger">The logger to use.</param>
        public SimpleEventBus(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public void SetTopologyService(ITopologyService topologyService)
        {
            _topologyService = topologyService;
        }

        /// <inheritdoc/>
        public Guid Subscribe<T>(T handler, IEventSubscriptionOptions? options = null) where T : IEventHandler
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            var subscriptionId = Guid.NewGuid();
            var subscription = new Subscription
            {
                Handler = handler,
                Options = options ?? new EventSubscriptionOptions(),
                Id = subscriptionId
            };

            lock (_subscriptions)
            {
                _subscriptions[subscriptionId] = subscription;
            }

            _logger.Log($"Added subscription {subscriptionId} for handler {handler.GetType().Name}");
            return subscriptionId;
        }

        /// <inheritdoc/>
        public bool Unsubscribe(Guid subscriptionId)
        {
            lock (_subscriptions)
            {
                if (_subscriptions.Remove(subscriptionId))
                {
                    _logger.Log($"Removed subscription {subscriptionId}");
                    return true;
                }
            }

            _logger.Log($"Subscription {subscriptionId} not found for removal");
            return false;
        }

        /// <inheritdoc/>
        public async Task<IPublishResult?> PublishAsync(object? sender, IEvent evt, CancellationToken ct = default)
        {
            if (evt == null)
                throw new ArgumentNullException(nameof(evt));

            try
            {
                _logger.Log($"Publishing event {evt.EventId} of type {evt.EventType}");

                // Create a result to track handlers
                var result = new PublishResult
                {
                    EventId = evt.EventId,
                    HandlerCount = 0,
                    SuccessCount = 0
                };

                // Find matching subscriptions
                var matchingSubscriptions = GetMatchingSubscriptions(evt);
                result.HandlerCount = matchingSubscriptions.Count;

                if (matchingSubscriptions.Count == 0)
                {
                    _logger.Log($"No handlers found for event {evt.EventId}");
                    return result;
                }

                // Process event with each matching subscription
                var tasks = new List<Task>();

                foreach (var subscription in matchingSubscriptions)
                {
                    // If sync processing is requested, handle immediately
                    if (subscription.Options.Synchronous)
                    {
                        try
                        {
                            await subscription.Handler.HandleEventAsync(sender, evt, ct);
                            result.SuccessCount++;
                        }
                        catch (Exception ex)
                        {
                            _logger.Log(ex, $"Error handling event {evt.EventId} in handler {subscription.Handler.GetType().Name}");
                            result.Errors.Add(ex);
                        }
                    }
                    else
                    {
                        // Otherwise process asynchronously
                        var task = HandleEventAsyncWithErrorCapture(sender, evt, subscription, result, ct);
                        tasks.Add(task);
                        result.HandlerTasks.Add(task);
                    }
                }

                // Wait for all async handlers if a timeout is specified
                if (tasks.Count > 0 && evt.RoutingData?.Timeout != null)
                {
                    var timeout = evt.RoutingData.Timeout.Value;
                    var completedTask = await Task.WhenAny(
                        Task.WhenAll(tasks),
                        Task.Delay(timeout, ct)
                    );

                    if (completedTask != Task.WhenAll(tasks))
                    {
                        _logger.Log($"Event {evt.EventId} processing timed out after {timeout}");
                        result.TimedOut = true;
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.Log(ex, $"Error publishing event {evt.EventId}");
                return PublishResult.Failure(evt.EventId, ex);
            }
        }

        private List<Subscription> GetMatchingSubscriptions(IEvent evt)
        {
            var result = new List<Subscription>();
            
            lock (_subscriptions)
            {
                foreach (var subscription in _subscriptions.Values)
                {
                    if (MatchesSubscription(evt, subscription.Options))
                    {
                        result.Add(subscription);
                    }
                }
            }
            
            return result;
        }

        private bool MatchesSubscription(IEvent evt, IEventSubscriptionOptions options)
        {
            // Check event type filter
            if (options.EventTypes != null && options.EventTypes.Length > 0)
            {
                if (!options.EventTypes.Contains(evt.EventType))
                    return false;
            }

            // Check source filter
            if (options.SourceIds != null && options.SourceIds.Length > 0)
            {
                if (!options.SourceIds.Contains(evt.SourceId))
                {
                    // If we should include connected sources, check topology
                    if (options.IncludeConnectedSources && _topologyService != null)
                    {
                        var isConnected = false;
                        foreach (var sourceId in options.SourceIds)
                        {
                            if (_topologyService.AreDevicesConnected(sourceId, evt.SourceId))
                            {
                                isConnected = true;
                                break;
                            }
                        }

                        if (!isConnected)
                            return false;
                    }
                    else
                    {
                        return false;
                    }
                }
            }

            // Check custom filter
            if (options.Filter != null && !options.Filter(evt))
                return false;

            return true;
        }

        private async Task HandleEventAsyncWithErrorCapture(
            object? sender,
            IEvent evt,
            Subscription subscription,
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
                _logger.Log(ex, $"Error handling event {evt.EventId} in handler {subscription.Handler.GetType().Name}");

                lock (result)
                {
                    result.Errors.Add(ex);
                }
            }
        }

        private class Subscription
        {
            public Guid Id { get; set; }
            public IEventHandler Handler { get; set; } = null!;
            public IEventSubscriptionOptions Options { get; set; } = null!;
        }
    }
}