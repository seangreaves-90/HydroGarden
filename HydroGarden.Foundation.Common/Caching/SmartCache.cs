using System.Collections.Concurrent;
using HydroGarden.Foundation.Abstractions.Interfaces;

namespace HydroGarden.Foundation.Common.Caching
{
    public class SmartCache : IPropertyCache, IAsyncDisposable
    {
        private class CacheEntry
        {
            public object? Value { get; }
            private long _lastAccessedTicks;
            private int _usageCount;

            public int UsageCount => _usageCount;
            public DateTimeOffset LastAccessed => new(Interlocked.Read(ref _lastAccessedTicks), TimeSpan.Zero);

            public CacheEntry(object? value)
            {
                Value = value;
                _lastAccessedTicks = DateTimeOffset.UtcNow.Ticks;
                _usageCount = 0;
            }

            public void RecordUsage()
            {
                Interlocked.Exchange(ref _lastAccessedTicks, DateTimeOffset.UtcNow.Ticks);
                Interlocked.Increment(ref _usageCount);
            }

            public bool IsExpired(TimeSpan slidingExpiration) =>
                DateTimeOffset.UtcNow - LastAccessed > slidingExpiration;
        }

        private readonly ConcurrentDictionary<string, CacheEntry> _entries;
        private readonly TimeSpan _slidingExpiration;
        private readonly int _baseMaxSize;
        private volatile int _currentMaxSize;
        private readonly SemaphoreSlim _cleanupLock;
        private volatile bool _isDisposed;

        public int CurrentMaxSize => _currentMaxSize;
        public int CurrentSize => _entries.Count;

        public SmartCache(
            TimeSpan? slidingExpiration = null,
            int maxSize = 1000)
        {
            _entries = new ConcurrentDictionary<string, CacheEntry>();
            _slidingExpiration = slidingExpiration ?? TimeSpan.FromMinutes(10);
            _baseMaxSize = maxSize;
            _currentMaxSize = maxSize;
            _cleanupLock = new SemaphoreSlim(1, 1);
        }

        private void AdjustMaxSize()
        {
            var currentEntries = _entries.ToArray();
            var frequentCount = currentEntries.Count(kvp => kvp.Value.UsageCount >= 3);

            if (frequentCount > _baseMaxSize / 2)
            {
                _currentMaxSize = _baseMaxSize * 2;
            }
            else
            {
                _currentMaxSize = _baseMaxSize;
            }
        }

        public async ValueTask SetAsync<T>(string key, T value, CancellationToken ct = default)
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(SmartCache));

            var entry = new CacheEntry(value);
            entry.RecordUsage(); // +1 for set

            _entries.AddOrUpdate(key, entry, (_, existing) =>
            {
                entry.RecordUsage(); // +1 for set
                return entry;
            });

            if (_entries.Count > _currentMaxSize)
            {
                await CleanupAsync(ct);
            }
        }

        public async ValueTask<bool> TryGetAsync<T>(string key, CancellationToken ct = default)
        {
            if (_isDisposed) return false;

            if (_entries.TryGetValue(key, out var entry))
            {
                if (entry.IsExpired(_slidingExpiration))
                {
                    await RemoveAsync(key, ct);
                    return false;
                }

                entry.RecordUsage(); // +1 for get
                return entry.Value is T;
            }

            return false;
        }

        public async ValueTask<T?> GetValueOrDefaultAsync<T>(string key, CancellationToken ct = default)
        {
            if (_isDisposed) return default;

            if (_entries.TryGetValue(key, out var entry))
            {
                if (entry.IsExpired(_slidingExpiration))
                {
                    await RemoveAsync(key, ct);
                    return default;
                }

                entry.RecordUsage(); // +1 for get
                return entry.Value is T value ? value : default;
            }

            return default;
        }

        public ValueTask RemoveAsync(string key, CancellationToken ct = default)
        {
            if (_isDisposed) return ValueTask.CompletedTask;
            _entries.TryRemove(key, out _);
            return ValueTask.CompletedTask;
        }

        private async Task CleanupAsync(CancellationToken ct)
        {
            if (!await _cleanupLock.WaitAsync(0, ct)) return;

            try
            {
                // First remove expired
                foreach (var kvp in _entries.ToArray())
                {
                    if (kvp.Value.IsExpired(_slidingExpiration))
                    {
                        _entries.TryRemove(kvp.Key, out _);
                    }
                }

                // Adjust max size based on usage patterns
                AdjustMaxSize();

                // If still over size, sort remaining entries and remove excess
                if (_entries.Count > _currentMaxSize)
                {
                    var remainingEntries = _entries.ToArray();
                    var orderedEntries = remainingEntries
                        .OrderBy(kvp => kvp.Value.UsageCount)  // Lowest usage first
                        .ThenBy(kvp => kvp.Value.LastAccessed) // Oldest first
                        .ToList();

                    // Remove entries until we're at max size
                    int numToRemove = orderedEntries.Count - _currentMaxSize;
                    for (int i = 0; i < numToRemove; i++)
                    {
                        _entries.TryRemove(orderedEntries[i].Key, out _);
                    }
                }
            }
            finally
            {
                _cleanupLock.Release();
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            _cleanupLock.Dispose();
            _entries.Clear();

            await ValueTask.CompletedTask;
        }
    }
}