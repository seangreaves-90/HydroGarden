using System.Collections.Concurrent;
using HydroGarden.Foundation.Abstractions.Interfaces;

namespace HydroGarden.Foundation.Common.Caching
{
    public class SmartCache : IPropertyCache
    {
        private class CacheEntry
        {
            public object? Value { get; }
            private DateTimeOffset _lastAccessed;

            public CacheEntry(object? value)
            {
                Value = value;
                _lastAccessed = DateTimeOffset.UtcNow;
            }

            public void RecordAccess() => _lastAccessed = DateTimeOffset.UtcNow;

            public bool IsExpired(TimeSpan slidingExpiration) =>
                DateTimeOffset.UtcNow - _lastAccessed > slidingExpiration;
        }

        private readonly ConcurrentDictionary<string, CacheEntry> _entries;
        private readonly TimeSpan _slidingExpiration;
        // _baseMaxSize is no longer used for percentage adjustments.
        private volatile int _currentMaxSize;
        private readonly object _resizeLock = new();

        public int CurrentMaxSize => _currentMaxSize;
        public int CurrentSize => _entries.Count;

        public SmartCache(TimeSpan? slidingExpiration = null, int maxSize = 1000)
        {
            _entries = new ConcurrentDictionary<string, CacheEntry>();
            _slidingExpiration = slidingExpiration ?? TimeSpan.FromMinutes(10);
            _currentMaxSize = maxSize;
        }

        public ValueTask SetAsync<T>(string key, T value, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            var entry = new CacheEntry(value);
            _entries.AddOrUpdate(key, entry, (_, existing) =>
            {
                // Record access on the existing entry and replace it.
                existing.RecordAccess();
                return entry;
            });

            CleanupAndResizeIfNeeded();

            return ValueTask.CompletedTask;
        }

        public ValueTask<bool> TryGetAsync<T>(string key, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            if (_entries.TryGetValue(key, out var entry))
            {
                if (entry.IsExpired(_slidingExpiration))
                {
                    _entries.TryRemove(key, out _);
                    CleanupAndResizeIfNeeded();
                    return new ValueTask<bool>(false);
                }

                if (entry.Value is T)
                {
                    entry.RecordAccess();
                    return new ValueTask<bool>(true);
                }
            }
            return new ValueTask<bool>(false);
        }

        public ValueTask<T?> GetValueOrDefaultAsync<T>(string key, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            if (_entries.TryGetValue(key, out var entry))
            {
                if (entry.IsExpired(_slidingExpiration))
                {
                    _entries.TryRemove(key, out _);
                    CleanupAndResizeIfNeeded();
                    return new ValueTask<T?>(default(T));
                }

                if (entry.Value is T value)
                {
                    entry.RecordAccess();
                    return new ValueTask<T?>(value);
                }
            }
            return new ValueTask<T?>(default(T));
        }

        public ValueTask RemoveAsync(string key, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            _entries.TryRemove(key, out _);
            CleanupAndResizeIfNeeded();
            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// Cleanup expired entries and set the current capacity equal to the current item count.
        /// </summary>
        private void CleanupAndResizeIfNeeded()
        {
            lock (_resizeLock)
            {
                foreach (var kvp in _entries.ToArray())
                {
                    if (kvp.Value.IsExpired(_slidingExpiration))
                    {
                        _entries.TryRemove(kvp.Key, out _);
                    }
                }
                // Simply set capacity to the current count.
                _currentMaxSize = _entries.Count;
            }
        }

        public ValueTask DisposeAsync()
        {
            _entries.Clear();
            return ValueTask.CompletedTask;
        }
    }
}
