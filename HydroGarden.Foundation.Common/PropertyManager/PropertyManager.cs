using HydroGarden.Foundation.Abstractions.Interfaces;
using System.Collections.Concurrent;
using HydroGarden.Foundation.Common.Locking;

namespace HydroGarden.Foundation.Common.PropertyManager
{
    public class PropertyManager : IPropertyManager, IAsyncDisposable
    {
        private readonly IPropertyStore _store;
        private readonly IPropertyCache _cache;
        private readonly string _id;
        private readonly ConcurrentDictionary<string, object?> _metadata;
        private readonly AsyncReaderWriterLock _lock;
        private readonly ILogger? _logger;
        private volatile bool _isLoaded;
        private volatile bool _isDisposed;

        public event EventHandler<IPropertyChangedEventArgs>? PropertyChanged;

        public PropertyManager(
            string id,
            IPropertyStore store,
            IPropertyCache cache,
            ILogger? logger = null)
        {
            _id = id ?? throw new ArgumentNullException(nameof(id));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger;

            _metadata = new ConcurrentDictionary<string, object?>();
            _lock = new AsyncReaderWriterLock();
        }

        public async Task LoadAsync(CancellationToken ct = default)
        {
            CheckDisposed();
            if (_isLoaded) return;

            using var writeLock = await _lock.WriterLockAsync(ct);
            if (_isLoaded) return;

            try
            {
                var properties = await _store.LoadAsync(_id, ct);
                foreach (var kvp in properties)
                {
                    await TryAddPropertyAsync(kvp.Key, kvp.Value, ct);
                }

                _isLoaded = true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Failed to load properties for {_id}");
                throw;
            }
        }

        private async Task<bool> TryAddPropertyAsync(
            string key,
            object value,
            CancellationToken ct)
        {
            try
            {
                var type = value.GetType();
                var metadataType = typeof(PropertyMetadata<>).MakeGenericType(type);
                var metadata = Activator.CreateInstance(metadataType, value, false, true, null);

                _metadata[key] = metadata;
                await _cache.SetAsync(key, value, ct);
                return true;
            }
            catch (Exception? ex)
            {
                _logger?.LogWarning(ex, $"Failed to add property {key}");
                return false;
            }
        }

        public async Task<T?> GetPropertyAsync<T>(string name, CancellationToken ct = default)
        {
            CheckDisposed();
            if (!_isLoaded) throw new InvalidOperationException("Properties not loaded");

            using var readLock = await _lock.ReaderLockAsync(ct);

            // Try cache first
            if (await _cache.TryGetAsync<T>(name, ct))
            {
                return await _cache.GetValueOrDefaultAsync<T>(name, ct);
            }

            // Get from metadata
            if (_metadata.TryGetValue(name, out var obj))
            {
                if (obj is PropertyMetadata<T> metadata)
                {
                    var value = await metadata.GetValueAsync(ct);
                    await _cache.SetAsync(name, value, ct);
                    return value;
                }
                throw new InvalidCastException($"Property {name} is not of type {typeof(T).Name}");
            }

            throw new KeyNotFoundException($"Property '{name}' not found");
        }

        public async Task SetPropertyAsync<T>(
            string name,
            T? value,
            bool isReadOnly = false,
            IPropertyValidator<IValidationResult, T>? validator = null,
            CancellationToken ct = default)
        {
            CheckDisposed();
            if (!_isLoaded) throw new InvalidOperationException("Properties not loaded");

            using var writeLock = await _lock.WriterLockAsync(ct);

            var metadata = (PropertyMetadata<T?>)_metadata.GetOrAdd(
                name,
                _ => new PropertyMetadata<T?>(value, isReadOnly, true, validator!))!;

            var oldValue = await metadata.GetValueAsync(ct);

            if (await metadata.TrySetValueAsync(value, ct))
            {
                await _cache.SetAsync(name, value, ct);
                OnPropertyChanged(name, oldValue, value);
            }
            else
            {
                throw new InvalidOperationException($"Failed to set property: {metadata.LastError}");
            }
        }

        public bool HasProperty(string name)
        {
            CheckDisposed();
            return _metadata.ContainsKey(name);
        }

        protected virtual void OnPropertyChanged(string name, object? oldValue, object? newValue)
        {
            var handler = PropertyChanged;
            if (handler != null)
            {
                var args = new EventArgs.PropertyChangedEventArgs(name, oldValue, newValue);
                handler(this, args);
            }
        }

        private void CheckDisposed()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(PropertyManager));
        }

        public async ValueTask DisposeAsync()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            try
            {
                // Final save attempt
                await SaveAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error saving properties during disposal");
            }
            finally
            {
                _lock.Dispose();
                if (_cache is IAsyncDisposable asyncCache)
                {
                    await asyncCache.DisposeAsync();
                }
                else if (_cache is IDisposable disposableCache)
                {
                    disposableCache.Dispose();
                }

                foreach (var metadata in _metadata.Values)
                {
                    if (metadata is IAsyncDisposable asyncDisposable)
                    {
                        await asyncDisposable.DisposeAsync();
                    }
                    else if (metadata is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        public async Task SaveAsync(CancellationToken ct = default)
        {
            CheckDisposed();
            if (!_isLoaded) throw new InvalidOperationException("Properties not loaded");

            using var readLock = await _lock.ReaderLockAsync(ct);
            try
            {
                var updates = new Dictionary<string, object>();
                foreach (var kvp in _metadata)
                {
                    var name = kvp.Key;
                    var metadata = kvp.Value;
                    var value = await ((metadata?.GetType()
                        .GetMethod("GetValueAsync")
                        ?.Invoke(metadata, [ct]) as Task<object>)!);

                    updates[name] = value;
                }

                await _store.SaveAsync(_id, updates, ct);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Failed to save properties for {_id}");
                throw;
            }
        }
    }
}