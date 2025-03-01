using FluentAssertions;
using HydroGarden.Foundation.Abstractions.Interfaces;
using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Foundation.Abstractions.Interfaces.Logging;
using HydroGarden.Foundation.Abstractions.Interfaces.Services;
using HydroGarden.Foundation.Common.Events;
using HydroGarden.Foundation.Common.PropertyMetadata;
using HydroGarden.Foundation.Core.Components;
using HydroGarden.Foundation.Core.Services;
using Moq;
using Xunit;
namespace HydroGarden.Foundation.Tests.Unit.Services
{
    /// <summary>
    /// Unit tests for the PersistenceService class.
    /// </summary>
    public class PersistenceServiceTests : IAsyncDisposable
    {
        private readonly Mock<IStore> _mockStore;
        private readonly Mock<IStoreTransaction> _mockTransaction;
        private readonly Mock<IHydroGardenLogger> _mockLogger;
        private readonly Mock<IEventBus> _mockEventBus;
        private readonly PersistenceService _sut;

        public PersistenceServiceTests()
        {
            _mockStore = new Mock<IStore>();
            _mockTransaction = new Mock<IStoreTransaction>();
            _mockLogger = new Mock<IHydroGardenLogger>();
            _mockEventBus = new Mock<IEventBus>();

            _mockStore.Setup(s => s.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(_mockTransaction.Object);

            _mockTransaction.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _mockTransaction.Setup(t => t.DisposeAsync())
                .Returns(ValueTask.CompletedTask);

            // Configure test PersistenceService with a short batch interval
            _sut = new PersistenceService(_mockStore.Object, _mockEventBus.Object, _mockLogger.Object, 100, TimeSpan.FromMilliseconds(100))
            {
                IsBatchProcessingEnabled = false, // Disable automatic batch processing for tests
                ForceTransactionCreation = true  // Ensure a transaction is created in tests
            };
        }

        public async ValueTask DisposeAsync()
        {
            await _sut.DisposeAsync();
        }

        /// <summary>
        /// Ensures that a new device is correctly registered and persisted.
        /// </summary>
        [Fact]
        public async Task GivenNewDevice_WhenAdded_ThenShouldRegisterAndPersistDevice()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var mockComponent = new TestHydroGardenComponent(deviceId, "Test Component", _mockLogger.Object);
            var properties = new Dictionary<string, object> { { "TestProp", "TestValue" } };
            mockComponent.SetupProperties(properties);

            _mockStore.Setup(s => s.LoadAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Dictionary<string, object>?)null);

            // Act
            await _sut.AddOrUpdateAsync(mockComponent);

            // Assert
            _mockStore.Verify(s => s.SaveWithMetadataAsync(
                deviceId,
                It.Is<IDictionary<string, object>>(p => p.ContainsKey("TestProp")),
                It.IsAny<IDictionary<string, IPropertyMetadata>>(),
                It.IsAny<CancellationToken>()),
                Times.Once);
        }

        /// <summary>
        /// Ensures that an existing device correctly reloads properties.
        /// </summary>
        [Fact]
        public async Task GivenExistingDevice_WhenAdded_ThenShouldReloadProperties()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var mockComponent = new TestHydroGardenComponent(deviceId, "Test Component", _mockLogger.Object);

            var storedProperties = new Dictionary<string, object> { { "StoredProp", "StoredValue" } };
            var storedMetadata = new Dictionary<string, IPropertyMetadata>
            {
                { "StoredProp", new PropertyMetadata(true, true, null, null) }
            };

            _mockStore.Setup(s => s.LoadAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(storedProperties);

            _mockStore.Setup(s => s.LoadMetadataAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(storedMetadata);

            // Act
            await _sut.AddOrUpdateAsync(mockComponent);

            // Assert
            mockComponent.LoadPropertiesCalled.Should().BeTrue();
            mockComponent.LoadedProperties.Should().ContainKey("StoredProp");
            mockComponent.LoadedProperties["StoredProp"].Should().Be("StoredValue");
        }

        /// <summary>
        /// Ensures that batched property change events are saved correctly.
        /// </summary>
        [Fact]
        public async Task GivenPropertyChangeEvent_WhenProcessed_ThenShouldBatchAndSaveProperties()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var mockComponent = new TestHydroGardenComponent(deviceId, "Test Component", _mockLogger.Object);
            mockComponent.SetupProperties(new Dictionary<string, object> { { "Name", "Test Device" } });

            _mockStore.Setup(s => s.LoadAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Dictionary<string, object>?)null);

            await _sut.AddOrUpdateAsync(mockComponent);

            var propertyChangedEvent = new HydroGardenPropertyChangedEvent(
                deviceId, deviceId, "TestProperty", typeof(string), null, "New Value", new PropertyMetadata(true, true, null, null));

            // Act
            await _sut.HandleEventAsync(mockComponent, propertyChangedEvent, CancellationToken.None);
            await _sut.ProcessPendingEventsAsync();

            // Assert
            _mockStore.Verify(s => s.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
            _mockTransaction.Verify(t => t.SaveAsync(
                deviceId,
                It.Is<IDictionary<string, object>>(d => d.ContainsKey("TestProperty") && (string)d["TestProperty"] == "New Value")),
                Times.Once);
            _mockTransaction.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        /// <summary>
        /// Ensures that an event transaction failure is correctly logged.
        /// </summary>
        [Fact]
        public async Task GivenTransactionFailure_WhenProcessingEvents_ThenShouldLogError()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var mockComponent = new TestHydroGardenComponent(deviceId, "Test Component", _mockLogger.Object);
            _mockStore.Setup(s => s.LoadAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Dictionary<string, object>?)null);

            await _sut.AddOrUpdateAsync(mockComponent);

            var propertyChangedEvent = new HydroGardenPropertyChangedEvent(
                deviceId, deviceId, "TestProperty", typeof(string), null, "New Value", new PropertyMetadata(true, true, null, null));

            var testException = new InvalidOperationException("Test exception");
            _mockTransaction.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>())).ThrowsAsync(testException);

            // Act
            await _sut.HandleEventAsync(mockComponent, propertyChangedEvent, CancellationToken.None);
            Func<Task> act = async () => await _sut.ProcessPendingEventsAsync();

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Test exception");
            _mockLogger.Verify(l => l.Log(It.IsAny<Exception>(), It.IsAny<string>()), Times.AtLeastOnce);
        }

        /// <summary>
        /// Ensures that multiple property changes for a device are batched into a single transaction.
        /// </summary>
        [Fact]
        public async Task GivenMultiplePropertyChanges_WhenProcessed_ThenShouldUseSingleTransaction()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var mockComponent = new TestHydroGardenComponent(deviceId, "Test Component", _mockLogger.Object);
            _mockStore.Setup(s => s.LoadAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<string, object>());

            await _sut.AddOrUpdateAsync(mockComponent);

            // Act
            for (int i = 0; i < 5; i++)
            {
                var propertyChangedEvent = new HydroGardenPropertyChangedEvent(
                    deviceId, deviceId, $"Property{i}", typeof(string), null, $"Value{i}", new PropertyMetadata(true, true, null, null));

                await _sut.HandleEventAsync(mockComponent, propertyChangedEvent, CancellationToken.None);
            }

            await _sut.ProcessPendingEventsAsync();

            // Assert
            _mockTransaction.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
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