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
                maxSize: 2);
        }

        [Fact]
        public async Task Cache_ShouldGrow_WhenFrequentlyAccessedItemsExceedBaseSize()
        {
            // Set first item (count=1)
            await _cache.SetAsync("first", "value1", _cts.Token);
            await Task.Delay(10, _cts.Token);

            // Set second item (count=1)
            await _cache.SetAsync("second", "value2", _cts.Token);
            await Task.Delay(10, _cts.Token);

            // Access both items multiple times
            for (int i = 0; i < 3; i++)
            {
                // Each access adds 1 to count
                bool firstExists = await _cache.TryGetAsync<string>("first", _cts.Token);
                bool secondExists = await _cache.TryGetAsync<string>("second", _cts.Token);

                firstExists.Should().BeTrue();
                secondExists.Should().BeTrue();

                await Task.Delay(20, _cts.Token);
            }

            // At this point:
            // first: set(1) + 3 gets(3) = 4
            // second: set(1) + 3 gets(3) = 4

            await Task.Delay(20, _cts.Token);
            // Add third item (count=1)
            await _cache.SetAsync("third", "value3", _cts.Token);
            await Task.Delay(50, _cts.Token);

            bool firstResult = await _cache.TryGetAsync<string>("first", _cts.Token);
            bool secondResult = await _cache.TryGetAsync<string>("second", _cts.Token);
            bool thirdResult = await _cache.TryGetAsync<string>("third", _cts.Token);

            // Both frequently used items should remain due to higher counts
            firstResult.Should().BeTrue("because it was frequently accessed (count=4)");
            secondResult.Should().BeTrue("because it was frequently accessed (count=4)");
            thirdResult.Should().BeTrue("because it was just added (count=1)");
        }

        [Fact]
        public async Task Cache_ShouldShrink_WhenFrequencyDrops()
        {
            // first: set(1)
            await _cache.SetAsync("first", "value1", _cts.Token);
            // second: set(1)
            await _cache.SetAsync("second", "value2", _cts.Token);

            for (int i = 0; i < 3; i++)
            {
                bool firstExists = await _cache.TryGetAsync<string>("first", _cts.Token);
                bool secondExists = await _cache.TryGetAsync<string>("second", _cts.Token);

                firstExists.Should().BeTrue();
                secondExists.Should().BeTrue();

                await Task.Delay(10, _cts.Token);
            }

            // At this point:
            // first: set(1) + 3 gets(3) = 4
            // second: set(1) + 3 gets(3) = 4

            // Add new items
            await _cache.SetAsync("third", "value3", _cts.Token);
            await Task.Delay(50, _cts.Token);
            await _cache.SetAsync("fourth", "value4", _cts.Token);
            await Task.Delay(50, _cts.Token);

            bool firstExists1 = await _cache.TryGetAsync<string>("first", _cts.Token);
            bool secondExists1 = await _cache.TryGetAsync<string>("second", _cts.Token);
            bool thirdResult = await _cache.TryGetAsync<string>("third", _cts.Token);
            bool fourthResult = await _cache.TryGetAsync<string>("fourth", _cts.Token);

            thirdResult.Should().BeTrue("because it has set(1) + get(1) = 2");
            fourthResult.Should().BeTrue("because it was most recent with set(1) + get(1) = 2");
        }

        [Fact]
        public async Task Cache_ShouldPreferRemovingNonFrequentItems()
        {
            // Set initial items
            await _cache.SetAsync("frequent1", "value1", _cts.Token);
            await Task.Delay(20, _cts.Token);
            await _cache.SetAsync("frequent2", "value2", _cts.Token);

            // Add accesses
            for (int i = 0; i < 4; i++)
            {
                await _cache.TryGetAsync<string>("frequent1", _cts.Token);
                await _cache.TryGetAsync<string>("frequent2", _cts.Token);
                await Task.Delay(25, _cts.Token);
            }

            // At this point:
            // frequent1: set(1) + 4 gets(4) = 5
            // frequent2: set(1) + 4 gets(4) = 5

            await Task.Delay(50, _cts.Token);

            // Add less frequently accessed items
            await _cache.SetAsync("infrequent", "value3", _cts.Token); // count=1
            await Task.Delay(50, _cts.Token);
            await _cache.SetAsync("new", "value4", _cts.Token); // count=1

            await Task.Delay(450, _cts.Token);

            bool infrequentExists = await _cache.TryGetAsync<string>("infrequent", _cts.Token);
            bool freq1ExistsAfter = await _cache.TryGetAsync<string>("frequent1", _cts.Token);
            bool freq2ExistsAfter = await _cache.TryGetAsync<string>("frequent2", _cts.Token);
            bool newExists = await _cache.TryGetAsync<string>("new", _cts.Token);

            infrequentExists.Should().BeFalse("because it was infrequently accessed (count=1)");
            freq1ExistsAfter.Should().BeTrue("because it was frequently accessed (count=5)");
            freq2ExistsAfter.Should().BeTrue("because it was frequently accessed (count=5)");
            newExists.Should().BeTrue("because it was most recent with set(1) + get(1) = 2");
        }

        async ValueTask IAsyncDisposable.DisposeAsync()
        {
            await _cache.DisposeAsync();
            _cts.Dispose();
        }
    }
}