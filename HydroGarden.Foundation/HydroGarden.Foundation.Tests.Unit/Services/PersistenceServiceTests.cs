using HydroGarden.Foundation.Abstractions.Interfaces;
using HydroGarden.Foundation.Abstractions.Interfaces.Components;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Foundation.Abstractions.Interfaces.Services;
using HydroGarden.Foundation.Common.Events;
using HydroGarden.Foundation.Core.Services;
using HydroGarden.Logger.Abstractions;
using Moq;
using System.Reflection;
using HydroGarden.Foundation.Common.PropertyMetadata;
using Xunit;

namespace HydroGarden.Foundation.Tests.Unit.Services
{
    public class PersistenceServiceTests
    {
        private readonly Mock<IStore> _mockStore;
        private readonly Mock<IEventBus> _mockEventBus;
        private readonly Mock<ILogger> _mockLogger;
        private readonly Mock<IStoreTransaction> _mockTransaction;
        private readonly Mock<IErrorMonitor> _mockErrorMonitor;
        private readonly PersistenceService _service;

        public PersistenceServiceTests()
        {
            _mockStore = new Mock<IStore>();
            _mockEventBus = new Mock<IEventBus>();
            _mockLogger = new Mock<ILogger>();
            _mockTransaction = new Mock<IStoreTransaction>();
            _mockErrorMonitor = new Mock<IErrorMonitor>();

            _mockStore.Setup(s => s.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(_mockTransaction.Object);

            _service = new PersistenceService(
                _mockStore.Object, 
                _mockEventBus.Object, 
                _mockLogger.Object, 
                _mockErrorMonitor.Object);
        }

        [Fact]
        public async Task AddOrUpdateAsync_Should_SaveComponent_WithProperties()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var mockDevice = new Mock<IIoTDevice>();
            mockDevice.Setup(d => d.Id).Returns(deviceId);
            mockDevice.Setup(d => d.GetProperties()).Returns(new Dictionary<string, object>
            {
                ["Property1"] = "Value1"
            });
            mockDevice.Setup(d => d.GetAllPropertyMetadata()).Returns(new Dictionary<string, IPropertyMetadata>
            {
                ["Property1"] = new PropertyMetadata { IsVisible = true }
            });
            
            // Act
            await _service.AddOrUpdateAsync(mockDevice.Object);

            // Assert
            _mockStore.Verify(s => s.SaveWithMetadataAsync(
                deviceId,
                It.IsAny<IDictionary<string, object>>(),
                It.IsAny<IDictionary<string, IPropertyMetadata>>(),
                It.IsAny<CancellationToken>()),
                Times.Once);
            
            mockDevice.Verify(d => d.SetEventHandler(It.Is<IPropertyChangedEventHandler>(h => h == _service)), Times.Once);
        }

        [Fact]
        public async Task AddOrUpdateAsync_Should_LoadExistingComponent_WhenComponentExists()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var mockDevice = new Mock<IIoTDevice>();
            mockDevice.Setup(d => d.Id).Returns(deviceId);
            
            var existingProperties = new Dictionary<string, object>
            {
                ["ExistingProp"] = "ExistingValue"
            };
            
            var existingMetadata = new Dictionary<string, IPropertyMetadata>
            {
                ["ExistingProp"] = new PropertyMetadata { IsVisible = true }
            };

            _mockStore.Setup(s => s.LoadAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingProperties);
                
            _mockStore.Setup(s => s.LoadMetadataAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingMetadata);

            // Act
            await _service.AddOrUpdateAsync(mockDevice.Object);

            // Assert
            mockDevice.Verify(d => d.LoadPropertiesAsync(
                It.Is<IDictionary<string, object>>(p => p == existingProperties),
                It.Is<IDictionary<string, IPropertyMetadata>>(m => m == existingMetadata)),
                Times.Once);
            
