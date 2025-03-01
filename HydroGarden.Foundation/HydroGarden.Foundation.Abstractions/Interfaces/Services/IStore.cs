namespace HydroGarden.Foundation.Abstractions.Interfaces.Services
{
    /// <summary>
    /// Defines a storage interface for saving and retrieving component properties.
    /// </summary>
    public interface IStore
    {
        /// <summary>
        /// Begins a new transaction asynchronously.
        /// </summary>
        /// <param name="ct">An optional cancellation token.</param>
        /// <returns>A task representing the asynchronous transaction.</returns>
        Task<IStoreTransaction> BeginTransactionAsync(CancellationToken ct = default);

        /// <summary>
        /// Loads stored properties asynchronously.
        /// </summary>
        /// <param name="id">The unique identifier of the component.</param>
        /// <param name="ct">An optional cancellation token.</param>
        /// <returns>A task returning a dictionary of stored properties.</returns>
        Task<IDictionary<string, object>?> LoadAsync(Guid id, CancellationToken ct = default);

        /// <summary>
        /// Loads metadata associated with stored properties asynchronously.
        /// </summary>
        /// <param name="id">The unique identifier of the component.</param>
        /// <param name="ct">An optional cancellation token.</param>
        /// <returns>A task returning a dictionary of property metadata.</returns>
        Task<IDictionary<string, IPropertyMetadata>?> LoadMetadataAsync(Guid id, CancellationToken ct = default);

        /// <summary>
        /// Saves properties asynchronously.
        /// </summary>
        /// <param name="id">The unique identifier of the component.</param>
        /// <param name="properties">The dictionary of properties to save.</param>
        /// <param name="ct">An optional cancellation token.</param>
        Task SaveAsync(Guid id, IDictionary<string, object> properties, CancellationToken ct = default);

        /// <summary>
        /// Saves properties along with their metadata asynchronously.
        /// </summary>
        /// <param name="id">The unique identifier of the component.</param>
        /// <param name="properties">The dictionary of properties to save.</param>
        /// <param name="metadata">The dictionary of property metadata.</param>
        /// <param name="ct">An optional cancellation token.</param>
        Task SaveWithMetadataAsync(Guid id, IDictionary<string, object> properties,
            IDictionary<string, IPropertyMetadata>? metadata, CancellationToken ct = default);
    }

    /// <summary>
    /// Defines a transaction interface for handling batch property storage operations.
    /// </summary>
    public interface IStoreTransaction : IAsyncDisposable
    {
        /// <summary>
        /// Saves properties within a transaction.
        /// </summary>
        /// <param name="id">The unique identifier of the component.</param>
        /// <param name="properties">The properties to save.</param>
        Task SaveAsync(Guid id, IDictionary<string, object> properties);

        /// <summary>
        /// Saves properties with metadata within a transaction.
        /// </summary>
        /// <param name="id">The unique identifier of the component.</param>
        /// <param name="properties">The properties to save.</param>
        /// <param name="metadata">The metadata to save.</param>
        Task SaveWithMetadataAsync(Guid id, IDictionary<string, object> properties,
            IDictionary<string, IPropertyMetadata>? metadata);

        /// <summary>
        /// Commits the transaction asynchronously.
        /// </summary>
        Task CommitAsync(CancellationToken ct = default);

        /// <summary>
        /// Rolls back the transaction asynchronously.
        /// </summary>
        Task RollbackAsync(CancellationToken ct = default);
    }
}
