using FluentAssertions;
using HydroGarden.Foundation.Abstractions.Interfaces;
using HydroGarden.Foundation.Common.PropertyMetadata;
using HydroGarden.Foundation.Core.Stores;
using Xunit;

namespace HydroGarden.Foundation.Tests.Unit.Store
{
    public class JsonStoreTests : IDisposable
    {
        private readonly string _testDirectory;
        private readonly string _testFilePath;
        private readonly JsonStore _sut;

        public JsonStoreTests()
        {
            // Setup temporary test directory
            _testDirectory = Path.Combine(Path.GetTempPath(), "HydroGardenTests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testDirectory);
            _testFilePath = Path.Combine(_testDirectory, "ComponentProperties.json");
            _sut = new JsonStore(_testDirectory);
        }

        public void Dispose()
        {
            // Clean up temporary test files after tests
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, true);
            }
        }

        [Fact]
        public async Task SaveAsync_ShouldCreateValidJsonFile()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var properties = new Dictionary<string, object>
            {
                { "Name", "Test Device" },
                { "IsActive", true },
                { "Value", 42.5 }
            };

            // Act
            await _sut.SaveAsync(deviceId, properties);

            // Assert
            File.Exists(_testFilePath).Should().BeTrue();
            var fileContent = await File.ReadAllTextAsync(_testFilePath);
            fileContent.Should().NotBeNullOrEmpty();

