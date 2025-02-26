using FluentAssertions;
using HydroGarden.Foundation.Common.Caching;
using Xunit;

namespace HydroGarden.Foundation.Tests.Unit.Caching
{
    public class LruCacheTests : IDisposable
    {
        private readonly LruCache<string, int> _sut;
        private const int CacheCapacity = 3;

        public LruCacheTests()
        {
            // Create an LRU cache with small capacity for testing eviction
            _sut = new LruCache<string, int>(CacheCapacity, evictionCheckIntervalSeconds: 1);
        }

        public void Dispose()
        {
            _sut.Dispose();
        }

        [Fact]
        public void Constructor_WithInvalidCapacity_ShouldThrowException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => new LruCache<string, int>(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LruCache<string, int>(-1));
        }

        [Fact]
        public async Task AddOrUpdateAsync_ShouldStoreValue()
        {
            // Arrange
            string key = "testKey";
            int value = 42;

            // Act
            await _sut.AddOrUpdateAsync(key, value);

            // Assert
            _sut.TryGetValue(key, out var retrievedValue).Should().BeTrue();
            retrievedValue.Should().Be(value);
        }

        [Fact]
        public void AddOrUpdate_ShouldStoreValue()
        {
            // Arrange
            string key = "testKey";
            int value = 42;

            // Act
            _sut.AddOrUpdate(key, value);

            // Assert
            _sut.TryGetValue(key, out var retrievedValue).Should().BeTrue();
            retrievedValue.Should().Be(value);
        }

        [Fact]
        public async Task AddOrUpdateAsync_BeyondCapacity_ShouldEvictLeastRecentlyUsed()
        {
            // Arrange
            // Add cache capacity + 1 items
            for (int i = 0; i < CacheCapacity; i++)
            {
                await _sut.AddOrUpdateAsync($"key{i}", i);
            }

            // Access the first item to make it recently used
            _sut.TryGetValue("key0", out _);

            // Act
            // Add one more item to trigger eviction
            await _sut.AddOrUpdateAsync("keyNew", 999);

            // Assert
            // The least recently used item (key1) should be evicted
            _sut.TryGetValue("key0", out _).Should().BeTrue("recently accessed item should remain");
            _sut.TryGetValue("key1", out _).Should().BeFalse("least recently used item should be evicted");
            _sut.TryGetValue("key2", out _).Should().BeTrue("later added item should remain");
            _sut.TryGetValue("keyNew", out _).Should().BeTrue("newest item should be present");
            _sut.Count.Should().Be(CacheCapacity);
        }

        [Fact]
        public void TryGetValue_UpdatesAccessTime()
        {
            // Arrange
            _sut.AddOrUpdate("key1", 1);
            _sut.AddOrUpdate("key2", 2);
            _sut.AddOrUpdate("key3", 3);

            // Act
            // Access key1 to make it recently used
            _sut.TryGetValue("key1", out _);

            // Add a new item to force eviction
            _sut.AddOrUpdate("key4", 4);

            // Assert
            // key2 should be evicted as it's now the least recently used
            _sut.TryGetValue("key1", out _).Should().BeTrue();
            _sut.TryGetValue("key2", out _).Should().BeFalse();
            _sut.TryGetValue("key3", out _).Should().BeTrue();
            _sut.TryGetValue("key4", out _).Should().BeTrue();
        }

        [Fact]
        public void TryRemove_ShouldRemoveItem()
        {
            // Arrange
            _sut.AddOrUpdate("key1", 1);

            // Act
            bool result = _sut.TryRemove("key1", out var removedValue);

            // Assert
            result.Should().BeTrue();
            removedValue.Should().Be(1);
            _sut.TryGetValue("key1", out _).Should().BeFalse();
        }

        [Fact]
        public void TryRemove_NonExistentKey_ShouldReturnFalse()
        {
            // Act
            bool result = _sut.TryRemove("nonExistentKey", out var removedValue);

            // Assert
            result.Should().BeFalse();
            removedValue.Should().Be(0); // Default value for int
        }

        [Fact]
        public void Clear_ShouldRemoveAllItems()
        {
            // Arrange
            _sut.AddOrUpdate("key1", 1);
            _sut.AddOrUpdate("key2", 2);

            // Act
            _sut.Clear();

            // Assert
            _sut.Count.Should().Be(0);
            _sut.TryGetValue("key1", out _).Should().BeFalse();
            _sut.TryGetValue("key2", out _).Should().BeFalse();
        }

        [Fact]
        public async Task EvictionTimerCallback_ShouldEvictItemsWhenOverCapacity()
        {
            // Arrange
            // Use a cache with very short eviction interval for testing
            using var cache = new LruCache<string, int>(2, evictionCheckIntervalSeconds: 1);

            // Add 3 items to exceed capacity
            cache.AddOrUpdate("key1", 1);
            cache.AddOrUpdate("key2", 2);
            cache.AddOrUpdate("key3", 3);

            // The eviction should already have happened during AddOrUpdate
            cache.Count.Should().Be(2);

            // Wait for eviction timer to potentially trigger
            await Task.Delay(2000);

            // Assert
            // Count should still be at capacity
            cache.Count.Should().Be(2);
        }

        [Fact]
        public void Properties_ShouldReportCorrectValues()
        {
            // Act
            _sut.AddOrUpdate("key1", 1);
            _sut.AddOrUpdate("key2", 2);

            // Assert
            _sut.Count.Should().Be(2);
            _sut.Capacity.Should().Be(CacheCapacity);
        }
    }
}