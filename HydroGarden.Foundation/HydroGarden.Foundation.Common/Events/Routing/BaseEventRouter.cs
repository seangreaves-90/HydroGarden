using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Foundation.Abstractions.Interfaces.Events.Routing;
using HydroGarden.Logger.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HydroGarden.Foundation.Common.Events.Routing
{
    /// <summary>
    /// Base implementation of an event router that provides common functionality.
    /// </summary>
    /// <remarks>
    /// This class provides core functionality that all routers should have, including
    /// basic filtering by event type and custom filters.
    /// </remarks>
    public abstract class BaseEventRouter : IEventRouter
    {
        /// <summary>
        /// Logger for event routing operations.
        /// </summary>
        protected readonly ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="BaseEventRouter"/> class.
        /// </summary>
        /// <param name="logger">The logger to use.</param>
        protected BaseEventRouter(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<IEventSubscription>> GetMatchingSubscriptionsAsync(
            IEvent @event, 
            IEnumerable<IEventSubscription> availableSubscriptions,
            CancellationToken ct = default)
        {
            if (@event == null)
                throw new ArgumentNullException(nameof(@event));
            
            if (availableSubscriptions == null)
                throw new ArgumentNullException(nameof(availableSubscriptions));

            var result = new List<IEventSubscription>();

            foreach (var subscription in availableSubscriptions)
            {
                if (ct.IsCancellationRequested)
                    break;

                bool matches = await MatchesSubscriptionAsync(@event, subscription, ct);
                if (matches)
                {
                    result.Add(subscription);
                }
            }

            return result;
        }

        /// <inheritdoc/>
        public virtual async Task<bool> MatchesSubscriptionAsync(
            IEvent @event,
            IEventSubscription subscription,
            CancellationToken ct = default)
        {
            if (@event == null)
                throw new ArgumentNullException(nameof(@event));
            
            if (subscription == null)
                throw new ArgumentNullException(nameof(subscription));

            // Check event type filter
            if (subscription.Options.EventTypes.Length > 0 && 
                !subscription.Options.EventTypes.Contains(@event.EventType))
            {
                return false;
            }

            // Apply custom filter if specified
            if (subscription.Options.Filter != null && !subscription.Options.Filter(@event))
            {
                return false;
            }

            // Allow derived classes to implement their specific matching logic
            return await MatchesSubscriptionCoreAsync(@event, subscription, ct);
        }

        /// <summary>
        /// Core matching logic to be implemented by derived router classes.
        /// </summary>
        /// <param name="event">The event to check.</param>
        /// <param name="subscription">The subscription to check.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>True if the subscription matches based on specific router criteria.</returns>
        protected abstract Task<bool> MatchesSubscriptionCoreAsync(
            IEvent @event,
            IEventSubscription subscription,
            CancellationToken ct = default);
    }
}