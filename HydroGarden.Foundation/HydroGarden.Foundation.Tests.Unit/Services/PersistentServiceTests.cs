using FluentAssertions;
using HydroGarden.Foundation.Abstractions.Interfaces;
using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Foundation.Abstractions.Interfaces.Logging;
using HydroGarden.Foundation.Abstractions.Interfaces.Services;
using HydroGarden.Foundation.Common.Events;
using HydroGarden.Foundation.Common.PropertyMetadata;
using HydroGarden.Foundation.Core.Components.Devices;
using HydroGarden.Foundation.Core.Stores;
using Moq;
using Xunit;

namespace HydroGarden.Foundation.Tests.Unit.Services
{
    public class PersistenceServiceMetadataTests : IAsyncDisposable
    {
        private readonly Mock<IStore> _mockStore;
        private readonly Mock<IStoreTransaction> _mockTransaction;
        private readonly Mock<ILogger> _mockLogger;
        private readonly Mock<IEventBus> _mockEventBus;
        private readonly Core.Services.PersistenceService _sut;
        private Dictionary<string, IPropertyMetadata> _capturedMetadata;

        public PersistenceServiceMetadataTests()
        {
            _mockStore = new Mock<IStore>();
            _mockTransaction = new Mock<IStoreTransaction>();
            _mockLogger = new Mock<ILogger>();
            _mockEventBus = new Mock<IEventBus>();
            _capturedMetadata = new Dictionary<string, IPropertyMetadata>();

            _mockStore.Setup(s => s.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(_mockTransaction.Object);

            _mockTransaction.Setup(t => t.SaveWithMetadataAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<IDictionary<string, object>>(),
                    It.IsAny<IDictionary<string, IPropertyMetadata>>()))
                .Callback<Guid, IDictionary<string, object>, IDictionary<string, IPropertyMetadata>>(
                    (_, __, metadata) =>
                    {
                        if (metadata != null)
                        {
                            _capturedMetadata = new Dictionary<string, IPropertyMetadata>(metadata);
                        }
                    })
                .Returns(Task.CompletedTask);

            _mockTransaction.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _mockTransaction.Setup(t => t.DisposeAsync())
                .Returns(ValueTask.CompletedTask);

            _sut = new Core.Services.PersistenceService(
                _mockStore.Object,
                _mockEventBus.Object,
                _mockLogger.Object,
                TimeSpan.FromMilliseconds(100))
            {
                IsBatchProcessingEnabled = false,
                ForceTransactionCreation = true
            };
        }

        public async ValueTask DisposeAsync()
        {
            await _sut.DisposeAsync();
        }

