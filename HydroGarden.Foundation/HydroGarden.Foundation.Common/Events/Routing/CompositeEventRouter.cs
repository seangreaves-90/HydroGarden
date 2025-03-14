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
    /// A composite router that combines multiple other routers for complex routing scenarios.
    /// </summary>
    /// <remarks>
    /// This router delegates to multiple child routers and then combines their results
    /// based on the strategy specified (Any, All, or First).
    /// </remarks>
    public class CompositeEventRouter : IEventRouter
    {
        /// <summary>
        /// Defines the matching strategy for combining results from multiple routers.
        /// </summary>
        public enum MatchingStrategy
        {
            /// <summary>
            /// A subscription matches if any router matches it.
            /// </summary>
            Any,
            
            /// <summary>
            /// A subscription matches only if all routers match it.
            /// </summary>
            All,
            
            /// <summary>
            /// Uses only the first router that returns matches.
            /// </summary>
            First
        }

        private readonly ILogger _logger;
        private readonly IList<IEventRouter> _routers;
        private readonly MatchingStrategy _strategy;

        /// <summary>
        /// Initializes a new instance of the <see cref="CompositeEventRouter"/> class.
        /// </summary>
        /// <param name="logger">The logger to use.</param>
        /// <param name="routers">The list of child routers to use.</param>
        /// <param name="strategy">The strategy to use for combining results.</param>
        public CompositeEventRouter(
            ILogger logger,
            IList<IEventRouter> routers,
            MatchingStrategy strategy = MatchingStrategy.Any)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _routers = routers ?? throw new ArgumentNullException(nameof(routers));
            
            if (!routers.Any())
                throw new ArgumentException("At least one router must be provided", nameof(routers));
            
            _strategy = strategy;
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

            var subscriptionList = availableSubscriptions.ToList();
            
            switch (_strategy)
            {
                case MatchingStrategy.First:
                    return await GetFirstMatchingSubscriptionsAsync(@event, subscriptionList, ct);
                
                case MatchingStrategy.All:
                    return await GetAllRequiredMatchingSubscriptionsAsync(@event, subscriptionList, ct);
                
                case MatchingStrategy.Any:
                default:
                    return await GetAnyMatchingSubscriptionsAsync(@event, subscriptionList, ct);
            }
        }

        /// <inheritdoc/>
        public async Task<bool> MatchesSubscriptionAsync(
            IEvent @event,
            IEventSubscription subscription,
            CancellationToken ct = default)
        {
            if (@event == null)
                throw new ArgumentNullException(nameof(@event));
            
            if (subscription == null)
                throw new ArgumentNullException(nameof(subscription));

            var matchResults = new List<bool>();
            
            foreach (var router in _routers)
            {
                if (ct.IsCancellationRequested)
                    break;

                try
                {
                    bool matches = await router.MatchesSubscriptionAsync(@event, subscription, ct);
                    matchResults.Add(matches);
                    
                    // For First strategy, return on first match
                    if (_strategy == MatchingStrategy.First && matches)
                        return true;
                    
                    // For All strategy, return false on first non-match
                    if (_strategy == MatchingStrategy.All && !matches)
                        return false;
                }
                catch (Exception ex)
                {
                    _logger.Log(ex, $"Error in router when checking subscription match for event {{{@event.EventId}}}");
                }
            }

            // For Any strategy, return true if any matched
            if (_strategy == MatchingStrategy.Any)
                return matchResults.Any(x => x);
            
            // For All strategy, all routers must match (if we get here, they all matched)
            if (_strategy == MatchingStrategy.All)
                return matchResults.All(x => x);
            
            // For First strategy, if we get here, no router matched
            return false;
        }

        private async Task<IReadOnlyList<IEventSubscription>> GetFirstMatchingSubscriptionsAsync(
            IEvent @event,
            IList<IEventSubscription> subscriptions,
            CancellationToken ct)
        {
            foreach (var router in _routers)
            {
                if (ct.IsCancellationRequested)
                    break;

                try
                {
                    var matches = await router.GetMatchingSubscriptionsAsync(@event, subscriptions, ct);
                    if (matches.Any())
                    {
                        return matches;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Log(ex, $"Error in router when getting matching subscriptions for event {{{@event.EventId}}}");
                }
            }

            return Array.Empty<IEventSubscription>();
        }

        private async Task<IReadOnlyList<IEventSubscription>> GetAllRequiredMatchingSubscriptionsAsync(
            IEvent @event,
            IList<IEventSubscription> subscriptions,
            CancellationToken ct)
        {
            var result = new HashSet<IEventSubscription>();
            bool isFirstRouter = true;

            foreach (var router in _routers)
            {
                if (ct.IsCancellationRequested)
                    break;

                try
                {
                    var matches = await router.GetMatchingSubscriptionsAsync(@event, subscriptions, ct);
                    
                    if (isFirstRouter)
                    {
                        // Initialize with first router's matches
                        foreach (var match in matches)
                        {
                            result.Add(match);
                        }
                        isFirstRouter = false;
                    }
                    else
                    {
                        // Keep only subscriptions that match in all routers
                        result.IntersectWith(matches);
                    }

                    // If no common matches left, we can exit early
                    if (!result.Any())
                    {
                        break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Log(ex, $"Error in router when getting matching subscriptions for event {{{@event.EventId}}}");
                }
            }

            return result.ToList();
        }

        private async Task<IReadOnlyList<IEventSubscription>> GetAnyMatchingSubscriptionsAsync(
            IEvent @event,
            IList<IEventSubscription> subscriptions,
            CancellationToken ct)
        {
            var result = new HashSet<IEventSubscription>();

            foreach (var router in _routers)
            {
                if (ct.IsCancellationRequested)
                    break;

                try
                {
                    var matches = await router.GetMatchingSubscriptionsAsync(@event, subscriptions, ct);
                    foreach (var match in matches)
                    {
                        result.Add(match);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Log(ex, $"Error in router when getting matching subscriptions for event {{{@event.EventId}}}");
                }
            }

            return result.ToList();
        }
    }
}