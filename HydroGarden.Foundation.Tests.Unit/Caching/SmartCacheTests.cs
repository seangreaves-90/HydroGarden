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
                slidingExpiration: TimeSpan.FromMilliseconds(500), // Increased for more reliable testing
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
                    var result = await _cache.TryGetAsync<int>($"key{i}", _cts.Token);
                    result.exists.Should().BeTrue();
                    result.value.Should().Be(i);
                }));
            }

            await Task.WhenAll(tasks);

            // Allow cleanup to run
            await Task.Delay(250, _cts.Token);

            // Check remaining entries
            var remainingKeys = await GetAllCacheKeysAsync();
            remainingKeys.Count.Should().BeLessThanOrEqualTo(2, "because cache size is limited to 2");
        }

        [Fact]
        public async Task RemoveLeastUsed_ShouldWorkConsistently()
        {
            // Add initial items
            await _cache.SetAsync("first", "value1", _cts.Token);
            await _cache.SetAsync("second", "value2", _cts.Token);

            // Access second item multiple times
            for (int i = 0; i < 3; i++)
            {
                var result = await _cache.TryGetAsync<string>("second", _cts.Token);
                result.exists.Should().BeTrue();
                result.value.Should().Be("value2");
                await Task.Delay(10, _cts.Token); // Small delay between accesses
            }

            // Add third item, triggering cleanup
            await _cache.SetAsync("third", "value3", _cts.Token);
            await Task.Delay(100, _cts.Token); // Allow cleanup to process

            // Verify least used item was removed
            var firstResult = await _cache.TryGetAsync<string>("first", _cts.Token);
            firstResult.exists.Should().BeFalse("because it was least accessed");

            var secondResult = await _cache.TryGetAsync<string>("second", _cts.Token);
            secondResult.exists.Should().BeTrue("because it was accessed multiple times");
            secondResult.value.Should().Be("value2");

            var thirdResult = await _cache.TryGetAsync<string>("third", _cts.Token);
            thirdResult.exists.Should().BeTrue("because it was just added");
            thirdResult.value.Should().Be("value3");
        }

        [Fact]
        public async Task ExpirationAndCleanup_ShouldWorkTogether()
        {
            // Add initial items
            await _cache.SetAsync("expire", "value1", _cts.Token);
            await Task.Delay(50, _cts.Token); // Ensure items have different timestamps
            await _cache.SetAsync("keep", "value2", _cts.Token);

            // First access to 'keep'
            var keepCheck = await _cache.TryGetAsync<string>("keep", _cts.Token);
            keepCheck.exists.Should().BeTrue("because item was just added");
            keepCheck.value.Should().Be("value2");

            await Task.Delay(200, _cts.Token); // Wait less than expiration time

            // Second access to 'keep' - should extend its lifetime
            keepCheck = await _cache.TryGetAsync<string>("keep", _cts.Token);
            keepCheck.exists.Should().BeTrue("because item was accessed before expiration");
            keepCheck.value.Should().Be("value2");

            await Task.Delay(300, _cts.Token); // Wait enough for 'expire' to timeout

            // Add new item to trigger cleanup
            await _cache.SetAsync("new", "value3", _cts.Token);

            // Allow cleanup to process
            await Task.Delay(50, _cts.Token);

            // Verify expired item is removed
            var expireResult = await _cache.TryGetAsync<string>("expire", _cts.Token);
            expireResult.exists.Should().BeFalse("because it expired");

            // Verify kept item remains
            var keepResult = await _cache.TryGetAsync<string>("keep", _cts.Token);
            keepResult.exists.Should().BeTrue("because it was accessed");
            keepResult.value.Should().Be("value2");

            // Verify new item exists
            var newResult = await _cache.TryGetAsync<string>("new", _cts.Token);
            newResult.exists.Should().BeTrue("because it was just added");
            newResult.value.Should().Be("value3");
        }

        [Fact]
        public async Task MultipleCleanupRequests_ShouldBeHandledCorrectly()
        {
            // Add items rapidly to trigger multiple cleanup requests
            for (int i = 0; i < 10; i++)
            {
                await _cache.SetAsync($"key{i}", i, _cts.Token);
                await Task.Delay(10, _cts.Token);
            }

            // Allow cleanup to process
            await Task.Delay(150, _cts.Token);

            // Verify cache size is maintained
            var remainingKeys = await GetAllCacheKeysAsync();
            remainingKeys.Count.Should().BeLessThanOrEqualTo(2, "because cache size is limited to 2");
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
                    var key = $"key{random.Next(5)}"; // Use limited key range to force contention
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

            // Allow cleanup to process
            await Task.Delay(250, _cts.Token);

            // Verify cache size is maintained
            var remainingKeys = await GetAllCacheKeysAsync();
            remainingKeys.Count.Should().BeLessThanOrEqualTo(2, "because cache size is limited to 2");
        }

        private async Task<ICollection<string>> GetAllCacheKeysAsync()
        {
            var keys = new HashSet<string>();
            for (int i = 0; i < 100; i++) // Check a reasonable range of keys
            {
                var result = await _cache.TryGetAsync<object>($"key{i}", _cts.Token);
                if (result.exists)
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
