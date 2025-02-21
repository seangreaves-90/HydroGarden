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
            _cache = new SmartCache(
                slidingExpiration: TimeSpan.FromMilliseconds(500),
                maxSize: 2,
                frequencyWindow: TimeSpan.FromMilliseconds(200));
        }

        [Fact]
        public async Task Cache_ShouldGrow_WhenFrequentlyAccessedItemsExceedBaseSize()
        {
            // Add initial items
            await _cache.SetAsync("first", "value1", _cts.Token);
            await Task.Delay(10, _cts.Token);
            await _cache.SetAsync("second", "value2", _cts.Token);
            await Task.Delay(10, _cts.Token);

            // Access items frequently
            for (int i = 0; i < 3; i++)
            {
                var first = await _cache.TryGetAsync<string>("first", _cts.Token);
                var second = await _cache.TryGetAsync<string>("second", _cts.Token);
                first.exists.Should().BeTrue();
                second.exists.Should().BeTrue();
                await Task.Delay(20, _cts.Token);  // Increased delay between accesses
            }

            await Task.Delay(20, _cts.Token);  // Additional delay for frequency recognition

            // Add third item - should not trigger cleanup due to frequent access
            await _cache.SetAsync("third", "value3", _cts.Token);
            await Task.Delay(50, _cts.Token);

            // All items should exist since cache grew
            var firstResult = await _cache.TryGetAsync<string>("first", _cts.Token);
            var secondResult = await _cache.TryGetAsync<string>("second", _cts.Token);
            var thirdResult = await _cache.TryGetAsync<string>("third", _cts.Token);

            firstResult.exists.Should().BeTrue("because it was frequently accessed");
            secondResult.exists.Should().BeTrue("because it was frequently accessed");
            thirdResult.exists.Should().BeTrue("because cache size increased");

            _cache.CurrentMaxSize.Should().Be(4, "because cache doubled in size");
            _cache.CurrentSize.Should().Be(3);
        }

        [Fact]
        public async Task Cache_ShouldShrink_WhenFrequencyDrops()
        {
            // Add and access items frequently
            await _cache.SetAsync("first", "value1", _cts.Token);
            await _cache.SetAsync("second", "value2", _cts.Token);

            for (int i = 0; i < 3; i++)
            {
                var first = await _cache.TryGetAsync<string>("first", _cts.Token);
                var second = await _cache.TryGetAsync<string>("second", _cts.Token);
                first.exists.Should().BeTrue();
                second.exists.Should().BeTrue();
                await Task.Delay(10, _cts.Token);
            }

            // Add third item
            await _cache.SetAsync("third", "value3", _cts.Token);

            // Wait for frequency window to expire
            await Task.Delay(250, _cts.Token);

            // Add fourth item to trigger cleanup
            await _cache.SetAsync("fourth", "value4", _cts.Token);
            await Task.Delay(50, _cts.Token);

            _cache.CurrentMaxSize.Should().Be(2, "because frequency window expired");
            _cache.CurrentSize.Should().Be(2);

            // Verify only most recently accessed items remain
            var thirdResult = await _cache.TryGetAsync<string>("third", _cts.Token);
            var fourthResult = await _cache.TryGetAsync<string>("fourth", _cts.Token);

            thirdResult.exists.Should().BeTrue("because it was more recent");
            fourthResult.exists.Should().BeTrue("because it was most recent");
        }

        [Fact]
        public async Task Cache_ShouldPreferRemovingNonFrequentItems()
        {
            // Add items and access some frequently
            await _cache.SetAsync("frequent1", "value1", _cts.Token);
            await _cache.SetAsync("frequent2", "value2", _cts.Token);
            await _cache.SetAsync("infrequent", "value3", _cts.Token);

            // Access frequent items multiple times
            for (int i = 0; i < 3; i++)
            {
                var freq1 = await _cache.TryGetAsync<string>("frequent1", _cts.Token);
                var freq2 = await _cache.TryGetAsync<string>("frequent2", _cts.Token);
                freq1.exists.Should().BeTrue();
                freq2.exists.Should().BeTrue();
                await Task.Delay(10, _cts.Token);
            }

            // Add new item to trigger cleanup
            await _cache.SetAsync("new", "value4", _cts.Token);
            await Task.Delay(50, _cts.Token);

            // Verify infrequent item was removed
            var infrequentResult = await _cache.TryGetAsync<string>("infrequent", _cts.Token);
            var freq1Result = await _cache.TryGetAsync<string>("frequent1", _cts.Token);
            var freq2Result = await _cache.TryGetAsync<string>("frequent2", _cts.Token);
            var newResult = await _cache.TryGetAsync<string>("new", _cts.Token);

            infrequentResult.exists.Should().BeFalse("because it was infrequently accessed");
            freq1Result.exists.Should().BeTrue("because it was frequently accessed");
            freq2Result.exists.Should().BeTrue("because it was frequently accessed");
            newResult.exists.Should().BeTrue("because it was just added");

            _cache.CurrentMaxSize.Should().Be(4, "because we have frequent items");
            _cache.CurrentSize.Should().Be(3);
        }

        async ValueTask IAsyncDisposable.DisposeAsync()
        {
            await _cache.DisposeAsync();
            _cts.Dispose();
        }
    }
}