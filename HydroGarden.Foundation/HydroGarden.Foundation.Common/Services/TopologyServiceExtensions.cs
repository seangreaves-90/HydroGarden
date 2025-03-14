using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Foundation.Abstractions.Interfaces.Services;

namespace HydroGarden.Foundation.Common.Services
{
    /// <summary>
    /// Extension methods for the <see cref="ITopologyService"/> interface.
    /// </summary>
    public static class TopologyServiceExtensions
    {
        /// <summary>
        /// Checks if two devices are connected in the topology.
        /// </summary>
        /// <param name="topologyService">The topology service.</param>
        /// <param name="sourceId">The source device ID.</param>
        /// <param name="targetId">The target device ID.</param>
        /// <param name="ct">Optional cancellation token.</param>
        /// <returns>True if the devices are connected, false otherwise.</returns>
        public static async Task<bool> AreDevicesConnectedAsync(
            this ITopologyService topologyService,
            Guid sourceId,
            Guid targetId,
            CancellationToken ct = default)
        {
            if (topologyService == null)
                throw new ArgumentNullException(nameof(topologyService));

            if (sourceId == targetId)
                return true;

            // Get all direct connections from the source
            var connections = await topologyService.GetConnectionsForSourceAsync(sourceId, ct);
            
            // Check if any direct connection exists to the target
            foreach (var connection in connections)
            {
                if (!connection.IsEnabled)
                    continue;

                if (connection.TargetId == targetId)
                {
                    // Evaluate the condition if present
                    if (!string.IsNullOrEmpty(connection.Condition))
                    {
                        bool conditionMet = await topologyService.EvaluateConnectionConditionAsync(connection, ct);
                        if (!conditionMet)
                            continue;
                    }
                    
                    return true;
                }
            }
            
            // No direct connection found
            return false;
        }
    }
}