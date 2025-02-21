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

        private bool UpdateMaxSize()
        {
            try
            {
                // Safe snapshot with concurrency protection
                var currentEntries = _entries.ToArray();
                var frequentCount = currentEntries.Count(kvp => !kvp.Value.IsExpired(_slidingExpiration)
                    && kvp.Value.IsFrequentlyAccessed(_frequencyWindow));

                var targetSize = _baseMaxSize;
                if (frequentCount > _baseMaxSize / 2)
                {
                    targetSize = _baseMaxSize * 2;
                }

                var oldSize = _currentMaxSize;
                var changed = oldSize != targetSize;
                if (changed)
                {
                    Interlocked.Exchange(ref _currentMaxSize, targetSize);
                }
                return changed;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async ValueTask SetAsync<T>(string key, T value, CancellationToken ct = default)
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(SmartCache));

            var entry = new CacheEntry(value);
            _entries.AddOrUpdate(key, entry, (_, __) => entry);

            // Check size and trigger cleanup if needed
            var currentSize = _entries.Count;
            if (currentSize > _currentMaxSize)
            {
                UpdateMaxSize();

                // If still over size after potential expansion
                if (_entries.Count > _currentMaxSize)
                {
                    var tcs = new TaskCompletionSource<bool>();
                    await _cleanupChannel.Writer.WriteAsync(tcs, ct);
                    try
                    {
                        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                        linkedCts.CancelAfter(TimeSpan.FromSeconds(1));
                        await tcs.Task.WaitAsync(linkedCts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        // Cleanup request timed out, but that's okay
                    }
                }
            }
        }

        public async ValueTask<(bool exists, T? value)> TryGetAsync<T>(string key, CancellationToken ct = default)
        {
            if (_isDisposed) return (false, default);

            if (_entries.TryGetValue(key, out var entry))
            {
                if (entry.IsExpired(_slidingExpiration))
                {
                    await RemoveAsync(key, ct);
                    return (false, default);
                }

                if (entry.Value is T typedValue)
                {
                    entry.RecordAccess();
                    return (true, typedValue);
                }
            }

            return (false, default);
        }

        public ValueTask RemoveAsync(string key, CancellationToken ct = default)
        {
            if (_isDisposed) return ValueTask.CompletedTask;
            _entries.TryRemove(key, out _);
            return ValueTask.CompletedTask;
        }

        private async Task ProcessCleanupRequestsAsync(CancellationToken ct)
        {
            try
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
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Normal shutdown
            }
        }

        private async Task CleanupAsync(CancellationToken ct)
        {
            if (!await _cleanupLock.WaitAsync(0, ct)) return;

            try
            {
                var currentEntries = _entries.ToArray();

                // First pass: identify entries to remove
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

                // Remove expired first
                foreach (var entry in expiredEntries)
                {
                    _entries.TryRemove(entry.Key, out _);
                }

                // Update size based on remaining entries
                UpdateMaxSize();

                // If still over size, remove non-frequent first
                if (_entries.Count > _currentMaxSize)
                {
                    var toRemove = nonFrequentEntries
                        .OrderBy(e => e.Value.AccessCount)
                        .ThenBy(e => e.Value.LastAccessed);

                    foreach (var entry in toRemove)
                    {
                        if (_entries.Count <= _currentMaxSize) break;
                        _entries.TryRemove(entry.Key, out _);
                    }

                    // If still over size, remove least accessed frequent entries
                    if (_entries.Count > _currentMaxSize)
                    {
                        var frequentToRemove = frequentEntries
                            .OrderBy(e => e.Value.AccessCount)
                            .ThenBy(e => e.Value.LastAccessed);

                        foreach (var entry in frequentToRemove)
                        {
                            if (_entries.Count <= _currentMaxSize) break;
                            _entries.TryRemove(entry.Key, out _);
                        }
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
            catch (OperationCanceledException)
            {
                // Expected during shutdown
            }

            _cleanupCts.Dispose();
            _cleanupLock.Dispose();
            _entries.Clear();
        }
    }
}