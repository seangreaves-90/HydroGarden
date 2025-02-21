using FluentAssertions;
using HydroGarden.Foundation.Common.Caching;

namespace HydroGarden.Foundation.Tests.Unit.Caching
{
    public class PropertyCacheTests
    {
        private readonly SmartCache _cache;

        public PropertyCacheTests()
        {
            _cache = new SmartCache(
                slidingExpiration: TimeSpan.FromMilliseconds(100),
                maxSize: 2);
        }

        [Fact]
        public void Cache_ShouldExpireItems_AfterSlidingExpirationPeriod()
        {
            // Arrange
            _cache.Set("test", "value");

            // Act
            Thread.Sleep(150); // Wait longer than sliding expiration
            var exists = _cache.TryGet<string>("test", out var _);

            // Assert
            exists.Should().BeFalse();
        }

        [Fact]
        public void Cache_ShouldRemoveLeastUsedItems_WhenExceedingMaxSize()
        {
            // Arrange
            _cache.Set("first", "value1");
            _cache.Set("second", "value2");

            // Access second item multiple times
            _cache.TryGet<string>("second", out var _);
            _cache.TryGet<string>("second", out var _);

            // Act - Add third item, exceeding max size
            _cache.Set("third", "value3");
            Thread.Sleep(50); // Allow cleanup to run

            // Assert
            _cache.TryGet<string>("first", out var _).Should().BeFalse(); // Should be removed
            _cache.TryGet<string>("second", out var _).Should().BeTrue(); // Should remain
            _cache.TryGet<string>("third", out var _).Should().BeTrue(); // Should remain
        }
    }
}