        [Fact]
        public async Task ProcessPendingEvents_ShouldMaintainAllMetadata_WhenHandlingSequentialPropertyChanges()
        {
            // Arrange
            var deviceId = Guid.NewGuid();

            // Initial device setup with one property and metadata
            var pump = new PumpDevice(deviceId, "Test Pump", 100, 0, _mockLogger.Object);

            _mockStore.Setup(s => s.LoadAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((IDictionary<string, object>?)null);

            _mockStore.Setup(s => s.LoadMetadataAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((IDictionary<string, IPropertyMetadata>?)null);

            // Set initial property with metadata
            var flowRateMetadata = new PropertyMetadata(true, true, "Flow Rate", "The percentage of pump flow rate");
            await pump.SetPropertyAsync("FlowRate", 50, flowRateMetadata);

            // Register the device
            await _sut.AddOrUpdateAsync(pump);

            // Act - Set a second property with different metadata
            var currentFlowRateMetadata = new PropertyMetadata(false, true, "Current Flow Rate", "The actual measured flow rate");
            var propertyEvent = new HydroGardenPropertyChangedEvent(
                deviceId, deviceId, "CurrentFlowRate", typeof(double), null, 48.5, currentFlowRateMetadata);

            await _sut.HandleEventAsync(pump, propertyEvent, CancellationToken.None);
            await _sut.ProcessPendingEventsAsync();

            // Assert - Both metadata entries should be preserved
            _capturedMetadata.Should().ContainKey("FlowRate");
            _capturedMetadata.Should().ContainKey("CurrentFlowRate");
            _capturedMetadata["FlowRate"].DisplayName.Should().Be("Flow Rate");
            _capturedMetadata["CurrentFlowRate"].DisplayName.Should().Be("Current Flow Rate");
        }

        [Fact]
        public async Task VerifyEndToEndMetadataPersistence_WithRealStore()
        {
            // Arrange - Create a real JsonStore in memory
            var testDir = Path.Combine(Path.GetTempPath(), "HydroGardenTestStore", Guid.NewGuid().ToString());
            Directory.CreateDirectory(testDir);

            try
            {
                var store = new JsonStore(testDir, _mockLogger.Object);
                var persistenceService = new Core.Services.PersistenceService(
                    store, _mockEventBus.Object, _mockLogger.Object);

                var deviceId = Guid.NewGuid();
                var pump = new PumpDevice(deviceId, "Test Pump", 100, 0, _mockLogger.Object);

                // Set FlowRate with metadata
                var flowRateMetadata = new PropertyMetadata(true, true, "Flow Rate", "The percentage of pump flow rate");
                await pump.SetPropertyAsync("FlowRate", 50.0, flowRateMetadata);

                // Register the device - this will initialize it
                await persistenceService.AddOrUpdateAsync(pump);

                // Now update a different property
                var currentFlowRateMetadata = new PropertyMetadata(false, true, "Current Flow Rate", "The actual measured flow rate");
                var propertyEvent = new HydroGardenPropertyChangedEvent(
                    deviceId, deviceId, "CurrentFlowRate", typeof(double), null, 48.5, currentFlowRateMetadata);

                await persistenceService.HandleEventAsync(pump, propertyEvent, CancellationToken.None);
                await persistenceService.ProcessPendingEventsAsync();

                // Load the metadata directly from the store to see what was actually saved
                var loadedMetadata = await store.LoadMetadataAsync(deviceId);

                // Assert - Verify that both metadata entries are preserved
                loadedMetadata.Should().NotBeNull();
                loadedMetadata.Should().ContainKey("FlowRate");
                loadedMetadata.Should().ContainKey("CurrentFlowRate");

                loadedMetadata["FlowRate"].DisplayName.Should().Be("Flow Rate");
                loadedMetadata["CurrentFlowRate"].DisplayName.Should().Be("Current Flow Rate");

                // Clean up
                await persistenceService.DisposeAsync();
            }
            finally
            {
                // Clean up
                if (Directory.Exists(testDir))
                    Directory.Delete(testDir, true);
            }
        }

        [Fact]
        public async Task PersistenceService_ShouldStoreAllMetadata_WhenMultiplePropertiesAreUpdatedInSeparateBatches()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var pump = new PumpDevice(deviceId, "Test Pump", 100, 0, _mockLogger.Object);

            // Set up store to return empty data initially
            _mockStore.Setup(s => s.LoadAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((IDictionary<string, object>?)null);

            _mockStore.Setup(s => s.LoadMetadataAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((IDictionary<string, IPropertyMetadata>?)null);

            // Register device
            await _sut.AddOrUpdateAsync(pump);

            // Let's track all metadata saved across multiple operations
            var allCapturedMetadata = new Dictionary<string, IPropertyMetadata>();

            _mockTransaction.Setup(t => t.SaveWithMetadataAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<IDictionary<string, object>>(),
                    It.IsAny<IDictionary<string, IPropertyMetadata>>()))
                .Callback<Guid, IDictionary<string, object>, IDictionary<string, IPropertyMetadata>>(
                    (_, __, metadata) =>
                    {
                        if (metadata != null)
                        {
                            // Track metadata across all operations
                            foreach (var pair in metadata)
                            {
                                allCapturedMetadata[pair.Key] = pair.Value;
                            }

                            // Also update our regular capture for the current operation
                            _capturedMetadata = new Dictionary<string, IPropertyMetadata>(metadata);
                        }
                    })
                .Returns(Task.CompletedTask);

            // Act - First property update
            var flowRateMetadata = new PropertyMetadata(true, true, "Flow Rate", "The percentage of pump flow rate");
            var flowRateEvent = new HydroGardenPropertyChangedEvent(
                deviceId, deviceId, "FlowRate", typeof(double), null, 50.0, flowRateMetadata);

            await _sut.HandleEventAsync(pump, flowRateEvent, CancellationToken.None);
            await _sut.ProcessPendingEventsAsync();

            // Second property update
            var currentFlowRateMetadata = new PropertyMetadata(false, true, "Current Flow Rate", "The actual measured flow rate");
            var currentFlowRateEvent = new HydroGardenPropertyChangedEvent(
                deviceId, deviceId, "CurrentFlowRate", typeof(double), null, 48.5, currentFlowRateMetadata);

            await _sut.HandleEventAsync(pump, currentFlowRateEvent, CancellationToken.None);
            await _sut.ProcessPendingEventsAsync();

            // Assert - With our fix, both metadata entries should be preserved
            _capturedMetadata.Should().ContainKey("CurrentFlowRate");
            _capturedMetadata.Should().ContainKey("FlowRate");

            // Both metadata entries should be in the total captured metadata
            allCapturedMetadata.Should().ContainKey("FlowRate");
            allCapturedMetadata.Should().ContainKey("CurrentFlowRate");
        }

        [Fact]
        public async Task State_PropertyMetadata_ShouldBeNonEditable()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var pump = new PumpDevice(deviceId, "Test Pump", 100, 0, _mockLogger.Object);

            // Act
            await pump.InitializeAsync();

            // Get the metadata for the State property
            var stateMetadata = pump.GetPropertyMetadata("State");

            // Assert
            stateMetadata.Should().NotBeNull();
            stateMetadata.IsEditable.Should().BeFalse("State property should not be editable");
            stateMetadata.DisplayName.Should().Be("Device State");
        }

        [Fact]
        public async Task ProcessPendingEvents_ShouldPreserveAllMetadata_EvenForPropertiesNotUpdatedInCurrentBatch()
        {
            // Arrange
            var deviceId = Guid.NewGuid();

            // Set up the store to return existing properties and metadata
            var existingProperties = new Dictionary<string, object>
            {
                { "FlowRate", 50.0 },
                { "MaxFlowRate", 100.0 }
            };

            var existingMetadata = new Dictionary<string, IPropertyMetadata>
            {
                { "FlowRate", new PropertyMetadata(true, true, "Flow Rate", "The percentage of pump flow rate") },
                { "MaxFlowRate", new PropertyMetadata(false, true, "Max Flow Rate", "Maximum flow rate") }
            };

            _mockStore.Setup(s => s.LoadAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingProperties);

            _mockStore.Setup(s => s.LoadMetadataAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingMetadata);

            var pump = new PumpDevice(deviceId, "Test Pump", 100, 0, _mockLogger.Object);
            await _sut.AddOrUpdateAsync(pump);

            // Clear the captured metadata to ensure we only capture the next batch
            _capturedMetadata.Clear();

            // Act - Update only CurrentFlowRate
            var currentFlowRateMetadata = new PropertyMetadata(false, true, "Current Flow Rate", "The actual measured flow rate");
            var propertyEvent = new HydroGardenPropertyChangedEvent(
                deviceId, deviceId, "CurrentFlowRate", typeof(double), null, 48.5, currentFlowRateMetadata);

            await _sut.HandleEventAsync(pump, propertyEvent, CancellationToken.None);
            await _sut.ProcessPendingEventsAsync();

            // Assert - With the fix, all metadata should be preserved, including FlowRate
            _capturedMetadata.Should().ContainKey("CurrentFlowRate");
            _capturedMetadata.Should().ContainKey("FlowRate");
            _capturedMetadata.Should().ContainKey("MaxFlowRate");
        }

        [Fact]
        public async Task AddOrUpdateAsync_ShouldPreserveExistingMetadata_WhenUpdatingDeviceProperties()
        {
            // Arrange
            var deviceId = Guid.NewGuid();

            // Initial device setup
            var initialProperties = new Dictionary<string, object>
            {
                { "FlowRate", 50.0 },
                { "IsRunning", false }
            };

            var initialMetadata = new Dictionary<string, IPropertyMetadata>
            {
                { "FlowRate", new PropertyMetadata(true, true, "Flow Rate", "Initial flow rate description") }
            };

            _mockStore.Setup(s => s.LoadAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(initialProperties);

            _mockStore.Setup(s => s.LoadMetadataAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(initialMetadata);

            var pump = new PumpDevice(deviceId, "Test Pump", 100, 0, _mockLogger.Object);

            // Act
            await _sut.AddOrUpdateAsync(pump);

            // Now update the pump with new metadata
            var newFlowRateMetadata = new PropertyMetadata(true, true, "Updated Flow Rate", "Updated description");
            await pump.SetPropertyAsync("FlowRate", 75.0, newFlowRateMetadata);

            // Process the change
            await _sut.ProcessPendingEventsAsync();

            // Assert
            _capturedMetadata.Should().ContainKey("FlowRate");
            _capturedMetadata["FlowRate"].DisplayName.Should().Be("Updated Flow Rate");
            _capturedMetadata["FlowRate"].Description.Should().Be("Updated description");
        }


    }
}