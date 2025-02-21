using HydroGarden.Foundation.Abstractions.Interfaces;
using HydroGarden.Foundation.Core.Stores;
using FluentAssertions;
using Moq;
using Xunit;

namespace HydroGarden.Foundation.Tests.Unit.Store
{
    public class JsonPropertyStoreTests : IDisposable
    {
        private readonly string _testDirectory;
        private readonly Mock<ILogger> _loggerMock;
        private readonly JsonPropertyStore _store;

        public JsonPropertyStoreTests()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testDirectory);
            _loggerMock = new Mock<ILogger>();
            _store = new JsonPropertyStore(_testDirectory, _loggerMock.Object);
        }

        [Fact]
        public async Task SaveAndLoad_ShouldPersistValues()
        {
            // Arrange
            var id = "test-device";
            var expectedProperties = new Dictionary<string, object>
            {
                ["string"] = "test",
                ["number"] = 42,
                ["boolean"] = true
            };

            // Act
            await _store.SaveAsync(id, expectedProperties);
            var loadedProperties = await _store.LoadAsync(id);

            // Assert
            loadedProperties.Should().BeEquivalentTo(expectedProperties);
            File.Exists(Path.Combine(_testDirectory, $"{id}.json")).Should().BeTrue();
        }

        [Fact]
        public async Task Load_NonExistentId_ShouldReturnEmptyDictionary()
        {
            // Arrange
            var id = "non-existent";

            // Act
            var result = await _store.LoadAsync(id);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        public void Dispose()
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, true);
            }
        }
    }
}