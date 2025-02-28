using HydroGarden.Foundation.Abstractions.Interfaces;
using HydroGarden.Foundation.Abstractions.Interfaces.Services;

namespace HydroGarden.Foundation.Core.Stores
{
    /// <summary>
    /// Represents a transaction for a JSON-based store.
    /// Ensures atomic operations within the store and allows commit or rollback.
    /// </summary>
    public class JsonStoreTransaction : IStoreTransaction
    {
        private readonly JsonStore _store; // Reference to the store handling this transaction
        private readonly Dictionary<string, JsonStore.ComponentStore> _workingState; // Local working copy of the store state
        private bool _isCommitted; // Tracks whether the transaction has been committed
        private bool _isRolledBack; // Tracks whether the transaction has been rolled back
        private bool _isDisposed; // Tracks whether the transaction has been disposed

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonStoreTransaction"/> class.
        /// </summary>
        /// <param name="store">The JSON store associated with this transaction.</param>
        /// <param name="currentState">The current state of the store.</param>
        internal JsonStoreTransaction(JsonStore store, Dictionary<string, JsonStore.ComponentStore> currentState)
        {
            _store = store;
            _workingState = new Dictionary<string, JsonStore.ComponentStore>(currentState);
        }

        /// <summary>
        /// Saves the provided properties in the working state using the given ID.
        /// </summary>
        /// <param name="id">The unique identifier for the component.</param>
        /// <param name="properties">The properties to save.</param>
        public Task SaveAsync(Guid id, IDictionary<string, object> properties)
        {
            var componentId = id.ToString();
            _workingState[componentId] = new JsonStore.ComponentStore
            {
                Properties = new Dictionary<string, object>(properties)
            };
            return Task.CompletedTask;
        }

        /// <summary>
        /// Saves the provided properties and metadata in the working state using the given ID.
        /// </summary>
        /// <param name="id">The unique identifier for the component.</param>
        /// <param name="properties">The properties to save.</param>
        /// <param name="metadata">Optional metadata associated with the properties.</param>
        public Task SaveWithMetadataAsync(Guid id, IDictionary<string, object> properties, IDictionary<string, IPropertyMetadata>? metadata)
        {
            var componentId = id.ToString();
            _workingState[componentId] = new JsonStore.ComponentStore
            {
                Properties = new Dictionary<string, object>(properties),
                Metadata = metadata?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value) ?? new Dictionary<string, IPropertyMetadata>()
            };
            return Task.CompletedTask;
        }

        /// <summary>
        /// Commits the transaction by persisting the working state to the store.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        public async Task CommitAsync(CancellationToken ct = default)
        {
            if (_isCommitted || _isRolledBack) throw new InvalidOperationException("Transaction already finalized.");
            await _store.SaveStoreAsync(_workingState, ct);
            _isCommitted = true;
        }

        /// <summary>
        /// Rolls back the transaction by marking it as rolled back.
        /// Note: This does not revert the changes explicitly, but prevents committing them.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        public Task RollbackAsync(CancellationToken ct = default)
        {
            _isRolledBack = true;
            return Task.CompletedTask;
        }

        /// <summary>
        /// Disposes of the transaction, ensuring rollback if not committed.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (_isDisposed) return;

            // If transaction is not committed or rolled back, perform a rollback
            if (!_isCommitted && !_isRolledBack) await RollbackAsync();

            _isDisposed = true;
        }
    }
}