            mockDevice.Verify(d => d.InitializeAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task AddOrUpdateAsync_Should_InitializeNewDevice_WhenDeviceIsNew()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var mockDevice = new Mock<IIoTDevice>();
            mockDevice.Setup(d => d.Id).Returns(deviceId);
            mockDevice.Setup(d => d.GetProperties()).Returns(new Dictionary<string, object>
            {
                ["Property1"] = "Value1"
            });
            mockDevice.Setup(d => d.GetAllPropertyMetadata()).Returns(new Dictionary<string, IPropertyMetadata>
            {
                ["Property1"] = new PropertyMetadata { IsVisible = true }
            });

            // Act
            await _service.AddOrUpdateAsync(mockDevice.Object);

            // Assert
            mockDevice.Verify(d => d.InitializeAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task HandleEventAsync_Should_UpdateInMemoryCacheAndPublishEvent()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var mockDevice = new Mock<IIoTDevice>();
            mockDevice.Setup(d => d.Id).Returns(deviceId);
            
            // Make sure the device is registered in the service
            mockDevice.Setup(d => d.GetProperties()).Returns(new Dictionary<string, object>());
            mockDevice.Setup(d => d.GetAllPropertyMetadata()).Returns(new Dictionary<string, IPropertyMetadata>());
            await _service.AddOrUpdateAsync(mockDevice.Object);
            
            // Create a property changed event
            var metadata = new PropertyMetadata { IsVisible = true };
            var propertyChangedEvent = new PropertyChangedEvent(deviceId, "TestProperty", "OldValue", "NewValue", metadata);

            // Act
            await _service.HandleEventAsync(mockDevice.Object, propertyChangedEvent);

            // Assert
            _mockEventBus.Verify(eb => eb.PublishAsync(
                It.Is<object>(s => s == _service),
                It.Is<IPropertyChangedEvent>(e => e == propertyChangedEvent),
                It.IsAny<CancellationToken>()),
                Times.Once);
                
            // Verify the property was saved in memory
            var propValue = await _service.GetPropertyAsync<string>(deviceId, "TestProperty");
            Assert.Equal("NewValue", propValue);
        }

        [Fact]
        public async Task ProcessPendingEventsAsync_Should_BatchAndSaveChanges()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var mockDevice = new Mock<IIoTDevice>();
            mockDevice.Setup(d => d.Id).Returns(deviceId);
            
            // Make sure the device is registered in the service
            mockDevice.Setup(d => d.GetProperties()).Returns(new Dictionary<string, object>());
            mockDevice.Setup(d => d.GetAllPropertyMetadata()).Returns(new Dictionary<string, IPropertyMetadata>());
            await _service.AddOrUpdateAsync(mockDevice.Object);
            
            // Create a property changed event
            var metadata = new PropertyMetadata { IsVisible = true };
            var propertyChangedEvent = new PropertyChangedEvent(deviceId, "TestProperty", "OldValue", "NewValue", metadata);
            
            // Add event to the service
            await _service.HandleEventAsync(mockDevice.Object, propertyChangedEvent);
            
            // Reset the verify count for the transaction
            _mockStore.Invocations.Clear();
            _mockTransaction.Invocations.Clear();

            // Act
            await _service.ProcessPendingEventsAsync();

            // Assert
            _mockStore.Verify(s => s.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
            _mockTransaction.Verify(t => t.SaveWithMetadataAsync(
                deviceId,
                It.IsAny<IDictionary<string, object>>(),
                It.IsAny<IDictionary<string, IPropertyMetadata>>()),
                Times.Once);
            _mockTransaction.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetPropertyAsync_Should_ReturnStoredPropertyValue()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var mockDevice = new Mock<IIoTDevice>();
            mockDevice.Setup(d => d.Id).Returns(deviceId);
            
            // Store a property directly in the service
            var fieldInfo = typeof(PersistenceService).GetField("_deviceProperties", BindingFlags.NonPublic | BindingFlags.Instance);
            var deviceProperties = (Dictionary<Guid, Dictionary<string, object>>)fieldInfo.GetValue(_service);
            deviceProperties[deviceId] = new Dictionary<string, object>
            {
                ["TestProperty"] = "TestValue"
            };

            // Act
            var result = await _service.GetPropertyAsync<string>(deviceId, "TestProperty");

            // Assert
            Assert.Equal("TestValue", result);
        }

        [Fact]
        public async Task StoreConnectionAsync_Should_PersistConnection()
        {
            // Arrange
            var connection = new ComponentConnection
            {
                ConnectionId = Guid.NewGuid(),
                SourceId = Guid.NewGuid(),
                TargetId = Guid.NewGuid(),
                ConnectionType = "TestType",
                IsEnabled = true
            };

            // Act
            await _service.StoreConnectionAsync(connection);

            // Assert
            _mockStore.Verify(s => s.SaveAsync(
                It.Is<Guid>(g => g == Guid.Parse("00000000-0000-0000-0000-000000000001")),
                It.IsAny<IDictionary<string, object>>(),
                It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task GetAllConnectionsAsync_Should_ReturnConnections()
        {
            // Arrange
            var connection = new ComponentConnection
            {
                ConnectionId = Guid.NewGuid(),
                SourceId = Guid.NewGuid(),
                TargetId = Guid.NewGuid(),
                ConnectionType = "TestType",
                IsEnabled = true
            };

            // Store a connection
            await _service.StoreConnectionAsync(connection);

            // Act
            var connections = await _service.GetAllConnectionsAsync();

            // Assert
            Assert.Single(connections);
            var retrievedConnection = connections.First();
            Assert.Equal(connection.ConnectionId, retrievedConnection.ConnectionId);
            Assert.Equal(connection.SourceId, retrievedConnection.SourceId);
            Assert.Equal(connection.TargetId, retrievedConnection.TargetId);
        }

        [Fact]
        public async Task DeleteConnectionAsync_Should_RemoveConnection()
        {
            // Arrange
            var connection = new ComponentConnection
            {
                ConnectionId = Guid.NewGuid(),
                SourceId = Guid.NewGuid(),
                TargetId = Guid.NewGuid(),
                ConnectionType = "TestType",
                IsEnabled = true
            };

            // Store a connection
            await _service.StoreConnectionAsync(connection);

            // Act
            var result = await _service.DeleteConnectionAsync(connection.ConnectionId);

            // Assert
            Assert.True(result);
            var connections = await _service.GetAllConnectionsAsync();
            Assert.Empty(connections);
        }

        [Fact]
        public async Task BeginTransactionAsync_Should_ReturnTransactionWithConnectionAccess()
        {
            // Arrange
            var connection = new ComponentConnection
            {
                ConnectionId = Guid.NewGuid(),
                SourceId = Guid.NewGuid(),
                TargetId = Guid.NewGuid(),
                ConnectionType = "TestType",
                IsEnabled = true
            };

            // Store a connection
            await _service.StoreConnectionAsync(connection);

            // Act
            await using var transaction = await _service.BeginTransactionAsync();

            // Store a new connection in the transaction
            var newConnection = new ComponentConnection
            {
                ConnectionId = Guid.NewGuid(),
                SourceId = Guid.NewGuid(),
                TargetId = Guid.NewGuid(),
                ConnectionType = "TestType",
                IsEnabled = true
            };

            await transaction.StoreConnectionAsync(newConnection);

            // Delete the old connection in the transaction
            var deleteResult = await transaction.DeleteConnectionAsync(connection.ConnectionId);

            // Commit the transaction
            await transaction.CommitAsync();

            // Assert
            Assert.True(deleteResult);
            var connections = await _service.GetAllConnectionsAsync();
            Assert.Single(connections);
            Assert.Equal(newConnection.ConnectionId, connections.First().ConnectionId);
        }

        [Fact]
        public async Task ErrorHandling_Should_ReportErrorOnFailure()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var mockDevice = new Mock<IIoTDevice>();
            mockDevice.Setup(d => d.Id).Returns(deviceId);
            
            // Set the mock test exception in the service using reflection
            var fieldInfo = typeof(PersistenceService).GetField("_mockTestException", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            var testException = new InvalidOperationException("Test exception");
            fieldInfo.SetValue(_service, testException);
            
            // Make the store throw an exception
            _mockStore.Setup(s => s.SaveWithMetadataAsync(
                    It.IsAny<Guid>(), 
                    It.IsAny<IDictionary<string, object>>(), 
                    It.IsAny<IDictionary<string, IPropertyMetadata>>(), 
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(testException);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(async () => 
                await _service.AddOrUpdateAsync(mockDevice.Object));
                
            // Verify error was reported
            _mockErrorMonitor.Verify(m => m.ReportExceptionAsync(
                It.IsAny<object>(),
                It.IsAny<Exception>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<ErrorSeverity>(),
                It.IsAny<ErrorSource>(),
                It.IsAny<IDictionary<string, object>>(),
                It.IsAny<CancellationToken>()),
                Times.AtLeastOnce);
        }
    }
}