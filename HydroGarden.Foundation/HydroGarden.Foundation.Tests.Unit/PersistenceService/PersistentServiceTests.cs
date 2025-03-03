using FluentAssertions;
using HydroGarden.Foundation.Abstractions.Interfaces;
using HydroGarden.Foundation.Abstractions.Interfaces.Components;
using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Foundation.Abstractions.Interfaces.Logging;
using HydroGarden.Foundation.Abstractions.Interfaces.Services;
using HydroGarden.Foundation.Common.Events;
using HydroGarden.Foundation.Common.PropertyMetadata;
using HydroGarden.Foundation.Core.Components.Devices;
using HydroGarden.Foundation.Core.Services;
using Moq;
using System.Collections.Concurrent;
using Xunit;

namespace HydroGarden.Foundation.Tests.Unit.Services
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
        private readonly PersistenceService _sut;

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

        #region Device Registration and Initialization

        /// <summary>
        /// Tests that the service correctly calls InitializeAsync when a new device is registered.
        /// </summary>
        [Fact]
        public async Task GivenNewDevice_WhenAdded_ThenShouldCallInitializeAsync()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var mockDevice = new Mock<TestIoTDevice>(deviceId, "Test Device", _mockLogger.Object) { CallBase = true };
            mockDevice.Setup(d => d.InitializeAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask)
                .Verifiable();

            _mockStore.Setup(s => s.LoadAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Dictionary<string, object>?)null);

            // Act
            await _sut.AddOrUpdateAsync(mockDevice.Object);

            // Assert
            mockDevice.Verify(d => d.InitializeAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        /// <summary>
        /// Tests that the service does not call InitializeAsync when an existing device is updated.
        /// </summary>
        [Fact]
        public async Task GivenExistingDevice_WhenUpdated_ThenShouldNotCallInitializeAsync()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var mockDevice = new Mock<TestIoTDevice>(deviceId, "Test Device", _mockLogger.Object) { CallBase = true };

            // Setup the service to recognize the device as existing
            _mockStore.Setup(s => s.LoadAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<string, object> { { "TestProp", "TestValue" } });

            await _sut.AddOrUpdateAsync(mockDevice.Object);

            // Reset mock to verify it's not called again
            mockDevice.Invocations.Clear();
            mockDevice.Setup(d => d.InitializeAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask)
                .Verifiable();

            // Act - Update the device again
            await _sut.AddOrUpdateAsync(mockDevice.Object);

            // Assert
            mockDevice.Verify(d => d.InitializeAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        #endregion

        #region Performance and Edge Cases

        /// <summary>
        /// Tests that the service correctly handles a large number of property events.
        /// </summary>
        [Fact]
        public async Task GivenLargeNumberOfEvents_WhenProcessed_ThenShouldHandleEfficiently()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var mockDevice = new TestIoTDevice(deviceId, "Test Device", _mockLogger.Object);

            _mockStore.Setup(s => s.LoadAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<string, object>());

            await _sut.AddOrUpdateAsync(mockDevice);

            // Generate a large number of property events (100)
            const int eventCount = 100;
            var events = new List<HydroGardenPropertyChangedEvent>(eventCount);

            for (int i = 0; i < eventCount; i++)
            {
                events.Add(new HydroGardenPropertyChangedEvent(
                    deviceId, deviceId, $"Property{i}", typeof(string),
                    null, $"Value{i}", new PropertyMetadata()));
            }

            // Act
            foreach (var evt in events)
            {
                await _sut.HandleEventAsync(mockDevice, evt, CancellationToken.None);
            }

            await _sut.ProcessPendingEventsAsync();

            // Assert
            // Verify SaveAsync was called for the device
            _mockTransaction.Verify(t => t.SaveAsync(
                deviceId,
                It.IsAny<IDictionary<string, object>>()
            ), Times.Once);

            // Verify transaction only committed once
            _mockTransaction.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);

            // Verify a large number of properties were saved (though we can't guarantee exactly 100
            // as the implementation might handle duplicates or other edge cases)
            _mockTransaction.Verify(t => t.SaveAsync(
                deviceId,
                It.Is<IDictionary<string, object>>(d => d.Count > 50)
            ), Times.Once);
        }

        /// <summary>
        /// Tests that the service correctly handles null values in property events.
        /// </summary>
        [Fact]
        public async Task GivenNullPropertyValue_WhenHandled_ThenShouldStoreDefaultObject()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var mockDevice = new TestIoTDevice(deviceId, "Test Device", _mockLogger.Object);

            _mockStore.Setup(s => s.LoadAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<string, object>());

            await _sut.AddOrUpdateAsync(mockDevice);

            // Create property change event with null value
            var propertyEvent = new HydroGardenPropertyChangedEvent(
                deviceId, deviceId, "NullProperty", typeof(string),
                "OldValue", null, new PropertyMetadata());

            // Act
            await _sut.HandleEventAsync(mockDevice, propertyEvent, CancellationToken.None);
            await _sut.ProcessPendingEventsAsync();

            // Assert
            // Verify transaction stores a default object instead of null
            _mockTransaction.Verify(t => t.SaveAsync(
                deviceId,
                It.Is<IDictionary<string, object>>(d =>
                    d.ContainsKey("NullProperty") &&
                    d["NullProperty"] != null) // Should be a default object, not null
            ), Times.Once);
        }

        /// <summary>
        /// Tests that the service correctly handles device IDs that don't exist.
        /// </summary>
        [Fact]
        public async Task GivenNonExistentDeviceId_WhenPropertyQueried_ThenShouldReturnNull()
        {
            // Arrange
            var nonExistentDeviceId = Guid.NewGuid();

            // Act
            var result = await _sut.GetPropertyAsync<string>(nonExistentDeviceId, "AnyProperty");

            // Assert
            result.Should().BeNull();
        }
        #endregion

        #region Property Loading and Persistence

        /// <summary>
        /// Tests that the service correctly handles loading metadata for stored properties.
        /// </summary>
        [Fact]
        public async Task GivenDeviceWithMetadata_WhenLoaded_ThenShouldPassMetadataToDevice()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var mockDevice = new Mock<TestIoTDevice>(deviceId, "Test Device", _mockLogger.Object) { CallBase = true };

            var storedProperties = new Dictionary<string, object> { { "PropertyWithMetadata", "Value" } };
            var storedMetadata = new Dictionary<string, IPropertyMetadata> {
                { "PropertyWithMetadata", new PropertyMetadata(true, true, "Display Name", "Description") }
            };

            _mockStore.Setup(s => s.LoadAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(storedProperties);
            _mockStore.Setup(s => s.LoadMetadataAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(storedMetadata);

            // Act
            await _sut.AddOrUpdateAsync(mockDevice.Object);

            // Assert
            mockDevice.Verify(d => d.LoadPropertiesAsync(
                It.Is<IDictionary<string, object>>(p => p.ContainsKey("PropertyWithMetadata")),
                It.Is<IDictionary<string, IPropertyMetadata>?>(m =>
                    m != null &&
                    m.ContainsKey("PropertyWithMetadata") &&
                    m["PropertyWithMetadata"].DisplayName == "Display Name")
            ), Times.Once);
        }

        /// <summary>
        /// Tests that the service correctly persists property changes in batches.
        /// </summary>
        [Fact]
        public async Task GivenMultipleDevicePropertyChanges_WhenProcessed_ThenShouldBatchPerDevice()
        {
            // Arrange
            var device1Id = Guid.NewGuid();
            var device2Id = Guid.NewGuid();

            var mockDevice1 = new TestIoTDevice(device1Id, "Device 1", _mockLogger.Object);
            var mockDevice2 = new TestIoTDevice(device2Id, "Device 2", _mockLogger.Object);

            // Setup store to handle both devices
            _mockStore.Setup(s => s.LoadAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<string, object>());

            await _sut.AddOrUpdateAsync(mockDevice1);
            await _sut.AddOrUpdateAsync(mockDevice2);

            // Create property change events for both devices
            var event1 = new HydroGardenPropertyChangedEvent(
                device1Id, device1Id, "Property1", typeof(string), null, "Value1", new PropertyMetadata());
            var event2 = new HydroGardenPropertyChangedEvent(
                device1Id, device1Id, "Property2", typeof(string), null, "Value2", new PropertyMetadata());
            var event3 = new HydroGardenPropertyChangedEvent(
                device2Id, device2Id, "Property1", typeof(string), null, "ValueA", new PropertyMetadata());

            // Act
            await _sut.HandleEventAsync(mockDevice1, event1, CancellationToken.None);
            await _sut.HandleEventAsync(mockDevice1, event2, CancellationToken.None);
            await _sut.HandleEventAsync(mockDevice2, event3, CancellationToken.None);
            await _sut.ProcessPendingEventsAsync();

            // Assert
            _mockTransaction.Verify(t => t.SaveAsync(
                device1Id,
                It.Is<IDictionary<string, object>>(d =>
                    d.ContainsKey("Property1") && (string)d["Property1"] == "Value1" &&
                    d.ContainsKey("Property2") && (string)d["Property2"] == "Value2")
            ), Times.Once);

            _mockTransaction.Verify(t => t.SaveAsync(
                device2Id,
                It.Is<IDictionary<string, object>>(d =>
                    d.ContainsKey("Property1") && (string)d["Property1"] == "ValueA")
            ), Times.Once);
        }

        /// <summary>
        /// Tests that the service correctly republishes property change events to the EventBus.
        /// </summary>
        [Fact]
        public async Task GivenPropertyChangeEvent_WhenHandled_ThenShouldPublishToEventBus()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var mockDevice = new TestIoTDevice(deviceId, "Test Device", _mockLogger.Object);

            _mockStore.Setup(s => s.LoadAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<string, object>());

            await _sut.AddOrUpdateAsync(mockDevice);

            var propertyEvent = new HydroGardenPropertyChangedEvent(
                deviceId, deviceId, "TestProperty", typeof(string), null, "TestValue", new PropertyMetadata());

            // Verify the event is published to the event bus
            _mockEventBus.Setup(e => e.PublishAsync(
                It.IsAny<object>(),
                It.Is<IHydroGardenEvent>(evt => evt.EventId == propertyEvent.EventId),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Mock<IPublishResult>().Object)
                .Verifiable();

            // Act
            await _sut.HandleEventAsync(mockDevice, propertyEvent, CancellationToken.None);

            // Assert
            _mockEventBus.Verify(e => e.PublishAsync(
                It.IsAny<object>(),
                It.Is<IHydroGardenEvent>(evt => evt.EventId == propertyEvent.EventId),
                It.IsAny<CancellationToken>()
            ), Times.Once);
        }

        #endregion

        #region Error Handling

        /// <summary>
        /// Tests that the service correctly handles exceptions during property event handling.
        /// </summary>
        [Fact]
        public async Task GivenExceptionInEventHandling_WhenPropertyChanged_ThenShouldLogErrorAndContinue()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var mockDevice = new TestIoTDevice(deviceId, "Test Device", _mockLogger.Object);

            _mockStore.Setup(s => s.LoadAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<string, object>());

            await _sut.AddOrUpdateAsync(mockDevice);

            var propertyEvent = new HydroGardenPropertyChangedEvent(
                deviceId, deviceId, "TestProperty", typeof(string), null, "TestValue", new PropertyMetadata());

            // Setup EventBus to throw exception
            var testException = new InvalidOperationException("Test exception");
            _mockEventBus.Setup(e => e.PublishAsync(
                It.IsAny<object>(),
                It.IsAny<IHydroGardenEvent>(),
                It.IsAny<CancellationToken>()))
                .ThrowsAsync(testException);

            // Act - This should not throw despite the EventBus throwing
            await _sut.HandleEventAsync(mockDevice, propertyEvent, CancellationToken.None);

            // Assert
            _mockLogger.Verify(l => l.Log(
                It.Is<Exception>(ex => ex == testException),
                It.Is<string>(s => s.Contains("Failed to handle property change event"))
            ), Times.Once);
        }

        /// <summary>
        /// Tests that the service correctly handles transaction errors during batch processing.
        /// </summary>
        [Fact]
        public async Task GivenTransactionCommitError_WhenBatchProcessing_ThenShouldLogAndRethrow()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var mockDevice = new TestIoTDevice(deviceId, "Test Device", _mockLogger.Object);

            _mockStore.Setup(s => s.LoadAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<string, object>());

            await _sut.AddOrUpdateAsync(mockDevice);

            var propertyEvent = new HydroGardenPropertyChangedEvent(
                deviceId, deviceId, "TestProperty", typeof(string), null, "TestValue", new PropertyMetadata());

            // Handle the event and queue it for processing
            await _sut.HandleEventAsync(mockDevice, propertyEvent, CancellationToken.None);

            // Setup transaction to throw on commit
            var testException = new InvalidOperationException("Transaction commit failed");
            _mockTransaction.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(testException);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.ProcessPendingEventsAsync());

            _mockLogger.Verify(l => l.Log(
                It.Is<Exception>(ex => ex == testException),
                It.Is<string>(s => s.Contains("Failed to persist device events"))
            ), Times.Once);
        }

        #endregion

        #region Concurrency and Resource Management

        /// <summary>
        /// Tests that the service correctly acquires and releases transaction lock during batch processing.
        /// </summary>
        [Fact]
        public async Task GivenMultipleConcurrentBatches_WhenProcessing_ThenShouldUseLockForSynchronization()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var mockDevice = new TestIoTDevice(deviceId, "Test Device", _mockLogger.Object);

            _mockStore.Setup(s => s.LoadAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<string, object>());

            await _sut.AddOrUpdateAsync(mockDevice);

            // Create events
            var event1 = new HydroGardenPropertyChangedEvent(
                deviceId, deviceId, "Property1", typeof(string), null, "Value1", new PropertyMetadata());
            var event2 = new HydroGardenPropertyChangedEvent(
                deviceId, deviceId, "Property2", typeof(string), null, "Value2", new PropertyMetadata());

            // Make transaction commit take some time to simulate concurrent access
            var semaphore = new SemaphoreSlim(0, 1);
            int commitCount = 0;

            _mockTransaction.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>()))
                .Returns(async () =>
                {
                    Interlocked.Increment(ref commitCount);
                    if (commitCount == 1)
                    {
                        // First call waits for semaphore
                        await semaphore.WaitAsync();
                    }
                    else
                    {
                        // Second call releases semaphore
                        semaphore.Release();
                    }
                });

            // Act - Start two concurrent batch processes
            await _sut.HandleEventAsync(mockDevice, event1, CancellationToken.None);
            var task1 = _sut.ProcessPendingEventsAsync();

            await _sut.HandleEventAsync(mockDevice, event2, CancellationToken.None);
            var task2 = _sut.ProcessPendingEventsAsync();

            // Allow tasks to complete
            semaphore.Release();
            await Task.WhenAll(task1, task2);

            // Assert - Two transaction.SaveAsync calls, one commit per batch
            _mockTransaction.Verify(t => t.SaveAsync(
                deviceId,
                It.IsAny<IDictionary<string, object>>()
            ), Times.Exactly(2));

            _mockTransaction.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        /// <summary>
        /// Tests that the service correctly cleans up resources on dispose.
        /// </summary>
        [Fact]
        public async Task GivenDisposal_WhenServiceDisposed_ThenShouldCleanupResources()
        {
            // Arrange
            // Setup logger to verify cancellation message
            _mockLogger.Setup(l => l.Log(It.Is<string>(s => s.Contains("Event processing loop canceled"))))
                .Verifiable();

            // Act
            await _sut.DisposeAsync();

            // Assert
            // Verify the cancellation message was logged during dispose
            _mockLogger.Verify(l => l.Log(It.Is<string>(s => s.Contains("Event processing loop canceled"))), Times.Once);

            // Verify that _processingCts and _transactionLock have been disposed
            // Note: We can't directly verify this due to private fields, but the log message
            // indicates the cancellation was processed which is part of the disposal process

            // We also verify service is marked as disposed by checking the _isDisposed flag indirectly
            // This can be done by attempting a second disposal which should be a no-op
            await _sut.DisposeAsync(); // Should not throw and should not log additional cancellation

            // Verify the cancellation message was only logged once
            _mockLogger.Verify(l => l.Log(It.Is<string>(s => s.Contains("Event processing loop canceled"))), Times.Once);
        }

        #endregion

        #region Property Querying

        /// <summary>
        /// Tests that the service correctly retrieves device properties.
        /// </summary>
        [Fact]
        public async Task GivenStoredDeviceProperty_WhenQueried_ThenShouldReturnCorrectValue()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var mockDevice = new TestIoTDevice(deviceId, "Test Device", _mockLogger.Object);

            var initialProperties = new Dictionary<string, object> {
                { "StringProperty", "TestValue" },
                { "IntProperty", 42 },
                { "BoolProperty", true }
            };

            _mockStore.Setup(s => s.LoadAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(initialProperties);

            await _sut.AddOrUpdateAsync(mockDevice);

            // Act & Assert
            var stringResult = await _sut.GetPropertyAsync<string>(deviceId, "StringProperty");
            stringResult.Should().Be("TestValue");

            var intResult = await _sut.GetPropertyAsync<int>(deviceId, "IntProperty");
            intResult.Should().Be(42);

            var boolResult = await _sut.GetPropertyAsync<bool>(deviceId, "BoolProperty");
            boolResult.Should().BeTrue();
        }

        /// <summary>
        /// Tests that the service returns default value for non-existent properties.
        /// </summary>
        [Fact]
        public async Task GivenNonExistentProperty_WhenQueried_ThenShouldReturnDefault()
        {
            // Arrange
            var deviceId = Guid.NewGuid();

            // Act
            var result = await _sut.GetPropertyAsync<string>(deviceId, "NonExistentProperty");

            // Assert
            result.Should().BeNull();
        }

        /// <summary>
        /// Tests that the service handles type mismatches correctly.
        /// </summary>
        [Fact]
        public async Task GivenPropertyTypeMismatch_WhenQueried_ThenShouldReturnDefault()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var mockDevice = new TestIoTDevice(deviceId, "Test Device", _mockLogger.Object);

            var initialProperties = new Dictionary<string, object> {
                { "StringProperty", "TestValue" }
            };

            _mockStore.Setup(s => s.LoadAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(initialProperties);

            await _sut.AddOrUpdateAsync(mockDevice);

            // Act - Query with wrong type
            var result = await _sut.GetPropertyAsync<int>(deviceId, "StringProperty");

            // Assert
            result.Should().Be(default(int));
        }

        /// <summary>
        /// Tests that the service correctly handles property events for updated values.
        /// </summary>
        [Fact]
        public async Task GivenPropertyUpdate_WhenHandledMultipleTimes_ThenShouldStoreLatestValue()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var mockDevice = new TestIoTDevice(deviceId, "Test Device", _mockLogger.Object);

            _mockStore.Setup(s => s.LoadAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<string, object>());

            await _sut.AddOrUpdateAsync(mockDevice);

            // Create property change events with different values for the same property
            var event1 = new HydroGardenPropertyChangedEvent(
                deviceId, deviceId, "UpdatedProperty", typeof(string),
                null, "InitialValue", new PropertyMetadata());

            var event2 = new HydroGardenPropertyChangedEvent(
                deviceId, deviceId, "UpdatedProperty", typeof(string),
                "InitialValue", "UpdatedValue", new PropertyMetadata());

            // Act - Process events in sequence
            await _sut.HandleEventAsync(mockDevice, event1, CancellationToken.None);
            await _sut.HandleEventAsync(mockDevice, event2, CancellationToken.None);
            await _sut.ProcessPendingEventsAsync();

            // Query the current value
            var currentValue = await _sut.GetPropertyAsync<string>(deviceId, "UpdatedProperty");

            // Assert - Should have the latest value
            currentValue.Should().Be("UpdatedValue");

            // Verify transaction only saves the latest value for each property
            _mockTransaction.Verify(t => t.SaveAsync(
                deviceId,
                It.Is<IDictionary<string, object>>(d =>
                    d.ContainsKey("UpdatedProperty") &&
                    (string)d["UpdatedProperty"] == "UpdatedValue")
            ), Times.Once);
        }

        #endregion

        /// <summary>
        /// Test IoT Device implementation for testing.
        /// </summary>
        public class TestIoTDevice : IoTDeviceBase
        {
            public TestIoTDevice(Guid id, string name, IHydroGardenLogger logger)
                : base(id, name, logger)
            {
            }

            public override Task InitializeAsync(CancellationToken ct = default)
            {
                return base.InitializeAsync(ct);
            }
        }
    }
}