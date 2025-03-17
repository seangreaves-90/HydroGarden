using HydroGarden.Foundation.Abstractions.Interfaces.Components;
using HydroGarden.Foundation.Abstractions.Interfaces.Events;

namespace HydroGarden.Foundation.Abstractions.Interfaces.Services
{
    /// <summary>
    /// Unified persistence service interface for component and topology data
    /// </summary>
    public interface IPersistenceService : IAsyncDisposable
    {
        /// <summary>
        /// Registers or updates a IIoTDevice component in the persistence layer.
        /// Ensures component properties are loaded and stored efficiently.
        /// </summary>
        /// <typeparam name="T">The type of the IIoTDevice component (must implement <see cref="IIoTDevice"/>).</typeparam>
        /// <param name="component">The component to add or update.</param>
        /// <param name="ct">Cancellation token for the operation.</param>
        public Task AddOrUpdateAsync<T>(T? component, CancellationToken ct = default) where T : IIoTDevice;

        /// <summary>
        /// Manually triggers batch processing of pending events (for testing or manual execution).
        /// </summary>
        Task ProcessPendingEventsAsync();

        /// <summary>
        /// Retrieves a stored property value for a given device.
        /// </summary>
        Task<T?> GetPropertyAsync<T>(Guid deviceId, string propertyName, CancellationToken ct = default);

        /// <summary>
        /// Begins a transaction for multiple persistence operations.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A transaction object that can be used to commit or rollback changes.</returns>
        Task<IPersistenceTransaction> BeginTransactionAsync(CancellationToken ct = default);

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

        /// <summary>
        /// Retrieves all stored components and their properties.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A list of tuples containing device ID, name, properties, and metadata.</returns>
        Task<List<(Guid Id, string Name, IDictionary<string, object> Properties, IDictionary<string, IPropertyMetadata> Metadata)>> GetAllStoredDevicesAsync(CancellationToken ct = default);
    }

    /// <summary>
    /// Represents a transaction for batch persistence operations.
    /// </summary>
    public interface IPersistenceTransaction : IAsyncDisposable
    {
        /// <summary>
        /// Stores a component within the transaction.
        /// </summary>
        /// <typeparam name="T">The type of the component.</typeparam>
        /// <param name="component">The component to store.</param>
        Task StoreComponentAsync<T>(T component) where T : IIoTDevice;

        /// <summary>
        /// Stores a connection within the transaction.
        /// </summary>
        /// <param name="connection">The connection to store.</param>
        Task StoreConnectionAsync(IComponentConnection connection);

        /// <summary>
        /// Deletes a connection within the transaction.
        /// </summary>
        /// <param name="connectionId">The ID of the connection to delete.</param>
        /// <returns>True if the connection was found and deleted, false otherwise.</returns>
        Task<bool> DeleteConnectionAsync(Guid connectionId);

        /// <summary>
        /// Commits all changes in the transaction.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        Task CommitAsync(CancellationToken ct = default);

        /// <summary>
        /// Rolls back all changes in the transaction.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        Task RollbackAsync(CancellationToken ct = default);
    }
}