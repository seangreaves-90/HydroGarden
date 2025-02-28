using FluentAssertions;
using HydroGarden.Foundation.Abstractions.Interfaces;
using HydroGarden.Foundation.Common.Events;
using HydroGarden.Foundation.Common.PropertyMetadata;
using HydroGarden.Foundation.Core.Components;
using HydroGarden.Foundation.Core.Services;
using Moq;
using Xunit;
namespace HydroGarden.Foundation.Tests.Unit.Services
{
    public class PersistenceServiceTests : IAsyncDisposable
    {
        private readonly Mock<IStore> _mockStore;
        private readonly Mock<IStoreTransaction> _mockTransaction;
        private readonly Mock<IHydroGardenLogger> _mockLogger;
        private readonly PersistenceService _sut;

        public PersistenceServiceTests()
        {
            _mockStore = new Mock<IStore>();
            _mockTransaction = new Mock<IStoreTransaction>();
            _mockLogger = new Mock<IHydroGardenLogger>();

            _mockStore.Setup(s => s.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(_mockTransaction.Object);

            _mockTransaction.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _mockTransaction.Setup(t => t.DisposeAsync())
                .Returns(ValueTask.CompletedTask);

            // Use a much shorter batch interval and disable batch processing for tests
            _sut = new PersistenceService(_mockStore.Object, _mockLogger.Object, 100, TimeSpan.FromMilliseconds(100));

            // Disable automatic batch processing for tests - we'll trigger it manually
            _sut.IsBatchProcessingEnabled = false;

            // Enable force transaction creation for tests to ensure a transaction is always created
            _sut.ForceTransactionCreation = true;
        }

        public async ValueTask DisposeAsync()
        {
            await _sut.DisposeAsync();
        }

        [Fact]
        public async Task AddOrUpdateDeviceAsync_NewDevice_ShouldRegisterDevice()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var mockComponent = new TestHydroGardenComponent(deviceId, "Test Component", _mockLogger.Object);

            var properties = new Dictionary<string, object> { { "TestProp", "TestValue" } };
            mockComponent.SetupProperties(properties);

            _mockStore.Setup(s => s.LoadAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Dictionary<string, object>?)null);

            // Act
            await _sut.AddOrUpdateDeviceAsync(mockComponent);

            // Assert
            _mockStore.Verify(s => s.SaveWithMetadataAsync(
                deviceId,
                It.Is<IDictionary<string, object>>(p => p.ContainsKey("TestProp")),
                It.IsAny<IDictionary<string, IPropertyMetadata>>(),
                It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task AddOrUpdateDeviceAsync_ExistingDevice_ShouldReloadProperties()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var mockComponent = new TestHydroGardenComponent(deviceId, "Test Component", _mockLogger.Object);

            var storedProperties = new Dictionary<string, object> { { "StoredProp", "StoredValue" } };
            var storedMetadata = new Dictionary<string, IPropertyMetadata>
            {
                {
                    "StoredProp",
                    new PropertyMetadata(true, true, null, null)
                }
            };

            _mockStore.Setup(s => s.LoadAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(storedProperties);

            _mockStore.Setup(s => s.LoadMetadataAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(storedMetadata);

            // Act
            await _sut.AddOrUpdateDeviceAsync(mockComponent);

            // Assert
            mockComponent.LoadPropertiesCalled.Should().BeTrue();
            mockComponent.LoadedProperties.Should().NotBeNull();
            mockComponent.LoadedProperties.Should().ContainKey("StoredProp");
            mockComponent.LoadedProperties["StoredProp"].Should().Be("StoredValue");
        }

        [Fact]
        public async Task HandleEventAsync_ShouldBatchAndSaveProperties()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var mockComponent = new TestHydroGardenComponent(deviceId, "Test Component", _mockLogger.Object);

            mockComponent.SetupProperties(new Dictionary<string, object> { { "Name", "Test Device" } });

            _mockStore.Setup(s => s.LoadAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Dictionary<string, object>?)null);

            // Set up a verifiable setup for BeginTransactionAsync
            _mockStore.Setup(s => s.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(_mockTransaction.Object)
                .Verifiable();

            // Setup for ANY SaveAsync call (regardless of parameters) - this is more flexible
            _mockTransaction.Setup(t => t.SaveAsync(It.IsAny<Guid>(), It.IsAny<IDictionary<string, object>>()))
                .Returns(Task.CompletedTask)
                .Verifiable();

            // Setup for CommitAsync
            _mockTransaction.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask)
                .Verifiable();

            await _sut.AddOrUpdateDeviceAsync(mockComponent);

            // Verify the device was added to the service
            _sut.HasDevice(deviceId).Should().BeTrue("The device should be registered in the service");

            // Act
            var metadata = new PropertyMetadata(true, true, null, null);
            var propertyChangedEvent = new HydroGardenPropertyChangedEvent(
                deviceId,       // deviceId
                deviceId,       // sourceId - same as deviceId for test
                "TestProperty", // propertyName
                typeof(string), // propertyType
                null,           // oldValue
                "New Value",    // newValue
                metadata);      // metadata

            await _sut.HandleEventAsync(mockComponent, propertyChangedEvent, CancellationToken.None);

            // Verify immediate property update
            var propValue = await _sut.GetPropertyAsync<string>(deviceId, "TestProperty");
            propValue.Should().Be("New Value", "Property should be immediately available after HandleEventAsync");

            // Manually trigger batch processing
            await _sut.ProcessPendingEventsAsync();

            // Assert - verify the calls were made (without being too specific about parameters)
            _mockStore.Verify();
            _mockTransaction.Verify();

            // Now also verify that ANY transaction had TestProperty with "New Value"
            // We want to make sure the new property got stored somewhere, but we don't care exactly how
            _mockTransaction.Verify(
                t => t.SaveAsync(
                    It.IsAny<Guid>(),
                    It.Is<IDictionary<string, object>>(d => d.ContainsKey("TestProperty") && (string)d["TestProperty"] == "New Value")),
                Times.AtLeastOnce,
                "A transaction should save the new property value");
        }

        [Fact]
        public async Task HandleEventAsync_TransactionError_ShouldLogError()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var mockComponent = new TestHydroGardenComponent(deviceId, "Test Component", _mockLogger.Object);

            mockComponent.SetupProperties(new Dictionary<string, object> { { "Name", "Test Device" } });

            _mockStore.Setup(s => s.LoadAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Dictionary<string, object>?)null);

