using HydroGarden.Foundation.Abstractions.Interfaces;
using System.Collections.Concurrent;
using HydroGarden.Foundation.Common.Locking;

namespace HydroGarden.Foundation.Common.PropertyManager
{
    public class PropertyManager : IPropertyManager
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
                    await TryAddPropertyAsync(kvp.Key, kvp.Value);
                }

                _isLoaded = true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Failed to load properties for {_id}");
                throw;
            }
        }

        private Task<bool> TryAddPropertyAsync(
            string key,
            object value)
        {
            try
            {
                var type = value.GetType();
                var metadataType = typeof(PropertyMetadata<>).MakeGenericType(type);
                var metadata = Activator.CreateInstance(metadataType, value, false, true, null);

                _metadata[key] = metadata;
                _cache.Set(key, value);
                return Task.FromResult(true);
            }
            catch (Exception? ex)
            {
                _logger?.LogWarning(ex, $"Failed to add property {key}");
                return Task.FromResult(false);
            }
        }

        public async Task<T?> GetPropertyAsync<T>(string name, CancellationToken ct = default)
        {
            CheckDisposed();
            if (!_isLoaded) throw new InvalidOperationException("Properties not loaded");

            using var readLock = await _lock.ReaderLockAsync(ct);

            // Try cache first
            if (_cache.TryGet<T>(name, out var cached))
                return cached;

            // Get from metadata
            if (_metadata.TryGetValue(name, out var obj))
            {
                if (obj is PropertyMetadata<T> metadata)
                {
                    var value = await metadata.GetValueAsync(ct);
                    _cache.Set(name, value);
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
                _cache.Set(name, value);
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

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            try
            {
                // Final save attempt
                SaveAsync(CancellationToken.None).Wait();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error saving properties during disposal");
            }
            finally
            {
                _lock.Dispose();
                foreach (var metadata in _metadata.Values)
                {
                    (metadata as IDisposable)?.Dispose();
                }
            }
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