using HydroGarden.Foundation.Abstractions.Interfaces.Events;

namespace HydroGarden.Foundation.Abstractions.Interfaces.Services
{
    /// <summary>
    /// Repository for storing and retrieving topology connection information
    /// </summary>
    public interface ITopologyRepository
    {
        /// <summary>
        /// Stores a topology connection.
        /// </summary>
        /// <param name="connection">The connection to store.</param>
        /// <param name="ct">Cancellation token.</param>
        Task StoreConnectionAsync(IComponentConnection connection, CancellationToken ct = default);

        /// <summary>
        /// Retrieves all stored connections.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A collection of component connections.</returns>
        Task<IEnumerable<IComponentConnection>> GetAllConnectionsAsync(CancellationToken ct = default);

        /// <summary>
        /// Retrieves a specific connection by its ID.
        /// </summary>
        /// <param name="connectionId">The ID of the connection to retrieve.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The component connection, or null if not found.</returns>
        Task<IComponentConnection?> GetConnectionAsync(Guid connectionId, CancellationToken ct = default);

        /// <summary>
        /// Deletes a connection by its ID.
        /// </summary>
        /// <param name="connectionId">The ID of the connection to delete.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>True if the connection was deleted, false if not found.</returns>
        Task<bool> DeleteConnectionAsync(Guid connectionId, CancellationToken ct = default);
    }
}