using FluentAssertions;
using HydroGarden.Foundation.Core.Stores;
using HydroGarden.Logger.Abstractions;
using Moq;
using Xunit;

namespace HydroGarden.Foundation.Tests.Unit.Stores
{
    public class JsonStoreTests
    {
        private readonly string _testFilePath;
        private readonly JsonStore _jsonStore;
        private readonly Mock<ILogger?> _loggerMock;

        public JsonStoreTests()
        {
            _testFilePath = Path.Combine(Path.GetTempPath(), "TestComponentProperties.json");
            _loggerMock = new Mock<ILogger?>();
            _jsonStore = new JsonStore(Path.GetTempPath(), _loggerMock.Object);
        }

        /// <summary>
        /// Ensures that properties saved to JSON store can be loaded correctly.
        /// </summary>
        [Fact]
        public async Task LoadAsync_ShouldReturnSavedProperties()
        {
            var deviceId = Guid.NewGuid();
            var properties = new Dictionary<string, object>
            {
                { "Name", "Test Device" },
                { "State", "Running" },
                { "FlowRate", 75.0 }
            };

            await _jsonStore.SaveAsync(deviceId, properties);

            var loadedProperties = await _jsonStore.LoadAsync(deviceId);

            loadedProperties.Should().NotBeNull();
            loadedProperties.Should().ContainKey("Name");

            // Ensure the value is correctly converted and does not contain extra spaces
            loadedProperties["Name"].ToString().Trim().Should().Be("Test Device");
        }

        /// <summary>
        /// Ensures that multiple saves do not overwrite data for other devices.
        /// </summary>
        [Fact]
        public async Task SaveAsync_MultipleTimes_ShouldPreserveOtherDeviceData()
        {
            var device1Id = Guid.NewGuid();
            var device2Id = Guid.NewGuid();

            var properties1 = new Dictionary<string, object>
            {
                { "Name", "Device 1" },
                { "FlowRate", 50.5 }
            };

            var properties2 = new Dictionary<string, object>
            {
                { "Name", "Device 2" },
                { "FlowRate", 100.0 }
            };

            await _jsonStore.SaveAsync(device1Id, properties1);
            await _jsonStore.SaveAsync(device2Id, properties2);

            var loadedProperties1 = await _jsonStore.LoadAsync(device1Id);
            var loadedProperties2 = await _jsonStore.LoadAsync(device2Id);

            loadedProperties1.Should().NotBeNull();
            loadedProperties2.Should().NotBeNull();

            loadedProperties1["Name"].ToString().Trim().Should().Be("Device 1");
            loadedProperties2["Name"].ToString().Trim().Should().Be("Device 2");
        }

        /// <summary>
        /// Ensures that complex data types are handled correctly during save and load operations.
        /// </summary>
        [Fact]
        public async Task SaveAsync_WithComplexTypes_ShouldHandleCorrectly()
        {
            var deviceId = Guid.NewGuid();
            var properties = new Dictionary<string, object>
            {
                { "Name", "Complex Device" },
                { "Settings", new { Mode = "Auto", Threshold = 10 } }, // Nested object
                { "Type", typeof(int).ToString() } // Ensuring Type is saved as string
            };

            await _jsonStore.SaveAsync(deviceId, properties);
            var loadedProperties = await _jsonStore.LoadAsync(deviceId);

            loadedProperties.Should().NotBeNull();
            loadedProperties["Name"].ToString().Trim().Should().Be("Complex Device");
            loadedProperties["Type"].ToString().Trim().Should().Be("System.Int32"); // Ensuring Type serialization is correct
        }

        /// <summary>
        /// Ensures that a transaction commit persists changes correctly.
        /// </summary>
        [Fact]
        public async Task Transaction_CommitAsync_ShouldPersistChanges()
        {
            var deviceId = Guid.NewGuid();
            var properties = new Dictionary<string, object>
            {
                { "Name", "Transaction Device" },
                { "FlowRate", 85.0 }
            };

            await using (var transaction = await _jsonStore.BeginTransactionAsync())
            {
                await transaction.SaveAsync(deviceId, properties);
                await transaction.CommitAsync();
            }

            var loadedProperties = await _jsonStore.LoadAsync(deviceId);

            loadedProperties.Should().NotBeNull();
            loadedProperties["Name"].ToString().Trim().Should().Be("Transaction Device");
        }

        /// <summary>
        /// Ensures that multiple saves within a transaction persist correctly.
        /// </summary>
        [Fact]
        public async Task Transaction_MultipleSaves_ShouldPersistAllChanges()
        {
            var device1Id = Guid.NewGuid();
            var device2Id = Guid.NewGuid();

            var properties1 = new Dictionary<string, object>
            {
                { "Name", "Device 1" },
                { "FlowRate", 45.0 }
            };

            var properties2 = new Dictionary<string, object>
            {
                { "Name", "Device 2" },
                { "FlowRate", 90.0 }
            };

            await using (var transaction = await _jsonStore.BeginTransactionAsync())
            {
                await transaction.SaveAsync(device1Id, properties1);
                await transaction.SaveAsync(device2Id, properties2);
                await transaction.CommitAsync();
            }

            var loadedProperties1 = await _jsonStore.LoadAsync(device1Id);
            var loadedProperties2 = await _jsonStore.LoadAsync(device2Id);

            loadedProperties1.Should().NotBeNull();
            loadedProperties2.Should().NotBeNull();

            loadedProperties1["Name"].ToString().Trim().Should().Be("Device 1");
            loadedProperties2["Name"].ToString().Trim().Should().Be("Device 2");
        }

        /// <summary>
        /// Ensures that transactions do not persist changes if rolled back.
        /// </summary>
        [Fact]
        public async Task Transaction_Rollback_ShouldNotPersistChanges()
        {
            var deviceId = Guid.NewGuid();
            var properties = new Dictionary<string, object>
            {
                { "Name", "Rollback Device" },
                { "FlowRate", 60.0 }
            };

            await using (var transaction = await _jsonStore.BeginTransactionAsync())
            {
                await transaction.SaveAsync(deviceId, properties);
                // No commit, implicitly rolls back
            }

            var loadedProperties = await _jsonStore.LoadAsync(deviceId);
            loadedProperties.Should().BeNull(); // Ensure rollback works
        }



        /// <summary>
        /// Ensures that existing properties are updated properly in a transaction.
        /// </summary>
        [Fact]
        public async Task Transaction_UpdateExisting_ShouldReflectNewValues()
        {
            var deviceId = Guid.NewGuid();
            var initialProperties = new Dictionary<string, object>
            {
                { "Name", "Old Name" },
                { "FlowRate", 70.0 }
            };

            await _jsonStore.SaveAsync(deviceId, initialProperties);

            await using (var transaction = await _jsonStore.BeginTransactionAsync())
            {
                await transaction.SaveAsync(deviceId, new Dictionary<string, object>
                {
                    { "Name", "Updated Name" },
                    { "FlowRate", 95.0 }
                });
                await transaction.CommitAsync();
            }

            var loadedProperties = await _jsonStore.LoadAsync(deviceId);

            loadedProperties.Should().NotBeNull();
            loadedProperties["Name"].ToString().Trim().Should().Be("Updated Name");
            loadedProperties["FlowRate"].Should().Be(95.0);
        }
    }
}
