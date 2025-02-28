using System.Collections.Concurrent;
using HydroGarden.Foundation.Abstractions.Interfaces;
using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Foundation.Abstractions.Interfaces.Logging;
using HydroGarden.Foundation.Common.Events;
using HydroGarden.Foundation.Common.Extensions;
using HydroGarden.Foundation.Common.Results;

namespace HydroGarden.Foundation.Core.Events
{

    /// <summary>
    /// Central event bus for publishing and subscribing to events
    /// </summary>
    public class EventBus : IEventBus, IDisposable
    {
        private readonly IHydroGardenLogger _logger;
        private readonly ITopologyService? _topologyService;
        private readonly ConcurrentDictionary<Guid, EventSubscription> _subscriptions = new();
        private readonly SemaphoreSlim _publishLock = new(1, 1);
        private int _isDisposed;

        /// <summary>
        /// Creates a new instance of the EventBus
        /// </summary>
        /// <param name="logger">Logger for recording events and errors</param>
        /// <param name="topologyService">Optional topology service for connection-based routing</param>
        public EventBus(IHydroGardenLogger logger, ITopologyService? topologyService = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _topologyService = topologyService;
        }

        /// <inheritdoc />
        public Guid Subscribe(IHydroGardenEventHandler handler, IEventSubscriptionOptions? options = null)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            options ??= new EventSubscriptionOptions();
            var subscription = new EventSubscription(Guid.NewGuid(), handler, options);
            _subscriptions[subscription.Id] = subscription;

            _logger.Log($"Event subscription {subscription.Id} registered for {options.EventTypes.Length} event types");
            return subscription.Id;
        }

        /// <inheritdoc />
        public bool Unsubscribe(Guid subscriptionId)
        {
            var result = _subscriptions.TryRemove(subscriptionId, out _);
            if (result)
            {
                _logger.Log($"Event subscription {subscriptionId} removed");
            }
            return result;
        }

        /// <inheritdoc />
        public async Task<IPublishResult> PublishAsync(object sender, IHydroGardenEvent evt, CancellationToken ct = default)
        {
            if (evt == null)
                throw new ArgumentNullException(nameof(evt));

            if (_isDisposed != 0)
                throw new ObjectDisposedException(nameof(EventBus));

            // Apply rate limiting if we're getting too many events
            await _publishLock.WaitAsync(ct);

            try
            {
                _logger.Log($"Publishing event {evt.EventId} of type {evt.EventType} from source {evt.DeviceId}");

                IPublishResult result = new PublishResult
                {
                    EventId = evt.EventId,
                    HandlerCount = 0,
                    SuccessCount = 0,
                    Errors = new List<Exception>()
                };

                var tasks = new List<Task>();
                var directTargets = new HashSet<Guid>();

                // Get target IDs from routing data if available
                if (evt.RoutingData != null && evt.RoutingData.TargetIds != null)
                {
                    directTargets = new HashSet<Guid>(evt.RoutingData.TargetIds);
                }

                // Get connected components from topology if available
                var connectedTargets = new HashSet<Guid>();
                if (_topologyService != null && (evt.RoutingData == null || evt.RoutingData.TargetIds == null || evt.RoutingData.TargetIds.Length == 0))
                {
                    var connections = await _topologyService.GetConnectionsForSourceAsync(evt.DeviceId, ct);
                    foreach (var connection in connections)
                    {
                        connectedTargets.Add(connection.TargetId);
                    }
                }

                // Create a list of relevant subscriptions sorted by priority
                var relevantSubscriptions = _subscriptions.Values
                    .Where(s => IsSubscriptionEligible(s, evt, directTargets, connectedTargets))
                    .OrderByDescending(s => evt.RoutingData?.Priority ?? EventPriority.Normal)
                    .ToList();

                result.HandlerCount = relevantSubscriptions.Count;

                foreach (var subscription in relevantSubscriptions)
                {
                    if (subscription.Options.Synchronous)
                    {
                        try
                        {
                            await subscription.Handler.HandleEventAsync(sender, (IHydroGardenPropertyChangedEvent)evt, ct);
                            result.SuccessCount++;
                        }
                        catch (Exception ex)
                        {
                            _logger.Log(ex, $"Error handling event {evt.EventId} in subscription {subscription.Id}");
                            result.Errors.Add(ex);
                        }
                    }
                    else
                    {
                        // Process asynchronously
                        tasks.Add(ProcessEventAsync(sender, evt, subscription, result, ct));
                    }
                }

                if (tasks.Count > 0)
                {
                    try
                    {
                        // If we have a timeout, use it
                        if (evt.RoutingData?.Timeout.HasValue == true)
                        {
                            var completedTask = await Task.WhenAny(
                                Task.WhenAll(tasks),
                                Task.Delay(evt.RoutingData.Timeout.Value, ct)
                            );

                            if (completedTask != Task.WhenAll(tasks))
                            {
                                _logger.Log($"Event {evt.EventId} processing timed out after {evt.RoutingData.Timeout.Value.TotalMilliseconds}ms");
                                result.TimedOut = true;
                            }
                        }
                        else
                        {
                            await Task.WhenAll(tasks);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Log(ex, $"Error waiting for event handlers to complete for event {evt.EventId}");
                        result.Errors.Add(ex);
                    }
                }

                _logger.Log($"Event {evt.EventId} published to {result.SuccessCount}/{result.HandlerCount} handlers");
                return (IPublishResult)result;
            }
            finally
            {
                _publishLock.Release();
            }
        }

        private async Task ProcessEventAsync(object sender, IHydroGardenEvent evt, IEventSubscription subscription, IPublishResult result, CancellationToken ct)
        {
            try
            {
                await subscription.Handler.HandleEventAsync(sender, (IHydroGardenPropertyChangedEvent)evt, ct);

                // Fix: Can't use Interlocked.Increment on a property that returns by value
                // Instead, increment and set the value directly
                int newSuccessCount = result.SuccessCount + 1;
                result.SuccessCount = newSuccessCount;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Cancellation is not an error
                throw;
            }
            catch (Exception ex)
            {
                _logger.Log(ex, $"Error handling event {evt.EventId} in subscription {subscription.Id}");
                lock (result.Errors)
                {
                    result.Errors.Add(ex);
                }
            }
        }

        private bool IsSubscriptionEligible(EventSubscription subscription, IHydroGardenEvent evt, HashSet<Guid> directTargets, HashSet<Guid> connectedTargets)
        {
            // Check for direct targeting
            if (directTargets.Count > 0 && !directTargets.Contains(subscription.Handler.GetTargetId()))
            {
                return false;
            }

            // Check event type filter
            if (subscription.Options.EventTypes.Length > 0 &&
                !subscription.Options.EventTypes.Contains(evt.EventType))
            {
                return false;
            }

            // Check source filter
            if (subscription.Options.SourceIds.Length > 0 &&
                !subscription.Options.SourceIds.Contains(evt.DeviceId))
            {
                return false;
            }

            // Check connected sources filter
            if (subscription.Options.IncludeConnectedSources &&
                !connectedTargets.Contains(subscription.Handler.GetTargetId()))
            {
                return false;
            }

            // Apply custom filter if provided
            if (subscription.Options.Filter != null && !subscription.Options.Filter(evt))
            {
                return false;
            }

            return true;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
                return;

            _publishLock.Dispose();
            _subscriptions.Clear();
        }
    }
}