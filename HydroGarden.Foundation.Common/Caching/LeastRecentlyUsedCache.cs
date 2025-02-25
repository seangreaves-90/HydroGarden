using System.Collections.Concurrent;

namespace HydroGarden.Foundation.Common.Caching
{
    /// <summary>
    /// Thread-safe Least Recently Used (LRU) cache implementation with size constraints.
    /// </summary>
    /// <typeparam name="TKey">The type of keys in the cache.</typeparam>
    /// <typeparam name="TValue">The type of values in the cache.</typeparam>
    public class LruCache<TKey, TValue> : IDisposable where TKey : notnull
    {
        private readonly int _capacity;
        private readonly ConcurrentDictionary<TKey, TValue> _cache = new();
        private readonly ConcurrentDictionary<TKey, long> _accessTimestamps = new();
        private readonly SemaphoreSlim _evictionLock = new(1, 1);
        private readonly TimeSpan _evictionCheckInterval;
        private readonly Timer _evictionTimer;
        private long _evictionCounter;
        private bool _isDisposed;

        /// <summary>
        /// Initializes a new instance of the LruCache class with the specified capacity.
        /// </summary>
        /// <param name="capacity">The maximum number of items to store in the cache.</param>
        /// <param name="evictionCheckIntervalSeconds">The interval in seconds at which to check for items to evict.</param>
        public LruCache(int capacity, int evictionCheckIntervalSeconds = 60)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be greater than zero.");

            _capacity = capacity;
            _evictionCheckInterval = TimeSpan.FromSeconds(evictionCheckIntervalSeconds);
            _evictionTimer = new Timer(EvictionTimerCallback, null, _evictionCheckInterval, _evictionCheckInterval);
        }

        /// <summary>
        /// Gets the number of items currently in the cache.
        /// </summary>
        public int Count => _cache.Count;

        /// <summary>
        /// Gets the maximum capacity of the cache.
        /// </summary>
        public int Capacity => _capacity;

        /// <summary>
        /// Gets the time of last eviction run.
        /// </summary>
        public DateTime LastEvictionTime => new(Interlocked.Read(ref _evictionCounter), DateTimeKind.Utc);

        /// <summary>
        /// Attempts to get the value associated with the specified key.
        /// </summary>
        /// <param name="key">The key to get the value for.</param>
        /// <param name="value">When this method returns, contains the value associated with the specified key, 
        /// if the key is found; otherwise, the default value for the type of the value parameter.</param>
        /// <returns>true if the cache contains an element with the specified key; otherwise, false.</returns>
        public bool TryGetValue(TKey key, out TValue? value)
        {
            if (_cache.TryGetValue(key, out value))
            {
                // Update access timestamp on successful retrieval
                _accessTimestamps[key] = DateTime.UtcNow.Ticks;
                return true;
            }

            value = default;
            return false;
        }

        /// <summary>
        /// Adds or updates a key-value pair in the cache.
        /// </summary>
        /// <param name="key">The key to add or update.</param>
        /// <param name="value">The value to add or update.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public async Task AddOrUpdateAsync(TKey key, TValue value)
        {
            // First, update the item in the cache
            _cache[key] = value;
            _accessTimestamps[key] = DateTime.UtcNow.Ticks;

            // Then check if eviction is needed
            if (_cache.Count > _capacity)
            {
                await EvictOldestItemAsync();
            }
        }

        /// <summary>
        /// Adds or updates a key-value pair in the cache.
        /// </summary>
        /// <param name="key">The key to add or update.</param>
        /// <param name="value">The value to add or update.</param>
        public void AddOrUpdate(TKey key, TValue value)
        {
            // This is a synchronous version of AddOrUpdateAsync
            _cache[key] = value;
            _accessTimestamps[key] = DateTime.UtcNow.Ticks;

            // Then check if eviction is needed (non-blocking)
            if (_cache.Count > _capacity)
            {
                _ = EvictOldestItemAsync();
            }
        }

        /// <summary>
        /// Attempts to remove the value with the specified key.
        /// </summary>
        /// <param name="key">The key to remove.</param>
        /// <param name="value">When this method returns, contains the value associated with the specified key, 
        /// if the key is found; otherwise, the default value for the type of the value parameter.</param>
        /// <returns>true if the cache contains an element with the specified key; otherwise, false.</returns>
        public bool TryRemove(TKey key, out TValue? value)
        {
            if (_cache.TryRemove(key, out value))
            {
                _accessTimestamps.TryRemove(key, out _);
                return true;
            }

            value = default;
            return false;
        }

        /// <summary>
        /// Removes all items from the cache.
        /// </summary>
        public void Clear()
        {
            _cache.Clear();
            _accessTimestamps.Clear();
        }

        /// <summary>
        /// Dispose resources used by the cache.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            _evictionTimer.Dispose();
            _evictionLock.Dispose();
            Clear();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Finalizer to ensure resources are cleaned up.
        /// </summary>
        ~LruCache()
        {
            Dispose();
        }

        private async void EvictionTimerCallback(object? state)
        {
            if (_isDisposed)
                return;

            try
            {
                // Only perform eviction if we're over capacity
                if (_cache.Count > _capacity)
                {
                    await EvictOldestItemAsync();
                }

                Interlocked.Exchange(ref _evictionCounter, DateTime.UtcNow.Ticks);
            }
            catch (Exception)
            {
                // Swallow exceptions in the timer callback to prevent unhandled exceptions
            }
        }

        private async Task EvictOldestItemAsync()
        {
            if (await _evictionLock.WaitAsync(0)) // Non-blocking acquire
            {
                try
                {
                    // We do a second check here to avoid duplicate evictions
                    // from multiple concurrent callers
                    while (_cache.Count > _capacity)
                    {
                        // Find the oldest item by timestamp
                        var oldest = _accessTimestamps
                            .OrderBy(x => x.Value)
                            .FirstOrDefault();

                        // It's possible another thread already removed this item
                        if (oldest.Key != null && _cache.TryRemove(oldest.Key, out _))
                        {
                            _accessTimestamps.TryRemove(oldest.Key, out _);
                        }
                    }
                }
                finally
                {
                    _evictionLock.Release();
                }
            }
        }
    }
}