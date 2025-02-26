using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HydroGarden.Foundation.Common.Caching
{
    /// <summary>
    /// Implements a Least Recently Used (LRU) cache with eviction policy.
    /// </summary>
    /// <typeparam name="TKey">The type of the cache key.</typeparam>
    /// <typeparam name="TValue">The type of the cache value.</typeparam>
    public class LruCache<TKey, TValue> : IDisposable where TKey : notnull
    {
        private readonly int _capacity;
        private readonly ConcurrentDictionary<TKey, TValue> _cache = new();
        private readonly ConcurrentDictionary<TKey, long> _accessTimestamps = new();
        private readonly SemaphoreSlim _evictionLock = new(1, 1);
        private readonly TimeSpan _evictionCheckInterval;
        private readonly Timer _evictionTimer;
        private bool _isDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="LruCache{TKey, TValue}"/> class.
        /// </summary>
        /// <param name="capacity">The maximum number of items the cache can hold.</param>
        /// <param name="evictionCheckIntervalSeconds">Interval in seconds for checking evictions.</param>
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
        /// Gets the cache capacity.
        /// </summary>
        public int Capacity => _capacity;

        /// <summary>
        /// Tries to retrieve a value from the cache.
        /// </summary>
        /// <param name="key">The key to look up.</param>
        /// <param name="value">The retrieved value, if found.</param>
        /// <returns>True if the key exists, otherwise false.</returns>
        public bool TryGetValue(TKey key, out TValue? value)
        {
            if (_cache.TryGetValue(key, out value))
            {
                _accessTimestamps[key] = DateTime.UtcNow.Ticks;
                return true;
            }
            value = default;
            return false;
        }

        /// <summary>
        /// Adds or updates an entry in the cache asynchronously.
        /// </summary>
        /// <param name="key">The key to store the value.</param>
        /// <param name="value">The value to store.</param>
        public async Task AddOrUpdateAsync(TKey key, TValue value)
        {
            _cache[key] = value;
            _accessTimestamps[key] = DateTime.UtcNow.Ticks;

            if (_cache.Count > _capacity)
            {
                await EvictOldestItemAsync();
            }
        }

        public void AddOrUpdate(TKey key, TValue value)
        {
            _cache[key] = value;
            _accessTimestamps[key] = DateTime.UtcNow.Ticks;
            if (_cache.Count > _capacity)
            {
                _ = EvictOldestItemAsync();
            }
        }


        /// <summary>
        /// Removes an entry from the cache.
        /// </summary>
        /// <param name="key">The key to remove.</param>
        /// <param name="value">The removed value, if found.</param>
        /// <returns>True if the key was found and removed, otherwise false.</returns>
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
        /// Clears all items from the cache.
        /// </summary>
        public void Clear()
        {
            _cache.Clear();
            _accessTimestamps.Clear();
        }

        /// <summary>
        /// Disposes of cache resources.
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

        private async void EvictionTimerCallback(object? state)
        {
            if (_isDisposed)
                return;

            try
            {
                if (_cache.Count > _capacity)
                {
                    await EvictOldestItemAsync();
                }
            }
            catch (Exception)
            {
            }
        }

        private async Task EvictOldestItemAsync()
        {
            if (await _evictionLock.WaitAsync(0))
            {
                try
                {
                    while (_cache.Count > _capacity)
                    {
                        var oldest = _accessTimestamps.OrderBy(x => x.Value).FirstOrDefault();
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
