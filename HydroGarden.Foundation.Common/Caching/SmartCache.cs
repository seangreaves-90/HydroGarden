using HydroGarden.Foundation.Abstractions.Interfaces;
using System.Collections.Concurrent;

namespace HydroGarden.Foundation.Common.Caching
{
    public class SmartCache : IPropertyCache, IDisposable
    {
        private class CacheEntry
        {
            public object? Value { get; }
            public DateTimeOffset LastAccessed { get; private set; }
            public int AccessCount { get; private set; }

            public CacheEntry(object? value)
            {
                Value = value;
                LastAccessed = DateTimeOffset.UtcNow;
                AccessCount = 0;
            }

            public void RecordAccess()
            {
                LastAccessed = DateTimeOffset.UtcNow;
                AccessCount++;
            }
        }

        private readonly ConcurrentDictionary<string, CacheEntry> _entries;
        private readonly TimeSpan _slidingExpiration;
        private readonly int _maxSize;
        private readonly Timer _cleanupTimer;
        private readonly SemaphoreSlim _cleanupLock;
        private volatile bool _isDisposed;
        private volatile bool _hasEntries;

        public SmartCache(
            TimeSpan? slidingExpiration = null,
            int maxSize = 1000)
        {
            _entries = new ConcurrentDictionary<string, CacheEntry>();
            _slidingExpiration = slidingExpiration ?? TimeSpan.FromMinutes(10);
            _maxSize = maxSize;
            _cleanupLock = new SemaphoreSlim(1, 1);
            _cleanupTimer = new Timer(
                o => {
                    if (_hasEntries)
                        _ = CleanupAsync();
                },
                null,
                Timeout.InfiniteTimeSpan,
                TimeSpan.FromMinutes(1));
        }

        public void Set<T>(string key, T value)
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(SmartCache));

            _entries[key] = new CacheEntry(value);

            if (!_hasEntries)
            {
                _hasEntries = true;
                _cleanupTimer.Change(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
            }

            if (_entries.Count > _maxSize)
            {
                _ = CleanupAsync();
            }
        }

        public bool TryGet<T>(string key, out T? value)
        {
            value = default;
            if (_isDisposed) return false;

            if (_entries.TryGetValue(key, out var entry))
            {
                if (entry.Value is T typedValue)
                {
                    entry.RecordAccess();
                    value = typedValue;
                    return true;
                }
            }
            return false;
        }

        public void Remove(string key)
        {
            if (_isDisposed) return;
            _entries.TryRemove(key, out _);

            if (_entries.IsEmpty)
            {
                _hasEntries = false;
                _cleanupTimer.Change(Timeout.InfiniteTimeSpan, TimeSpan.FromMinutes(1));
            }
        }

        private async Task CleanupAsync()
        {
            if (!await _cleanupLock.WaitAsync(0)) return;
            try
            {
                var now = DateTimeOffset.UtcNow;
                var expired = _entries
                    .Where(e => now - e.Value.LastAccessed > _slidingExpiration)
                    .Select(e => e.Key)
                    .ToList();

                foreach (var key in expired)
                {
                    _entries.TryRemove(key, out _);
                }

                if (_entries.Count > _maxSize)
                {
                    var leastAccessed = _entries
                        .OrderBy(e => e.Value.AccessCount)
                        .Take(_entries.Count - _maxSize)
                        .Select(e => e.Key)
                        .ToList();

                    foreach (var key in leastAccessed)
                    {
                        _entries.TryRemove(key, out _);
                    }
                }

                if (_entries.IsEmpty)
                {
                    _hasEntries = false;
                    _cleanupTimer.Change(Timeout.InfiniteTimeSpan, TimeSpan.FromMinutes(1));
                }
            }
            finally
            {
                _cleanupLock.Release();
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            _cleanupTimer.Dispose();
            _cleanupLock.Dispose();
            _entries.Clear();
        }
    }
}
