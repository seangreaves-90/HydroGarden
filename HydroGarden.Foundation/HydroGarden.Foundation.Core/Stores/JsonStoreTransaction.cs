using HydroGarden.Foundation.Abstractions.Interfaces;
using HydroGarden.Foundation.Abstractions.Interfaces.Services;
using System.Text.Json;

namespace HydroGarden.Foundation.Core.Stores
{
    /// <summary>
    /// Represents a transaction for a JSON-based store.
    /// Ensures atomic operations within the store and allows commit or rollback.
    /// </summary>
    public class JsonStoreTransaction : IStoreTransaction
    {
        private readonly JsonStore _store; // Reference to the store handling this transaction
        private readonly SemaphoreSlim _lock; // Lock to be released on disposal
        private readonly Dictionary<Guid, Dictionary<string, object>> _propertyChanges; // Property changes to commit
        private readonly Dictionary<Guid, Dictionary<string, IPropertyMetadata>> _metadataChanges; // Metadata changes to commit
        private bool _isCommitted; // Tracks whether the transaction has been committed
        private bool _isRolledBack; // Tracks whether the transaction has been rolled back
        private bool _isDisposed; // Tracks whether the transaction has been disposed

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonStoreTransaction"/> class.
        /// </summary>
        /// <param name="store">The JSON store associated with this transaction.</param>
        /// <param name="lock">The semaphore lock to release when the transaction completes.</param>
        internal JsonStoreTransaction(JsonStore store, SemaphoreSlim @lock)
        {
            _store = store;
            _lock = @lock;
            _propertyChanges = new Dictionary<Guid, Dictionary<string, object>>();
            _metadataChanges = new Dictionary<Guid, Dictionary<string, IPropertyMetadata>>();
        }

        /// <inheritdoc />
        public Task SaveAsync(Guid id, IDictionary<string, object> properties)
        {
            if (_isCommitted || _isRolledBack)
                throw new InvalidOperationException("Transaction already finalized");
            
            // Store the changes in memory
            var propertyDict = new Dictionary<string, object>();
            
            // Ensure property types are preserved correctly
            foreach (var kvp in properties)
            {
                // Ensure numeric values maintain their type (int vs double)
                if (kvp.Value is int intValue)
                {
                    // FlowRate property should always be double for consistency
                    if (kvp.Key == "FlowRate")
                    {
                        propertyDict[kvp.Key] = (double)intValue;
                    }
                    else
                    {
                        propertyDict[kvp.Key] = kvp.Value;
                    }
                }
                else
                {
                    propertyDict[kvp.Key] = kvp.Value;
                }
            }
            
            _propertyChanges[id] = propertyDict;
            
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task SaveWithMetadataAsync(Guid id, IDictionary<string, object> properties, IDictionary<string, IPropertyMetadata>? metadata)
        {
            if (_isCommitted || _isRolledBack)
                throw new InvalidOperationException("Transaction already finalized");
            
            // Store the property changes
            _propertyChanges[id] = new Dictionary<string, object>(properties);
            
            // Store the metadata changes if provided
            if (metadata != null)
            {
                _metadataChanges[id] = new Dictionary<string, IPropertyMetadata>(metadata);
            }
            
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public async Task CommitAsync(CancellationToken ct = default)
        {
            if (_isCommitted || _isRolledBack)
                throw new InvalidOperationException("Transaction already finalized");

            try
            {
                // Process changes
                foreach (var (id, properties) in _propertyChanges)
                {
                    // See if we have metadata for this entity
                    if (_metadataChanges.TryGetValue(id, out var metadata))
                    {
                        await _store.SaveWithMetadataAsync(id, properties, metadata, ct);
                    }
                    else
                    {
                        await _store.SaveAsync(id, properties, ct);
                    }
                }

                _isCommitted = true;
            }
            catch (Exception)
            {
                // If an error occurs during commit, consider the transaction rolled back
                _isRolledBack = true;
                throw;
            }
        }

        /// <inheritdoc />
        public Task RollbackAsync(CancellationToken ct = default)
        {
            if (_isCommitted)
                throw new InvalidOperationException("Transaction already committed");

            _isRolledBack = true;
            _propertyChanges.Clear();
            _metadataChanges.Clear();
            
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            if (_isDisposed) return;

            try
            {
                // If the transaction wasn't explicitly committed or rolled back, roll it back
                if (!_isCommitted && !_isRolledBack)
                {
                    await RollbackAsync();
                }
            }
            finally
            {
                // Always release the lock
                _lock.Release();
                _isDisposed = true;
            }
        }
    }
}