            // The JSON should be valid and contain the device ID
            fileContent.Should().Contain(deviceId.ToString());
            fileContent.Should().Contain("Test Device");
        }

        [Fact]
        public async Task LoadAsync_ShouldReturnSavedProperties()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var properties = new Dictionary<string, object>
            {
                { "Name", "Test Device" },
                { "IsActive", true },
                { "Value", 42.5 }
            };
            await _sut.SaveAsync(deviceId, properties);

            // Act
            var loadedProperties = await _sut.LoadAsync(deviceId);

            // Assert
            loadedProperties.Should().NotBeNull();
            loadedProperties.Should().ContainKey("Name");
            loadedProperties["Name"].Should().Be("Test Device");
            loadedProperties.Should().ContainKey("IsActive");
            loadedProperties["IsActive"].Should().Be(true);
            loadedProperties.Should().ContainKey("Value");
            ((double)loadedProperties["Value"]).Should().BeApproximately(42.5, 0.01);
        }

        [Fact]
        public async Task SaveWithMetadataAsync_ShouldSavePropertiesAndMetadata()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var properties = new Dictionary<string, object>
            {
                { "Name", "Test Device" },
                { "Value", 42.5 }
            };
            var metadata = new Dictionary<string, IPropertyMetadata>
            {
                {
                    "Name",
                    new PropertyMetadata {
                        IsEditable = true,
                        IsVisible = true,
                        DisplayName = "Device Name",
                        Description = "The name of the device"
                    }
                },
                {
                    "Value",
                    new PropertyMetadata {
                        IsEditable = false,
                        IsVisible = true,
                        DisplayName = "Sensor Value",
                        Description = "The current value from the sensor"
                    }
                }
            };

            // Act
            await _sut.SaveWithMetadataAsync(deviceId, properties, metadata);

            // Assert
            File.Exists(_testFilePath).Should().BeTrue();
            var fileContent = await File.ReadAllTextAsync(_testFilePath);
            fileContent.Should().NotBeNullOrEmpty();

            // The JSON should contain metadata information
            fileContent.Should().Contain("Metadata");
            fileContent.Should().Contain("Device Name");
            fileContent.Should().Contain("Sensor Value");
        }

        [Fact]
        public async Task LoadMetadataAsync_ShouldReturnSavedMetadata()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var properties = new Dictionary<string, object>
            {
                { "Name", "Test Device" },
                { "Value", 42.5 }
            };
            var metadata = new Dictionary<string, IPropertyMetadata>
            {
                {
                    "Name",
                    new PropertyMetadata {
                        IsEditable = true,
                        IsVisible = true,
                        DisplayName = "Device Name",
                        Description = "The name of the device"
                    }
                },
                {
                    "Value",
                    new PropertyMetadata {
                        IsEditable = false,
                        IsVisible = true,
                        DisplayName = "Sensor Value",
                        Description = "The current value from the sensor"
                    }
                }
            };
            await _sut.SaveWithMetadataAsync(deviceId, properties, metadata);

            // Act
            var loadedMetadata = await _sut.LoadMetadataAsync(deviceId);

            // Assert
            loadedMetadata.Should().NotBeNull();
            loadedMetadata.Should().ContainKey("Name");
            loadedMetadata["Name"].DisplayName.Should().Be("Device Name");
            loadedMetadata["Name"].Description.Should().Be("The name of the device");
            loadedMetadata["Name"].IsEditable.Should().BeTrue();
            loadedMetadata["Name"].IsVisible.Should().BeTrue();

            loadedMetadata.Should().ContainKey("Value");
            loadedMetadata["Value"].DisplayName.Should().Be("Sensor Value");
            loadedMetadata["Value"].Description.Should().Be("The current value from the sensor");
            loadedMetadata["Value"].IsEditable.Should().BeFalse();
            loadedMetadata["Value"].IsVisible.Should().BeTrue();
        }

        [Fact]
        public async Task SaveAsync_MultipleTimes_ShouldPreserveOtherDeviceData()
        {
            // Arrange
            var device1Id = Guid.NewGuid();
            var device1Properties = new Dictionary<string, object>
            {
                { "Name", "Device 1" },
                { "Value", 42.5 }
            };

            var device2Id = Guid.NewGuid();
            var device2Properties = new Dictionary<string, object>
            {
                { "Name", "Device 2" },
                { "Value", 99.9 }
            };

            // Act
            await _sut.SaveAsync(device1Id, device1Properties);
            await _sut.SaveAsync(device2Id, device2Properties);

            // Assert
            var loadedDevice1Properties = await _sut.LoadAsync(device1Id);
            var loadedDevice2Properties = await _sut.LoadAsync(device2Id);

            loadedDevice1Properties.Should().NotBeNull();
            loadedDevice1Properties["Name"].Should().Be("Device 1");
            ((double)loadedDevice1Properties["Value"]).Should().BeApproximately(42.5, 0.01);

            loadedDevice2Properties.Should().NotBeNull();
            loadedDevice2Properties["Name"].Should().Be("Device 2");
            ((double)loadedDevice2Properties["Value"]).Should().BeApproximately(99.9, 0.01);
        }

        [Fact]
        public async Task LoadAsync_NonExistentFile_ShouldReturnNull()
        {
            // Arrange
            var deviceId = Guid.NewGuid();

            // Act
            var result = await _sut.LoadAsync(deviceId);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task LoadAsync_NonExistentDevice_ShouldReturnNull()
        {
            // Arrange
            var existingDeviceId = Guid.NewGuid();
            var nonExistentDeviceId = Guid.NewGuid();
            var properties = new Dictionary<string, object>
            {
                { "Name", "Test Device" }
            };
            await _sut.SaveAsync(existingDeviceId, properties);

            // Act
            var result = await _sut.LoadAsync(nonExistentDeviceId);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task SaveAsync_WithComplexTypes_ShouldHandleCorrectly()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var now = DateTime.UtcNow;
            var guid = Guid.NewGuid();
            var properties = new Dictionary<string, object>
            {
                { "Name", "Complex Device" },
                { "Timestamp", now },
                { "Identifier", guid },
                { "Type", typeof(JsonStore) }
            };

            // Act
            await _sut.SaveAsync(deviceId, properties);
            var loadedProperties = await _sut.LoadAsync(deviceId);

            // Assert
            loadedProperties.Should().NotBeNull();
            loadedProperties["Name"].Should().Be("Complex Device");

            // DateTime should be preserved with proper serialization
            var loadedTimestamp = loadedProperties["Timestamp"];
            loadedTimestamp.Should().BeOfType<DateTime>();
            ((DateTime)loadedTimestamp).Should().BeCloseTo(now, TimeSpan.FromMilliseconds(10));

            // Guid should be preserved
            var loadedGuid = loadedProperties["Identifier"];
            loadedGuid.Should().BeOfType<Guid>();
            loadedGuid.Should().Be(guid);

            // Type should be preserved as a string with special format
            loadedProperties["Type"].ToString().Should().Contain("JsonStore");
        }

        [Fact]
        public async Task SaveAsync_WithInvalidJsonContent_ShouldHandleError()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var properties = new Dictionary<string, object>
            {
                { "Name", "Test Device" }
            };

            // Create an invalid JSON file
            await File.WriteAllTextAsync(_testFilePath, "This is not valid JSON content");

            // Act & Assert
            await Assert.ThrowsAsync<System.Text.Json.JsonException>(() =>
                _sut.SaveAsync(deviceId, properties));
        }

        [Fact]
        public async Task SaveAsync_WithConcurrentAccess_ShouldMaintainDataIntegrity()
        {
            // Arrange
            var device1Id = Guid.NewGuid();
            var device2Id = Guid.NewGuid();
            var device1Properties = new Dictionary<string, object> { { "Name", "Device 1" } };
            var device2Properties = new Dictionary<string, object> { { "Name", "Device 2" } };

            // Act
            var task1 = _sut.SaveAsync(device1Id, device1Properties);
            var task2 = _sut.SaveAsync(device2Id, device2Properties);

            await Task.WhenAll(task1, task2);

            // Assert
            var loadedDevice1Properties = await _sut.LoadAsync(device1Id);
            var loadedDevice2Properties = await _sut.LoadAsync(device2Id);

            loadedDevice1Properties.Should().NotBeNull();
            loadedDevice1Properties["Name"].Should().Be("Device 1");

            loadedDevice2Properties.Should().NotBeNull();
            loadedDevice2Properties["Name"].Should().Be("Device 2");
        }
    }
}