using FluentAssertions;
using HydroGarden.Foundation.Common.Caching;
using Xunit;

namespace HydroGarden.Foundation.Tests.Unit.Caching
{
    public class PropertyCacheTests : IAsyncDisposable
    {
        private readonly SmartCache _cache;
        private readonly SmartCache _largeCache;
        private readonly CancellationTokenSource _cts;

        public PropertyCacheTests()
        {
            _cts = new CancellationTokenSource(TimeSpan.FromSeconds(1)); // Test timeout

            _cache = new SmartCache(
                slidingExpiration: TimeSpan.FromMilliseconds(50),
                maxSize: 2);

            _largeCache = new SmartCache(
                slidingExpiration: TimeSpan.FromMilliseconds(50),
                maxSize: 10);
        }

        [Fact]
        public async Task Cache_ShouldHandleMultipleTypes()
        {
            // Arrange - Using largeCache to avoid size limitations
            await _largeCache.SetAsync("string", "text", _cts.Token);
            await _largeCache.SetAsync("number", 42, _cts.Token);
            await _largeCache.SetAsync("boolean", true, _cts.Token);

            // Act & Assert
            var stringResult = await _largeCache.TryGetAsync<string>("string", _cts.Token);
            stringResult.exists.Should().BeTrue("because string value was just added");
            stringResult.value.Should().Be("text");

            var numberResult = await _largeCache.TryGetAsync<int>("number", _cts.Token);
            numberResult.exists.Should().BeTrue("because number value was just added");
            numberResult.value.Should().Be(42);

            var boolResult = await _largeCache.TryGetAsync<bool>("boolean", _cts.Token);
            boolResult.exists.Should().BeTrue("because boolean value was just added");
            boolResult.value.Should().BeTrue();

            // Verify type safety
            var invalidResult = await _largeCache.TryGetAsync<int>("string", _cts.Token);
            invalidResult.exists.Should().BeFalse("because types don't match");
        }

        [Fact]
        public async Task Cache_ShouldRetainItems_WithinExpirationPeriod()
        {
            // Arrange
            await _cache.SetAsync("test", "value", _cts.Token);

            // Act - Check immediately
            var immediateResult = await _cache.TryGetAsync<string>("test", _cts.Token);

            // Short wait, but less than expiration
            await Task.Delay(25, _cts.Token); // Half the expiration time
            var midResult = await _cache.TryGetAsync<string>("test", _cts.Token);

            // Assert
            immediateResult.exists.Should().BeTrue("because item was just added");
            immediateResult.value.Should().Be("value");

            midResult.exists.Should().BeTrue("because expiration time hasn't elapsed");
            midResult.value.Should().Be("value");
        }

        [Fact]
        public async Task Cache_ShouldRetainMostUsedItems_WhenAtMaxSize()
        {
            // Arrange
            await _cache.SetAsync("first", "value1", _cts.Token);
            await _cache.SetAsync("second", "value2", _cts.Token);

            // Access both items multiple times
            await _cache.TryGetAsync<string>("first", _cts.Token);
            await _cache.TryGetAsync<string>("second", _cts.Token);

            // Assert both items exist
            var firstResult = await _cache.TryGetAsync<string>("first", _cts.Token);
            var secondResult = await _cache.TryGetAsync<string>("second", _cts.Token);

            firstResult.exists.Should().BeTrue("because it's within size limit and accessed");
            firstResult.value.Should().Be("value1");

            secondResult.exists.Should().BeTrue("because it's within size limit and accessed");
            secondResult.value.Should().Be("value2");
        }

        [Fact]
        public async Task Cache_ShouldExpireItems_AfterSlidingExpirationPeriod()
        {
            // Arrange
            await _cache.SetAsync("test", "value", _cts.Token);

            // Verify initial state
            var initialResult = await _cache.TryGetAsync<string>("test", _cts.Token);
            initialResult.exists.Should().BeTrue("because item was just added");

            // Act - Wait longer than sliding expiration
            await Task.Delay(100, _cts.Token); // Wait double the expiration time
            var result = await _cache.TryGetAsync<string>("test", _cts.Token);

            // Assert
            result.exists.Should().BeFalse("because the item should have expired after 50ms");
        }

