using FluentAssertions;
using HydroGarden.Foundation.Common.Caching;
using Xunit;

namespace HydroGarden.Foundation.Tests.Unit.Caching
{
    public class SmartCacheTests : IAsyncDisposable
    {
        private readonly SmartCache _cache;
        private readonly CancellationTokenSource _cts;

        public SmartCacheTests()
        {
            _cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            _cache = new SmartCache(
                slidingExpiration: TimeSpan.FromMilliseconds(500),
                maxSize: 2);
        }

        [Fact]
        public async Task ConcurrentAccess_ShouldMaintainConsistency()
        {
            const int numTasks = 50;
            var tasks = new List<Task>();

            for (int i = 0; i < numTasks; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    await _cache.SetAsync($"key{i}", i, _cts.Token);
                    bool exists = await _cache.TryGetAsync<int>($"key{i}", _cts.Token);
                    exists.Should().BeTrue();

                    int value = await _cache.GetValueOrDefaultAsync<int>($"key{i}", _cts.Token);
                    value.Should().Be(i);
                }));
            }

            await Task.WhenAll(tasks);
            await Task.Delay(250, _cts.Token);

            var remainingKeys = await GetAllCacheKeysAsync();
            remainingKeys.Count.Should().BeLessThanOrEqualTo(2, "because cache size is limited to 2");
        }

        [Fact]
        public async Task ExpirationAndCleanup_ShouldWorkTogether()
        {
            await _cache.SetAsync("expire", "value1", _cts.Token);
            await Task.Delay(50, _cts.Token);
            await _cache.SetAsync("keep", "value2", _cts.Token);

            bool keepExists = await _cache.TryGetAsync<string>("keep", _cts.Token);
            keepExists.Should().BeTrue();
            string? keepValue = await _cache.GetValueOrDefaultAsync<string>("keep", _cts.Token);
            keepValue.Should().Be("value2");

            await Task.Delay(200, _cts.Token);

            keepExists = await _cache.TryGetAsync<string>("keep", _cts.Token);
            keepExists.Should().BeTrue();
            keepValue = await _cache.GetValueOrDefaultAsync<string>("keep", _cts.Token);
            keepValue.Should().Be("value2");

            await Task.Delay(300, _cts.Token);

            await _cache.SetAsync("new", "value3", _cts.Token);
            await Task.Delay(50, _cts.Token);

            bool expireExists = await _cache.TryGetAsync<string>("expire", _cts.Token);
            expireExists.Should().BeFalse("because it expired");

            keepExists = await _cache.TryGetAsync<string>("keep", _cts.Token);
            keepExists.Should().BeTrue();
            keepValue = await _cache.GetValueOrDefaultAsync<string>("keep", _cts.Token);
            keepValue.Should().Be("value2");

            bool newExists = await _cache.TryGetAsync<string>("new", _cts.Token);
            newExists.Should().BeTrue();
            string? newValue = await _cache.GetValueOrDefaultAsync<string>("new", _cts.Token);
            newValue.Should().Be("value3");
        }

        [Fact]
        public async Task Concurrent_SetAndGet_ShouldMaintainConsistency()
        {
            const int numOperations = 1000;
            var random = new Random();
            var tasks = new List<Task>();

            for (int i = 0; i < numOperations; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    var key = $"key{random.Next(5)}";
                    if (random.Next(2) == 0)
                    {
                        await _cache.SetAsync(key, random.Next(100), _cts.Token);
                    }
                    else
                    {
                        await _cache.TryGetAsync<int>(key, _cts.Token);
                    }
                }));
            }

            await Task.WhenAll(tasks);
            await Task.Delay(500, _cts.Token);

            var remainingKeys = await GetAllCacheKeysAsync();
            remainingKeys.Count.Should().BeLessThanOrEqualTo(2, "because cache size is limited to 2");
        }

        private async Task<ICollection<string>> GetAllCacheKeysAsync()
        {
            var keys = new HashSet<string>();
            for (int i = 0; i < 100; i++)
            {
                bool exists = await _cache.TryGetAsync<object>($"key{i}", _cts.Token);
                if (exists)
                {
                    keys.Add($"key{i}");
                }
            }
            return keys;
        }

        public async ValueTask DisposeAsync()
        {
            await _cache.DisposeAsync();
            _cts.Dispose();
        }
    }
}
