using FluentAssertions;
using HydroGarden.Foundation.Common.Caching;
using Xunit;

namespace HydroGarden.Foundation.Tests.Unit.Caching
{
    public class SmartCacheDynamicTests : IAsyncDisposable
    {
        private readonly SmartCache _cache;
        private readonly CancellationTokenSource _cts;

        public SmartCacheDynamicTests()
        {
            _cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            // Start with a base capacity of 2 and a short expiration for testing.
            _cache = new SmartCache(
                slidingExpiration: TimeSpan.FromMilliseconds(500),
                maxSize: 2);
        }

        [Fact]
        public async Task Cache_ShouldGrow_WhenNewItemsExceedCapacity()
        {
            // Initially, capacity is 2.
            _cache.CurrentMaxSize.Should().Be(2);

            // Add two items.
            await _cache.SetAsync("first", "value1", _cts.Token);
            await _cache.SetAsync("second", "value2", _cts.Token);
            _cache.CurrentSize.Should().Be(2);
            _cache.CurrentMaxSize.Should().Be(2);

            // Add a third item, triggering a growth.
            await _cache.SetAsync("third", "value3", _cts.Token);
            await Task.Delay(50, _cts.Token);

            _cache.CurrentSize.Should().Be(3);
            // New capacity should equal the count.
            _cache.CurrentMaxSize.Should().Be(3);

            // All items should still be retrievable.
            bool firstExists = await _cache.TryGetAsync<string>("first", _cts.Token);
            bool secondExists = await _cache.TryGetAsync<string>("second", _cts.Token);
            bool thirdExists = await _cache.TryGetAsync<string>("third", _cts.Token);

            firstExists.Should().BeTrue();
            secondExists.Should().BeTrue();
            thirdExists.Should().BeTrue();
        }

        [Fact]
        public async Task Cache_ShouldShrink_AfterExpiration()
        {
            // Create a cache with a longer expiration to add items initially.
            var longExpirationCache = new SmartCache(slidingExpiration: TimeSpan.FromSeconds(5), maxSize: 10);
            // Add 5 items.
            for (int i = 0; i < 5; i++)
            {
                await longExpirationCache.SetAsync($"key{i}", $"value{i}", _cts.Token);
            }
            longExpirationCache.CurrentSize.Should().Be(5);
            longExpirationCache.CurrentMaxSize.Should().Be(5);

            // Wait for all items to expire.
            await Task.Delay(6000, _cts.Token);

            // Add one new item; this forces cleanup.
            await longExpirationCache.SetAsync("newKey", "newValue", _cts.Token);
            await Task.Delay(50, _cts.Token);

            // Expected: All expired items removed, count becomes 1.
            longExpirationCache.CurrentSize.Should().Be(1);
            // New capacity equals the count (1).
            longExpirationCache.CurrentMaxSize.Should().Be(1);

            bool exists = await longExpirationCache.TryGetAsync<string>("newKey", _cts.Token);
            exists.Should().BeTrue();

            await longExpirationCache.DisposeAsync();
        }

        [Fact]
        public async Task Cache_ShouldNotReturnExpiredItems()
        {
            // Add an item with a very short expiration.
            await _cache.SetAsync("temp", "temporary", _cts.Token);
            _cache.CurrentSize.Should().BeGreaterThan(0);

            // Wait for the item to expire.
            await Task.Delay(600, _cts.Token);

            bool exists = await _cache.TryGetAsync<string>("temp", _cts.Token);
            exists.Should().BeFalse("because the item has expired");
        }

        public async ValueTask DisposeAsync()
        {
            await _cache.DisposeAsync();
            _cts.Dispose();
        }
    }
}