        [Fact]
        public async Task Cache_ShouldRemoveLeastUsedItems_WhenExceedingMaxSize()
        {
            // Arrange & Act
            await _cache.SetAsync("first", "value1", _cts.Token);
            await _cache.SetAsync("second", "value2", _cts.Token);

            // Access second item multiple times to increase its access count
            var secondGet1 = await _cache.TryGetAsync<string>("second", _cts.Token);
            var secondGet2 = await _cache.TryGetAsync<string>("second", _cts.Token);

            // Verify setup
            secondGet1.exists.Should().BeTrue("because second item should be available");
            secondGet2.exists.Should().BeTrue("because second item should still be available");

            // Check initial state
            var initialFirst = await _cache.TryGetAsync<string>("first", _cts.Token);
            initialFirst.exists.Should().BeTrue("because we haven't exceeded size limit yet");

            // Add third item, which should trigger cleanup
            await _cache.SetAsync("third", "value3", _cts.Token);

            // Assert final state
            var firstResult = await _cache.TryGetAsync<string>("first", _cts.Token);
            firstResult.exists.Should().BeFalse("because it has the lowest access count (0) and should be removed");

            var secondResult = await _cache.TryGetAsync<string>("second", _cts.Token);
            secondResult.exists.Should().BeTrue("because it has a higher access count (2)");
            secondResult.value.Should().Be("value2", "because the value should be preserved");

            var thirdResult = await _cache.TryGetAsync<string>("third", _cts.Token);
            thirdResult.exists.Should().BeTrue("because it was just added");
            thirdResult.value.Should().Be("value3", "because the value should be preserved");
        }

        [Fact]
        public async Task Cache_ShouldUpdateAccessTime_OnGet()
        {
            // Arrange
            await _cache.SetAsync("test", "value", _cts.Token);

            // Act - Multiple accesses with delays
            await Task.Delay(25, _cts.Token); // Wait half expiration
            var firstAccess = await _cache.TryGetAsync<string>("test", _cts.Token);

            await Task.Delay(25, _cts.Token); // Wait another half expiration
            var secondAccess = await _cache.TryGetAsync<string>("test", _cts.Token);

            await Task.Delay(25, _cts.Token); // Wait another half expiration
            var thirdAccess = await _cache.TryGetAsync<string>("test", _cts.Token);

            // Assert
            firstAccess.exists.Should().BeTrue("because first access refreshes expiration");
            secondAccess.exists.Should().BeTrue("because second access refreshes expiration");
            thirdAccess.exists.Should().BeTrue("because third access refreshes expiration");
        }

        [Fact]
        public async Task Cache_ShouldNotAffectExistingItems_WhenAddingNewItemsWithinLimit()
        {
            // Arrange
            await _cache.SetAsync("first", "value1", _cts.Token);

            // Act
            await _cache.SetAsync("second", "value2", _cts.Token); // Still within limit

            // Assert
            var firstResult = await _cache.TryGetAsync<string>("first", _cts.Token);
            firstResult.exists.Should().BeTrue("because it's within size limit");
            firstResult.value.Should().Be("value1");

            var secondResult = await _cache.TryGetAsync<string>("second", _cts.Token);
            secondResult.exists.Should().BeTrue("because it's within size limit");
            secondResult.value.Should().Be("value2");
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                _cts.Cancel(); // Ensure cleanup tasks are cancelled
                await Task.WhenAll(
                    _cache.DisposeAsync().AsTask(),
                    _largeCache.DisposeAsync().AsTask()
                );
            }
            finally
            {
                _cts.Dispose();
            }
        }
    }
}