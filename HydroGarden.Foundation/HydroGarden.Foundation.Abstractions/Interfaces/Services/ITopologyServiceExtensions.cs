using HydroGarden.Foundation.Abstractions.Interfaces.Events;

namespace HydroGarden.Foundation.Abstractions.Interfaces.Services
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
            if (sourceId == targetId)
                return true;

            var connections = await topologyService.GetConnectionsForSourceAsync(sourceId, ct);
            
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
                
                // Recursive check for indirect connections (but limited to one level to avoid cycles)
                // This is a simple implementation - a more sophisticated one might use a graph traversal
                // algorithm with cycle detection
                if (sourceId != connection.TargetId) // Avoid self-loops
                {
                    var indirectConnections = await topologyService.GetConnectionsForSourceAsync(connection.TargetId, ct);
                    
                    foreach (var indirectConnection in indirectConnections)
                    {
                        if (!indirectConnection.IsEnabled)
                            continue;
                            
                        if (indirectConnection.TargetId == targetId)
                        {
                            // Evaluate the condition if present
                            if (!string.IsNullOrEmpty(indirectConnection.Condition))
                            {
                                bool conditionMet = await topologyService.EvaluateConnectionConditionAsync(indirectConnection, ct);
                                if (!conditionMet)
                                    continue;
                            }
                            
                            return true;
                        }
                    }
                }
            }
            
            return false;
        }
    }
}