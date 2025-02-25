namespace HydroGarden.Foundation.Abstractions.Interfaces
{
    /// <summary>
    /// Represents a storage mechanism for component properties and metadata.
    /// </summary>
    public interface IStore
    {
        /// <summary>
        /// Begins a new transaction for batch operations.
        /// </summary>
        /// <param name="ct">Cancellation token</param>
        /// <returns>A transaction object that can be used for batch operations.</returns>
        Task<IStoreTransaction> BeginTransactionAsync(CancellationToken ct = default);

        /// <summary>
        /// Loads properties for a specific component.
        /// </summary>
        /// <param name="id">The component identifier.</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>A dictionary of property names and values, or null if the component is not found.</returns>
        Task<IDictionary<string, object>?> LoadAsync(Guid id, CancellationToken ct = default);

        /// <summary>
        /// Loads metadata for a specific component.
        /// </summary>
        /// <param name="id">The component identifier.</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>A dictionary of property names and metadata, or null if the component is not found.</returns>
        Task<IDictionary<string, IPropertyMetadata>?> LoadMetadataAsync(Guid id, CancellationToken ct = default);

        /// <summary>
        /// Saves properties for a specific component.
        /// </summary>
        /// <param name="id">The component identifier.</param>
        /// <param name="properties">The properties to save.</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task SaveAsync(Guid id, IDictionary<string, object> properties, CancellationToken ct = default);

        /// <summary>
        /// Saves properties and metadata for a specific component.
        /// </summary>
        /// <param name="id">The component identifier.</param>
        /// <param name="properties">The properties to save.</param>
        /// <param name="metadata">The metadata to save.</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task SaveWithMetadataAsync(Guid id, IDictionary<string, object> properties,
            IDictionary<string, IPropertyMetadata>? metadata, CancellationToken ct = default);
    }

    /// <summary>
    /// Represents a transaction for batch operations on a store.
    /// </summary>
    public interface IStoreTransaction : IAsyncDisposable
    {
        /// <summary>
        /// Saves properties for a specific component within this transaction.
        /// </summary>
        /// <param name="id">The component identifier.</param>
        /// <param name="properties">The properties to save.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task SaveAsync(Guid id, IDictionary<string, object> properties);

        /// <summary>
        /// Saves properties and metadata for a specific component within this transaction.
        /// </summary>
        /// <param name="id">The component identifier.</param>
        /// <param name="properties">The properties to save.</param>
        /// <param name="metadata">The metadata to save.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task SaveWithMetadataAsync(Guid id, IDictionary<string, object> properties,
            IDictionary<string, IPropertyMetadata>? metadata);

        /// <summary>
        /// Commits all changes made in this transaction.
        /// </summary>
        /// <param name="ct">Cancellation token</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task CommitAsync(CancellationToken ct = default);

        /// <summary>
        /// Rolls back all changes made in this transaction.
        /// </summary>
        /// <param name="ct">Cancellation token</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task RollbackAsync(CancellationToken ct = default);
    }
}