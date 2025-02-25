using FluentAssertions;
using HydroGarden.Foundation.Abstractions.Interfaces;
using HydroGarden.Foundation.Common.PropertyMetadata;
using HydroGarden.Foundation.Core.Stores;
using Moq;
using Xunit;

namespace HydroGarden.Foundation.Tests.Unit.Store
{
    public class JsonStoreTests : IDisposable
    {
        private readonly string _testDirectory;
        private readonly string _testFilePath;
        private readonly JsonStore _sut;
        private readonly Mock<IHydroGardenLogger> _mockLogger;

        public JsonStoreTests()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), "HydroGardenTests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testDirectory);
            _testFilePath = Path.Combine(_testDirectory, "ComponentProperties.json");
            _mockLogger = new Mock<IHydroGardenLogger>();
            _sut = new JsonStore("testPath", _mockLogger.Object);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, true);
            }
        }

        [Fact]
        public async Task SaveAsync_ShouldCreateValidJsonFile()
        {
            var deviceId = Guid.NewGuid();
            var properties = new Dictionary<string, object>
            {
                { "Name", "Test Device" },
                { "IsActive", true },
                { "Value", 42.5 }
            };
            await _sut.SaveAsync(deviceId, properties);
            File.Exists(_testFilePath).Should().BeTrue();
            var fileContent = await File.ReadAllTextAsync(_testFilePath);
            fileContent.Should().NotBeNullOrEmpty();
            fileContent.Should().Contain(deviceId.ToString());
            fileContent.Should().Contain("Test Device");
        }

        [Fact]
        public async Task LoadAsync_ShouldReturnSavedProperties()
        {
            var deviceId = Guid.NewGuid();
            var properties = new Dictionary<string, object>
            {
                { "Name", "Test Device" },
                { "IsActive", true },
                { "Value", 42.5 }
            };
            await _sut.SaveAsync(deviceId, properties);
            var loadedProperties = await _sut.LoadAsync(deviceId);
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
            File.Exists(_testFilePath).Should().BeTrue();
            var fileContent = await File.ReadAllTextAsync(_testFilePath);
            fileContent.Should().NotBeNullOrEmpty();
            fileContent.Should().Contain("Metadata");
            fileContent.Should().Contain("Device Name");
            fileContent.Should().Contain("Sensor Value");
        }

        [Fact]
        public async Task LoadMetadataAsync_ShouldReturnSavedMetadata()
        {
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
            var loadedMetadata = await _sut.LoadMetadataAsync(deviceId);
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
        public async Task BeginTransactionAsync_ShouldCreateValidTransaction()
        {
            await using var transaction = await _sut.BeginTransactionAsync();
            transaction.Should().NotBeNull();
            transaction.Should().BeOfType<JsonStoreTransaction>();
        }

        [Fact]
        public async Task Transaction_CommitAsync_ShouldPersistChanges()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var properties = new Dictionary<string, object>
            {
                { "Name", "Test Device" },
                { "Value", 42.5 }
            };

            // Act
            await using (var transaction = await _sut.BeginTransactionAsync())
            {
                await transaction.SaveAsync(deviceId, properties);
                await transaction.CommitAsync();
            }

            // Assert
            var loadedProperties = await _sut.LoadAsync(deviceId);
            loadedProperties.Should().NotBeNull();
            loadedProperties.Should().ContainKey("Name");
            loadedProperties["Name"].Should().Be("Test Device");
            loadedProperties.Should().ContainKey("Value");
            ((double)loadedProperties["Value"]).Should().BeApproximately(42.5, 0.01);
        }

        [Fact]
        public async Task Transaction_RollbackAsync_ShouldNotPersistChanges()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var properties = new Dictionary<string, object>
            {
                { "Name", "Test Device" },
                { "Value", 42.5 }
            };

            // Act
            await using (var transaction = await _sut.BeginTransactionAsync())
            {
                await transaction.SaveAsync(deviceId, properties);
                await transaction.RollbackAsync();
            }

            // Assert
            var loadedProperties = await _sut.LoadAsync(deviceId);
            loadedProperties.Should().BeNull();
        }

        [Fact]
        public async Task Transaction_Dispose_WithoutCommit_ShouldRollbackChanges()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var properties = new Dictionary<string, object>
            {
                { "Name", "Test Device" },
                { "Value", 42.5 }
            };

            // Act
            await using (var transaction = await _sut.BeginTransactionAsync())
            {
                await transaction.SaveAsync(deviceId, properties);
                // No commit or rollback, just dispose
            }

            // Assert
            var loadedProperties = await _sut.LoadAsync(deviceId);
            loadedProperties.Should().BeNull();
        }

        [Fact]
        public async Task Transaction_SaveWithMetadataAsync_ShouldPersistMetadata()
        {
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
            new PropertyMetadata
            {
                IsEditable = true,
                IsVisible = true,
                DisplayName = "Device Name",
                Description = "The name of the device"
            }
        },
        {
            "Value",
            new PropertyMetadata
            {
                IsEditable = false,
                IsVisible = true,
                DisplayName = "Sensor Value",
                Description = "The current value from the sensor"
            }
        }
    };

            await using (var transaction = await _sut.BeginTransactionAsync())
            {
                await transaction.SaveWithMetadataAsync(deviceId, properties, metadata);
                await transaction.CommitAsync();
            }

            // Ensure async write operations are completed before reading
            await Task.Delay(100);

            var loadedMetadata = await _sut.LoadMetadataAsync(deviceId);

            loadedMetadata.Should().NotBeNull("Metadata should be stored and retrievable.");
            loadedMetadata.Should().ContainKey("Name");
            loadedMetadata["Name"].DisplayName.Should().Be("Device Name");
            loadedMetadata["Name"].IsEditable.Should().BeTrue();
            loadedMetadata.Should().ContainKey("Value");
            loadedMetadata["Value"].DisplayName.Should().Be("Sensor Value");
            loadedMetadata["Value"].IsEditable.Should().BeFalse();
        }



        [Fact]
        public async Task Transaction_MultipleSaves_ShouldPersistAllChanges()
        {
            // Arrange
            var device1Id = Guid.NewGuid();
            var device2Id = Guid.NewGuid();
            var properties1 = new Dictionary<string, object>
            {
                { "Name", "Device 1" },
                { "Value", 42.5 }
            };
            var properties2 = new Dictionary<string, object>
            {
                { "Name", "Device 2" },
                { "Value", 99.9 }
            };

            // Act
            await using (var transaction = await _sut.BeginTransactionAsync())
            {
                await transaction.SaveAsync(device1Id, properties1);
                await transaction.SaveAsync(device2Id, properties2);
                await transaction.CommitAsync();
            }

            // Assert
            var loadedProperties1 = await _sut.LoadAsync(device1Id);
            var loadedProperties2 = await _sut.LoadAsync(device2Id);

            loadedProperties1.Should().NotBeNull();
            loadedProperties1["Name"].Should().Be("Device 1");
            ((double)loadedProperties1["Value"]).Should().BeApproximately(42.5, 0.01);

            loadedProperties2.Should().NotBeNull();
            loadedProperties2["Name"].Should().Be("Device 2");
            ((double)loadedProperties2["Value"]).Should().BeApproximately(99.9, 0.01);
        }

        [Fact]
        public async Task SaveAsync_MultipleTimes_ShouldPreserveOtherDeviceData()
        {
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
            await _sut.SaveAsync(device1Id, device1Properties);
            await _sut.SaveAsync(device2Id, device2Properties);
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
            var deviceId = Guid.NewGuid();
            var result = await _sut.LoadAsync(deviceId);
            result.Should().BeNull();
        }

        [Fact]
        public async Task LoadAsync_NonExistentDevice_ShouldReturnNull()
        {
            var existingDeviceId = Guid.NewGuid();
            var nonExistentDeviceId = Guid.NewGuid();
            var properties = new Dictionary<string, object>
            {
                { "Name", "Test Device" }
            };
            await _sut.SaveAsync(existingDeviceId, properties);
            var result = await _sut.LoadAsync(nonExistentDeviceId);
            result.Should().BeNull();
        }

        [Fact]
        public async Task SaveAsync_WithComplexTypes_ShouldHandleCorrectly()
        {
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
            await _sut.SaveAsync(deviceId, properties);
            var loadedProperties = await _sut.LoadAsync(deviceId);
            loadedProperties.Should().NotBeNull();
            loadedProperties["Name"].Should().Be("Complex Device");
            var loadedTimestamp = loadedProperties["Timestamp"];
            loadedTimestamp.Should().BeOfType<DateTime>();
            ((DateTime)loadedTimestamp).Should().BeCloseTo(now, TimeSpan.FromMilliseconds(10));
            var loadedGuid = loadedProperties["Identifier"];
            loadedGuid.Should().BeOfType<Guid>();
            loadedGuid.Should().Be(guid);
            loadedProperties["Type"].ToString().Should().Contain("JsonStore");
        }

        [Fact]
        public async Task SaveAsync_WithInvalidJsonContent_ShouldHandleError()
        {
            var deviceId = Guid.NewGuid();
            var properties = new Dictionary<string, object>
            {
                { "Name", "Test Device" }
            };
            await File.WriteAllTextAsync(_testFilePath, "This is not valid JSON content");
            await Assert.ThrowsAsync<System.Text.Json.JsonException>(() =>
                _sut.SaveAsync(deviceId, properties));
        }

        [Fact]
        public async Task SaveAsync_WithConcurrentAccess_ShouldMaintainDataIntegrity()
        {
            var device1Id = Guid.NewGuid();
            var device2Id = Guid.NewGuid();
            var device1Properties = new Dictionary<string, object> { { "Name", "Device 1" } };
            var device2Properties = new Dictionary<string, object> { { "Name", "Device 2" } };
            var task1 = _sut.SaveAsync(device1Id, device1Properties);
            var task2 = _sut.SaveAsync(device2Id, device2Properties);
            await Task.WhenAll(task1, task2);
            var loadedDevice1Properties = await _sut.LoadAsync(device1Id);
            var loadedDevice2Properties = await _sut.LoadAsync(device2Id);
            loadedDevice1Properties.Should().NotBeNull();
            loadedDevice1Properties["Name"].Should().Be("Device 1");
            loadedDevice2Properties.Should().NotBeNull();
            loadedDevice2Properties["Name"].Should().Be("Device 2");
        }
    }
}