            await _sut.AddOrUpdateDeviceAsync(mockComponent);

            var metadata = new PropertyMetadata(true, true, null, null);
            var propertyChangedEvent = new HydroGardenPropertyChangedEvent(
                deviceId,       // deviceId
                deviceId,       // sourceId - same as deviceId for test
                "TestProperty", // propertyName
                typeof(string), // propertyType
                null,           // oldValue
                "New Value",    // newValue
                metadata);      // metadata

            var testException = new InvalidOperationException("Test exception");
            _mockTransaction.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(testException);

            // Act
            await _sut.HandleEventAsync(mockComponent, propertyChangedEvent, CancellationToken.None);

            // Manually trigger batch processing - this should throw the exception and log it
            try
            {
                await _sut.ProcessPendingEventsAsync();
            }
            catch
            {
                // Expected exception - ignore
            }

            // Assert
            _mockLogger.Verify(l => l.Log(
                It.Is<Exception>(e => e == testException || e.InnerException == testException),
                It.IsAny<string>()),
                Times.AtLeastOnce);
        }

        [Fact]
        public async Task HandleEventAsync_MultipleBatchedEvents_ShouldUseSingleTransaction()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var mockComponent = new TestHydroGardenComponent(deviceId, "Test Component", _mockLogger.Object);

            mockComponent.SetupProperties(new Dictionary<string, object> { { "Name", "Test Device" } });

            _mockStore.Setup(s => s.LoadAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<string, object>());

            await _sut.AddOrUpdateDeviceAsync(mockComponent);

            var metadata = new PropertyMetadata(true, true, null, null);
            var transactions = 0;

            _mockStore.Setup(s => s.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .Callback(() => transactions++)
                .ReturnsAsync(_mockTransaction.Object);

            // Act
            for (int i = 0; i < 5; i++)
            {
                // Fix: Use Guid for sourceId instead of string, and make sure parameters are in the right order
                var propertyChangedEvent = new HydroGardenPropertyChangedEvent(
                    deviceId,         // deviceId
                    deviceId,         // sourceId - using deviceId, not converting to string
                    $"Property{i}",   // propertyName
                    typeof(string),   // propertyType
                    null,             // oldValue
                    $"Value{i}",      // newValue
                    metadata);        // metadata

                await _sut.HandleEventAsync(mockComponent, propertyChangedEvent, CancellationToken.None);
            }

            // Manually trigger batch processing
            await _sut.ProcessPendingEventsAsync();

            // Assert
            transactions.Should().Be(1, "multiple events should be batched into a single transaction");
            _mockTransaction.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task AddOrUpdateDeviceAsync_ShouldBeCompatibleWithObsoleteMethod()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var mockComponent = new TestHydroGardenComponent(deviceId, "Test Component", _mockLogger.Object);

            mockComponent.SetupProperties(new Dictionary<string, object> { { "TestProp", "TestValue" } });

            _mockStore.Setup(s => s.LoadAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Dictionary<string, object>?)null);

            // Act - using the deprecated method
#pragma warning disable CS0618 // Type or member is obsolete
            await _sut.AddDeviceAsync(mockComponent);
#pragma warning restore CS0618 // Type or member is obsolete

            // Assert - should still call the store
            _mockStore.Verify(s => s.SaveWithMetadataAsync(
                deviceId,
                It.Is<IDictionary<string, object>>(p => p.ContainsKey("TestProp")),
                It.IsAny<IDictionary<string, IPropertyMetadata>>(),
                It.IsAny<CancellationToken>()),
                Times.Once);
        }

        // Helper class for testing
        private class TestHydroGardenComponent : HydroGardenComponentBase
        {
            public bool LoadPropertiesCalled { get; private set; }
            public IDictionary<string, object>? LoadedProperties { get; private set; }
            public IDictionary<string, IPropertyMetadata>? LoadedMetadata { get; private set; }
            private IDictionary<string, object> _properties = new Dictionary<string, object>();

            public TestHydroGardenComponent(Guid id, string name, IHydroGardenLogger logger)
                : base(id, name, logger)
            {
            }

            public void SetupProperties(IDictionary<string, object> properties)
            {
                _properties = properties;
            }

            public override IDictionary<string, object> GetProperties()
            {
                return _properties;
            }

            public override Task LoadPropertiesAsync(IDictionary<string, object> properties, IDictionary<string, IPropertyMetadata>? metadata = null)
            {
                LoadPropertiesCalled = true;
                LoadedProperties = properties;
                LoadedMetadata = metadata;
                return base.LoadPropertiesAsync(properties, metadata);
            }
        }
    }
}