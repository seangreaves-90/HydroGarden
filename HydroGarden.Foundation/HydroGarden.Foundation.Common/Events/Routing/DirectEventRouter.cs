using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Logger.Abstractions;

namespace HydroGarden.Foundation.Common.Events.Routing
{
    /// <summary>
    /// A simple event router that matches events based on direct criteria without topology information.
    /// </summary>
    /// <remarks>
    /// This router handles basic routing based on event type and source ID matching.
    /// It does not use topology information for connected components.
    /// </remarks>
    public class DirectEventRouter : BaseEventRouter
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DirectEventRouter"/> class.
        /// </summary>
        /// <param name="logger">The logger to use.</param>
        public DirectEventRouter(ILogger logger) : base(logger)
        {
        }

        /// <inheritdoc/>
        protected override Task<bool> MatchesSubscriptionCoreAsync(
            IEvent @event,
            IEventSubscription subscription,
            CancellationToken ct = default)
        {
            // Check if the event has explicit target IDs
            if (@event.RoutingData?.TargetIds.Length > 0)
            {
                // Check if any subscription source ID matches a target
                if (subscription.Options.SourceIds.Length > 0)
                {
                    bool hasMatchingTarget = subscription.Options.SourceIds
                        .Any(sourceId => @event.RoutingData.TargetIds.Contains(sourceId));

                    return Task.FromResult(hasMatchingTarget);
                }

                // No source IDs specified in subscription, so no match with targeted event
                return Task.FromResult(false);
            }

            // No explicit targets, check source ID filter
            if (subscription.Options.SourceIds.Length > 0)
            {
                // Must have a direct source ID match since this router doesn't use topology
                bool sourceMatch = subscription.Options.SourceIds.Contains(@event.SourceId);
                return Task.FromResult(sourceMatch);
            }

            // No source filters, so the subscription matches
            return Task.FromResult(true);
        }
    }
}