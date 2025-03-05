/// <summary>
/// Service for managing component connections and topology
/// </summary>
public interface ITopologyService : IAsyncDisposable
{
    /// <summary>
    /// Gets all connections for the given source component
    /// </summary>
    /// <param name="sourceId">The source component ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of connections from the source</returns>
    Task<IReadOnlyList<IComponentConnection>> GetConnectionsForSourceAsync(Guid sourceId, CancellationToken ct = default);

    /// <summary>
    /// Gets all connections for the given target component
    /// </summary>
    /// <param name="targetId">The target component ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of connections to the target</returns>
    Task<IReadOnlyList<IComponentConnection>> GetConnectionsForTargetAsync(Guid targetId, CancellationToken ct = default);

    /// <summary>
    /// Creates a new connection between components
    /// </summary>
    /// <param name="connection">The connection to create</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The created connection with assigned ID</returns>
    Task<IComponentConnection> CreateConnectionAsync(IComponentConnection connection, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing connection
    /// </summary>
    /// <param name="connection">The connection to update</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if the connection was updated, false if not found</returns>
    Task<bool> UpdateConnectionAsync(IComponentConnection connection, CancellationToken ct = default);

    /// <summary>
    /// Deletes a connection by ID
    /// </summary>
    /// <param name="connectionId">The connection ID to delete</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if the connection was deleted, false if not found</returns>
    Task<bool> DeleteConnectionAsync(Guid connectionId, CancellationToken ct = default);

    /// <summary>
    /// Evaluates whether a connection is active based on its condition
    /// </summary>
    /// <param name="connection">The connection to evaluate</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if the connection is active, false otherwise</returns>
    Task<bool> EvaluateConnectionConditionAsync(IComponentConnection connection, CancellationToken ct = default);
}