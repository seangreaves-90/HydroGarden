using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Foundation.Abstractions.Interfaces.Events.Routing;
using HydroGarden.Foundation.Abstractions.Interfaces.Services;
using HydroGarden.Logger.Abstractions;

namespace HydroGarden.Foundation.Common.Events.Routing
{
    /// <summary>
    /// An event router that matches events based on topology connections between components.
    /// </summary>
    /// <remarks>
    /// This router uses the topology service to determine if components are connected,
    /// allowing for more advanced routing decisions.
    /// </remarks>
    /// <remarks>
    /// Initializes a new instance of the <see cref="TopologyEventRouter"/> class.
    /// </remarks>
    /// <param name="logger">The logger to use.</param>
    /// <param name="topologyService">The topology service to use for routing decisions.</param>
    /// <param name="fallbackRouter">The fallback router to use when topology routing is not applicable.</param>
    public class TopologyEventRouter(
        ILogger logger,
        ITopologyService topologyService,
        IEventRouter fallbackRouter) : BaseEventRouter(logger)
    {
        private readonly ITopologyService _topologyService = topologyService ?? throw new ArgumentNullException(nameof(topologyService));
        private readonly IEventRouter _fallbackRouter = fallbackRouter ?? throw new ArgumentNullException(nameof(fallbackRouter));

        /// <inheritdoc/>
        protected override async Task<bool> MatchesSubscriptionCoreAsync(
            IEvent @event,
            IEventSubscription subscription,
            CancellationToken ct = default)
        {
            // Check if the event has explicit target IDs
            if (@event.RoutingData?.TargetIds.Count > 0)
            {
                // Use fallback router for targeted events - direct matching logic
                return await _fallbackRouter.MatchesSubscriptionAsync(@event, subscription, ct);
            }

            // No explicit targets, check source ID filter
            if (subscription.Options.SourceIds.Length > 0)
            {
                // Check direct match first
                bool directSourceMatch = subscription.Options.SourceIds.Contains(@event.SourceId);
                if (directSourceMatch)
                {
                    return true;
                }

                // If the subscription wants connected sources, check topology
                if (subscription.Options.IncludeConnectedSources)
                {
                    try
                    {
                        return await CheckTopologyConnectionAsync(@event.SourceId, subscription.Options.SourceIds, ct);
                    }
                    catch (Exception ex)
                    {
                        Logger.Log(ex, $"Error checking topology connection for event {{{@event.EventId}}}");
                        return false;
                    }
                }
                
                // Not a direct match and not including connected sources
                return false;
            }

            // No source filters, so the subscription matches
            return true;
        }

        /// <summary>
        /// Checks if the source is connected to any of the target IDs via the topology.
        /// </summary>
        /// <param name="sourceId">The source ID to check.</param>
        /// <param name="targetIds">The target IDs to check against.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>True if a connection exists, false otherwise.</returns>
        private async Task<bool> CheckTopologyConnectionAsync(
            Guid sourceId,
            Guid[] targetIds,
            CancellationToken ct)
        {
            foreach (var targetId in targetIds)
            {
                try
                {
                    if (sourceId == targetId)
                    {
                        return true; // Self-connection is always true
                    }

                    // Check for direct connection
                    var connections = await _topologyService.GetConnectionsForSourceAsync(sourceId, ct);
                    foreach (var connection in connections)
                    {
                        if (!connection.IsEnabled)
                            continue;

                        if (connection.TargetId == targetId)
                        {
                            // Evaluate condition if present
                            if (string.IsNullOrEmpty(connection.Condition) || 
                                await _topologyService.EvaluateConnectionConditionAsync(connection, ct))
                            {
                                return true;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log(ex, $"Error checking topology connection between {sourceId} and {targetId}");
                }
            }

            return false;
        }
    }
}