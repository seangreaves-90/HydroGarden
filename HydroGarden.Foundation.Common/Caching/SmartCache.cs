using System.Collections.Concurrent;
using System.Threading.Channels;
using HydroGarden.Foundation.Abstractions.Interfaces;

namespace HydroGarden.Foundation.Common.Caching
{
    public class SmartCache : IPropertyCache, IAsyncDisposable
    {
        private class CacheEntry
        {
            public object? Value { get; }
            private long _lastAccessedTicks;
            private int _accessCount;

            public int AccessCount => _accessCount;
            public DateTimeOffset LastAccessed => new(Interlocked.Read(ref _lastAccessedTicks), TimeSpan.Zero);

            public CacheEntry(object? value)
            {
                Value = value;
                _lastAccessedTicks = DateTimeOffset.UtcNow.Ticks;
                _accessCount = 0;
            }

            public void RecordAccess()
            {
                Interlocked.Exchange(ref _lastAccessedTicks, DateTimeOffset.UtcNow.Ticks);
                Interlocked.Increment(ref _accessCount);
            }

            public bool IsExpired(TimeSpan slidingExpiration) =>
                DateTimeOffset.UtcNow - LastAccessed > slidingExpiration;

            public bool IsFrequentlyAccessed(TimeSpan window) =>
                DateTimeOffset.UtcNow - LastAccessed <= window && AccessCount > 1;
        }

        private readonly ConcurrentDictionary<string, CacheEntry> _entries;
        private readonly TimeSpan _slidingExpiration;
        private readonly TimeSpan _frequencyWindow;
        private readonly int _baseMaxSize;
        private volatile int _currentMaxSize;
        private readonly SemaphoreSlim _cleanupLock;
        private readonly Channel<TaskCompletionSource<bool>> _cleanupChannel;
        private readonly CancellationTokenSource _cleanupCts;
        private readonly Task _cleanupTask;
        private volatile bool _isDisposed;

        public SmartCache(
            TimeSpan? slidingExpiration = null,
            int maxSize = 1000,
            TimeSpan? frequencyWindow = null)
        {
            _entries = new ConcurrentDictionary<string, CacheEntry>();
            _slidingExpiration = slidingExpiration ?? TimeSpan.FromMinutes(10);
            _frequencyWindow = frequencyWindow ?? TimeSpan.FromMinutes(5);
            _baseMaxSize = maxSize;
            _currentMaxSize = maxSize;
            _cleanupLock = new SemaphoreSlim(1, 1);
            _cleanupCts = new CancellationTokenSource();
            _cleanupChannel = Channel.CreateBounded<TaskCompletionSource<bool>>(
                new BoundedChannelOptions(10)
                {
                    FullMode = BoundedChannelFullMode.Wait
                });
            _cleanupTask = ProcessCleanupRequestsAsync(_cleanupCts.Token);
        }

        public int CurrentMaxSize => _currentMaxSize;
        public int CurrentSize => _entries.Count;

        private void AdjustMaxSize()
        {
            var currentEntries = _entries.ToArray();
            var frequentCount = currentEntries.Count(kvp => !kvp.Value.IsExpired(_slidingExpiration)
                && kvp.Value.IsFrequentlyAccessed(_frequencyWindow));

            _currentMaxSize = frequentCount > _baseMaxSize / 2 ? _baseMaxSize * 2 : _baseMaxSize;
        }

        public async ValueTask SetAsync<T>(string key, T value, CancellationToken ct = default)
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(SmartCache));

            var entry = new CacheEntry(value);
            _entries.AddOrUpdate(key, entry, (_, __) => entry);

            var currentSize = _entries.Count;

            if (currentSize > _currentMaxSize)
            {
                AdjustMaxSize();

                if (_entries.Count > _currentMaxSize)
                {
                    // 🚀 **Ensure eviction happens BEFORE allowing the cache to grow**
                    await CleanupAsync(ct);
                }
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

                entry.RecordAccess();
                return true;
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

                return entry.Value is T typedValue ? typedValue : default;
            }

            return default;
        }

        public ValueTask RemoveAsync(string key, CancellationToken ct = default)
        {
            if (_isDisposed) return ValueTask.CompletedTask;
            _entries.TryRemove(key, out _);
            return ValueTask.CompletedTask;
        }

        private async Task ProcessCleanupRequestsAsync(CancellationToken ct)
        {
            await foreach (var tcs in _cleanupChannel.Reader.ReadAllAsync(ct))
            {
                try
                {
                    await CleanupAsync(ct);
                    tcs.TrySetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            }
        }

        private async Task CleanupAsync(CancellationToken ct)
        {
            if (!await _cleanupLock.WaitAsync(0, ct)) return;

            try
            {
                var currentEntries = _entries.ToArray();
                var expiredEntries = new List<KeyValuePair<string, CacheEntry>>();
                var nonFrequentEntries = new List<KeyValuePair<string, CacheEntry>>();
                var frequentEntries = new List<KeyValuePair<string, CacheEntry>>();

                foreach (var entry in currentEntries)
                {
                    if (entry.Value.IsExpired(_slidingExpiration))
                    {
                        expiredEntries.Add(entry);
                    }
                    else if (entry.Value.IsFrequentlyAccessed(_frequencyWindow))
                    {
                        frequentEntries.Add(entry);
                    }
                    else
                    {
                        nonFrequentEntries.Add(entry);
                    }
                }

                // 🚀 **Remove expired items first**
                foreach (var entry in expiredEntries)
                {
                    _entries.TryRemove(entry.Key, out _);
                }

                AdjustMaxSize();

                // 🚀 **STRICT eviction order: Remove all non-frequent items before touching frequent ones**
                while (_entries.Count > _currentMaxSize)
                {
                    if (nonFrequentEntries.Count > 0)
                    {
                        var toRemove = nonFrequentEntries
                            .OrderBy(e => e.Value.LastAccessed)
                            .ThenBy(e => e.Value.AccessCount) // Remove least accessed first
                            .First();

                        _entries.TryRemove(toRemove.Key, out _);
                        nonFrequentEntries.Remove(toRemove);
                    }
                    else if (frequentEntries.Count > 2) // 🚀 **Only remove frequent items if absolutely necessary**
                    {
                        var toRemove = frequentEntries
                            .OrderBy(e => e.Value.LastAccessed)
                            .ThenBy(e => e.Value.AccessCount) // Remove least accessed among frequent ones
                            .First();

                        _entries.TryRemove(toRemove.Key, out _);
                        frequentEntries.Remove(toRemove);
                    }
                    else
                    {
                        break;
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

            _cleanupCts.Cancel();
            _cleanupChannel.Writer.Complete();

            try
            {
                await _cleanupTask;
            }
            catch (OperationCanceledException) { }

            _cleanupCts.Dispose();
            _cleanupLock.Dispose();
            _entries.Clear();
        }
    }
}
