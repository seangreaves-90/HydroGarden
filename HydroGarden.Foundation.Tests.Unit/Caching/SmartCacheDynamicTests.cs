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
            await _cache.SetAsync("first", "value1", _cts.Token);
            await Task.Delay(10, _cts.Token);
            await _cache.SetAsync("second", "value2", _cts.Token);
            await Task.Delay(10, _cts.Token);

            for (int i = 0; i < 3; i++)
            {
                bool firstExists = await _cache.TryGetAsync<string>("first", _cts.Token);
                bool secondExists = await _cache.TryGetAsync<string>("second", _cts.Token);

                firstExists.Should().BeTrue();
                secondExists.Should().BeTrue();

                await Task.Delay(20, _cts.Token);
            }

            await Task.Delay(20, _cts.Token);
            await _cache.SetAsync("third", "value3", _cts.Token);
            await Task.Delay(50, _cts.Token);

            bool firstResult = await _cache.TryGetAsync<string>("first", _cts.Token);
            bool secondResult = await _cache.TryGetAsync<string>("second", _cts.Token);
            bool thirdResult = await _cache.TryGetAsync<string>("third", _cts.Token);

            firstResult.Should().BeTrue("because it was frequently accessed");
            secondResult.Should().BeTrue("because it was frequently accessed");
            thirdResult.Should().BeTrue("because cache size increased");

            _cache.CurrentMaxSize.Should().Be(4, "because cache doubled in size");
            _cache.CurrentSize.Should().Be(3);
        }

        [Fact]
        public async Task Cache_ShouldShrink_WhenFrequencyDrops()
        {
            await _cache.SetAsync("first", "value1", _cts.Token);
            await _cache.SetAsync("second", "value2", _cts.Token);

            for (int i = 0; i < 3; i++)
            {
                bool firstExists = await _cache.TryGetAsync<string>("first", _cts.Token);
                bool secondExists = await _cache.TryGetAsync<string>("second", _cts.Token);

                firstExists.Should().BeTrue();
                secondExists.Should().BeTrue();

                await Task.Delay(10, _cts.Token);
            }

            await _cache.SetAsync("third", "value3", _cts.Token);
            await Task.Delay(250, _cts.Token);
            await _cache.SetAsync("fourth", "value4", _cts.Token);
            await Task.Delay(50, _cts.Token);

            _cache.CurrentMaxSize.Should().Be(2, "because frequency window expired");
            _cache.CurrentSize.Should().Be(2);

            bool thirdResult = await _cache.TryGetAsync<string>("third", _cts.Token);
            bool fourthResult = await _cache.TryGetAsync<string>("fourth", _cts.Token);

            thirdResult.Should().BeTrue("because it was more recent");
            fourthResult.Should().BeTrue("because it was most recent");
        }

        [Fact]
        public async Task Cache_ShouldPreferRemovingNonFrequentItems()
        {
            await _cache.SetAsync("frequent1", "value1", _cts.Token);
            await _cache.SetAsync("frequent2", "value2", _cts.Token);
            await _cache.SetAsync("infrequent", "value3", _cts.Token);

            for (int i = 0; i < 3; i++)
            {
                bool freq1Exists = await _cache.TryGetAsync<string>("frequent1", _cts.Token);
                bool freq2Exists = await _cache.TryGetAsync<string>("frequent2", _cts.Token);

                freq1Exists.Should().BeTrue();
                freq2Exists.Should().BeTrue();

                await Task.Delay(10, _cts.Token);
            }

            await _cache.SetAsync("new", "value4", _cts.Token);
            await Task.Delay(50, _cts.Token);

            bool infrequentExists = await _cache.TryGetAsync<string>("infrequent", _cts.Token);
            bool freq1ExistsAfter = await _cache.TryGetAsync<string>("frequent1", _cts.Token);
            bool freq2ExistsAfter = await _cache.TryGetAsync<string>("frequent2", _cts.Token);
            bool newExists = await _cache.TryGetAsync<string>("new", _cts.Token);

            infrequentExists.Should().BeFalse("because it was infrequently accessed");
            freq1ExistsAfter.Should().BeTrue("because it was frequently accessed");
            freq2ExistsAfter.Should().BeTrue("because it was frequently accessed");
            newExists.Should().BeTrue("because it was just added");

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
