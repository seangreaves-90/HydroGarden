using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HydroGarden.Foundation.Abstractions.Interfaces.Events.Routing
{
    /// <summary>
    /// Interface for event routing services that determine which subscriptions should receive an event.
    /// </summary>
    /// <remarks>
    /// The EventRouter is responsible for all subscription matching and routing decisions,
    /// allowing the EventBus to focus solely on subscription management and message delivery.
    /// </remarks>
    public interface IEventRouter
    {
        /// <summary>
        /// Determines which subscriptions match an event based on routing rules.
        /// </summary>
        /// <param name="event">The event to be routed.</param>
        /// <param name="availableSubscriptions">The collection of all available subscriptions.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A read-only list of matching subscriptions.</returns>
        Task<IReadOnlyList<IEventSubscription>> GetMatchingSubscriptionsAsync(
            IEvent @event, 
            IEnumerable<IEventSubscription> availableSubscriptions,
            CancellationToken ct = default);
        
        /// <summary>
        /// Determines if a specific subscription matches an event.
        /// </summary>
        /// <param name="event">The event to check.</param>
        /// <param name="subscription">The subscription to check against.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>True if the subscription matches the event, false otherwise.</returns>
        Task<bool> MatchesSubscriptionAsync(
            IEvent @event,
            IEventSubscription subscription,
            CancellationToken ct = default);
    }
}