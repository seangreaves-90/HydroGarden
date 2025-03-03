using FluentAssertions;
using HydroGarden.Foundation.Abstractions.Interfaces;
using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Foundation.Abstractions.Interfaces.Logging;
using HydroGarden.Foundation.Abstractions.Interfaces.Services;
using HydroGarden.Foundation.Common.Events;
using HydroGarden.Foundation.Common.PropertyMetadata;
using HydroGarden.Foundation.Core.Components.Devices;
using Moq;
using Xunit;

namespace HydroGarden.Foundation.Tests.Unit.PersistenceService
{
    /// <summary>
    /// Enhanced unit tests for the PersistenceService class.
    /// </summary>
    public class EnhancedPersistenceServiceTests : IAsyncDisposable
    {
        private readonly Mock<IStore> _mockStore;
        private readonly Mock<IStoreTransaction> _mockTransaction;
        private readonly Mock<IHydroGardenLogger> _mockLogger;
        private readonly Mock<IEventBus> _mockEventBus;
        private readonly Core.Services.PersistenceService _sut;

        public EnhancedPersistenceServiceTests()
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
            _sut = new Core.Services.PersistenceService(_mockStore.Object, _mockEventBus.Object, _mockLogger.Object, TimeSpan.FromMilliseconds(100))
            {
                IsBatchProcessingEnabled = false, // Disable automatic batch processing for tests
                ForceTransactionCreation = true  // Ensure a transaction is created in tests
            };
        }

        public async ValueTask DisposeAsync()
        {
            await _sut.DisposeAsync();
        }

        #region Device Registration and Initialization

        [Fact]
        public async Task GivenNewDevice_WhenAdded_ThenShouldCallInitializeAsync()
        {
            var deviceId = Guid.NewGuid();
            var mockDevice = new Mock<TestIoTDevice>(deviceId, "Test Device", _mockLogger.Object) { CallBase = true };
            mockDevice.Setup(d => d.InitializeAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask)
                .Verifiable();

            _mockStore.Setup(s => s.LoadAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Dictionary<string, object>?)null);

            await _sut.AddOrUpdateAsync(mockDevice.Object);

            mockDevice.Verify(d => d.InitializeAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GivenExistingDevice_WhenUpdated_ThenShouldNotCallInitializeAsync()
        {
            var deviceId = Guid.NewGuid();
            var mockDevice = new Mock<TestIoTDevice>(deviceId, "Test Device", _mockLogger.Object) { CallBase = true };

            _mockStore.Setup(s => s.LoadAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<string, object> { { "TestProp", "TestValue" } });

            await _sut.AddOrUpdateAsync(mockDevice.Object);

            mockDevice.Invocations.Clear();
            mockDevice.Setup(d => d.InitializeAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask)
                .Verifiable();

            await _sut.AddOrUpdateAsync(mockDevice.Object);

            mockDevice.Verify(d => d.InitializeAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        #endregion

        #region Property Persistence with Metadata

        [Fact]
        public async Task AddOrUpdateAsync_ShouldPersistPropertiesAndMetadata()
        {
            var deviceId = Guid.NewGuid();
            var pump = new PumpDevice(deviceId, "Test Pump", 100, 0, _mockLogger.Object);

            _mockStore.Setup(s => s.LoadAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((IDictionary<string, object>?)null);
            _mockStore.Setup(s => s.LoadMetadataAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((IDictionary<string, IPropertyMetadata>?)null);

            var expectedMetadata = new Dictionary<string, IPropertyMetadata>
            {
                ["FlowRate"] = new PropertyMetadata(true, true, "Flow Rate", "Pump flow rate in percentage")
            };

            await pump.SetPropertyAsync("FlowRate", 75, expectedMetadata["FlowRate"]);
            await _sut.AddOrUpdateAsync(pump);

            _mockStore.Verify(s => s.SaveWithMetadataAsync(
                    It.Is<Guid>(id => id == deviceId),
                    It.IsAny<IDictionary<string, object>>(),
                    It.Is<IDictionary<string, IPropertyMetadata>>(metadata =>
                        metadata.Count > 0 && metadata.ContainsKey("FlowRate")),
                    It.IsAny<CancellationToken>()),
                Times.Once);

            _mockLogger.Verify(l =>
                l.Log(It.Is<string>(msg => msg.Contains("Device") && msg.Contains("persisted with metadata"))));
        }

        [Fact]
        public async Task GivenPropertyChangeEvent_WhenHandled_ThenShouldSaveWithMetadata()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var mockDevice = new TestIoTDevice(deviceId, "Test Device", _mockLogger.Object);

            _mockStore.Setup(s => s.LoadAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<string, object>());

            await _sut.AddOrUpdateAsync(mockDevice);

            var propertyEvent = new HydroGardenPropertyChangedEvent(
                deviceId, deviceId, "TestProperty", typeof(string), null, "TestValue", new PropertyMetadata());

            await _sut.HandleEventAsync(mockDevice, propertyEvent, CancellationToken.None);
            await _sut.ProcessPendingEventsAsync();

            
            _mockTransaction.Verify(t => t.SaveWithMetadataAsync(
                deviceId,
                It.Is<IDictionary<string, object>>(d => d.ContainsKey("TestProperty")),
                It.Is<IDictionary<string, IPropertyMetadata>>(m => m.ContainsKey("TestProperty"))), Times.Once);
        }


        #endregion

        #region Error Handling

        [Fact]
        public async Task GivenTransactionCommitError_WhenBatchProcessing_ThenShouldLogAndRethrow()
        {
            var deviceId = Guid.NewGuid();
            var mockDevice = new TestIoTDevice(deviceId, "Test Device", _mockLogger.Object);

            _mockStore.Setup(s => s.LoadAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<string, object>());

            await _sut.AddOrUpdateAsync(mockDevice);

            var propertyEvent = new HydroGardenPropertyChangedEvent(
                deviceId, deviceId, "TestProperty", typeof(string), null, "TestValue", new PropertyMetadata());

            await _sut.HandleEventAsync(mockDevice, propertyEvent, CancellationToken.None);

            var testException = new InvalidOperationException("Transaction commit failed");
            _mockTransaction.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(testException);

            await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.ProcessPendingEventsAsync());

            _mockLogger.Verify(l => l.Log(
                It.Is<Exception>(ex => ex == testException),
                It.Is<string>(s => s.Contains("Failed to persist device events"))
            ), Times.Once);
        }

        #endregion

        /// <summary>
        /// Test IoT Device implementation for testing.
        /// </summary>
        public class TestIoTDevice(Guid id, string name, IHydroGardenLogger logger) : IoTDeviceBase(id, name, logger);
    }
}
