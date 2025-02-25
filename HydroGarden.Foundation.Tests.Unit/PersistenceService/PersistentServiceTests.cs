using FluentAssertions;
using HydroGarden.Foundation.Abstractions.Interfaces;
using HydroGarden.Foundation.Common.PropertyMetadata;
using HydroGarden.Foundation.Core.EventHandlers;
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

            // Create the service with a short batch interval for testing
            _sut = new PersistenceService(_mockStore.Object, _mockLogger.Object, 100, TimeSpan.FromMilliseconds(100));
        }

        public async ValueTask DisposeAsync()
        {
            await _sut.DisposeAsync();
        }

        [Fact]
        public async Task AddDeviceAsync_ShouldRegisterDevice()
        {
            var deviceId = Guid.NewGuid();
            var mockComponent = new Mock<IHydroGardenComponent>();
            mockComponent.Setup(c => c.Id).Returns(deviceId);
            _mockStore.Setup(s => s.LoadAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Dictionary<string, object>)null);
            await _sut.AddDeviceAsync(mockComponent.Object);
            _mockStore.Verify(s => s.LoadAsync(deviceId, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task AddDeviceAsync_WithStoredProperties_ShouldLoadProperties()
        {
            var deviceId = Guid.NewGuid();
            var mockComponent = new Mock<IHydroGardenComponent>();
            mockComponent.Setup(c => c.Id).Returns(deviceId);
            var storedProperties = new Dictionary<string, object>
            {
                { "Name", "Stored Device" },
                { "Value", 123.45 }
            };
            _mockStore.Setup(s => s.LoadAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(storedProperties);
            await _sut.AddDeviceAsync(mockComponent.Object);
            _mockStore.Verify(s => s.LoadAsync(deviceId, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task AddDeviceAsync_DuplicateDevice_ShouldThrowException()
        {
            var deviceId = Guid.NewGuid();
            var mockComponent = new Mock<IHydroGardenComponent>();
            mockComponent.Setup(c => c.Id).Returns(deviceId);
            _mockStore.Setup(s => s.LoadAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Dictionary<string, object>)null);
            await _sut.AddDeviceAsync(mockComponent.Object);
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _sut.AddDeviceAsync(mockComponent.Object));
        }

        [Fact]
        public async Task HandleEventAsync_ShouldBatchAndSaveProperties()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var mockComponent = new Mock<IHydroGardenComponent>();
            mockComponent.Setup(c => c.Id).Returns(deviceId);
            _mockStore.Setup(s => s.LoadAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Dictionary<string, object>)null);

            _mockTransaction.Setup(t => t.SaveAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<IDictionary<string, object>>()))
                .Returns(Task.CompletedTask);

            await _sut.AddDeviceAsync(mockComponent.Object);

            var metadata = new PropertyMetadata { IsEditable = true, IsVisible = true };
            var propertyChangedEvent = new HydroGardenPropertyChangedEvent(
                deviceId,
                "TestProperty",
                typeof(string),
                null,
                "New Value",
                metadata);

            // Act
            await _sut.HandleEventAsync(mockComponent.Object, propertyChangedEvent, CancellationToken.None);

            // Allow time for batch processing
            await Task.Delay(200);

            // Assert
            _mockStore.Verify(s => s.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
            _mockTransaction.Verify(t => t.SaveAsync(
                deviceId,
                It.Is<IDictionary<string, object>>(d => d.ContainsKey("TestProperty") && (string)d["TestProperty"] == "New Value")),
                Times.AtLeastOnce);
            _mockTransaction.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        }

        [Fact]
        public async Task HandleEventAsync_UnregisteredDevice_ShouldLogError()
        {
            var deviceId = Guid.NewGuid();
            var mockComponent = new Mock<IHydroGardenComponent>();
            mockComponent.Setup(c => c.Id).Returns(deviceId);
            var metadata = new PropertyMetadata { IsEditable = true, IsVisible = true };
            var propertyChangedEvent = new HydroGardenPropertyChangedEvent(
                Guid.NewGuid(), // Different device ID than the registered one
                "TestProperty",
                typeof(string),
                null,
                "New Value",
                metadata);

            // Act
            await _sut.HandleEventAsync(mockComponent.Object, propertyChangedEvent, CancellationToken.None);

            // Allow time for batch processing
            await Task.Delay(200);

            // Assert
            _mockTransaction.Verify(t => t.SaveAsync(
                It.IsAny<Guid>(),
                It.IsAny<IDictionary<string, object>>()),
                Times.Never);
        }

        [Fact]
        public async Task GetPropertyAsync_RegisteredDevice_ShouldReturnProperty()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var mockComponent = new Mock<IHydroGardenComponent>();
            mockComponent.Setup(c => c.Id).Returns(deviceId);
            _mockStore.Setup(s => s.LoadAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<string, object>());
            await _sut.AddDeviceAsync(mockComponent.Object);

            var metadata = new PropertyMetadata { IsEditable = true, IsVisible = true };
            var propertyChangedEvent = new HydroGardenPropertyChangedEvent(
                deviceId,
                "TestProperty",
                typeof(string),
                null,
                "New Value",
                metadata);

            // Act
            await _sut.HandleEventAsync(mockComponent.Object, propertyChangedEvent, CancellationToken.None);
            var property = await _sut.GetPropertyAsync<string>(deviceId, "TestProperty");

            // Assert
            property.Should().Be("New Value");
        }

        [Fact]
        public async Task GetPropertyAsync_UnregisteredDevice_ShouldReturnDefault()
        {
            var unregisteredDeviceId = Guid.NewGuid();
            var property = await _sut.GetPropertyAsync<string>(unregisteredDeviceId, "TestProperty");
            property.Should().BeNull();
        }

        [Fact]
        public async Task GetPropertyAsync_NonExistentProperty_ShouldReturnDefault()
        {
            var deviceId = Guid.NewGuid();
            var mockComponent = new Mock<IHydroGardenComponent>();
            mockComponent.Setup(c => c.Id).Returns(deviceId);
            _mockStore.Setup(s => s.LoadAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<string, object>());
            await _sut.AddDeviceAsync(mockComponent.Object);
            var property = await _sut.GetPropertyAsync<string>(deviceId, "NonExistentProperty");
            property.Should().BeNull();
        }

        [Fact]
        public async Task HandleEventAsync_MultipleBatchedEvents_ShouldUseSingleTransaction()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var mockComponent = new Mock<IHydroGardenComponent>();
            mockComponent.Setup(c => c.Id).Returns(deviceId);
            _mockStore.Setup(s => s.LoadAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<string, object>());
            await _sut.AddDeviceAsync(mockComponent.Object);

            var metadata = new PropertyMetadata { IsEditable = true, IsVisible = true };
            var transactions = 0;

            _mockStore.Setup(s => s.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .Callback(() => transactions++)
                .ReturnsAsync(_mockTransaction.Object);

            // Act - send multiple events rapidly
            for (int i = 0; i < 5; i++)
            {
                var propertyChangedEvent = new HydroGardenPropertyChangedEvent(
                    deviceId,
                    $"Property{i}",
                    typeof(string),
                    null,
                    $"Value{i}",
                    metadata);

                await _sut.HandleEventAsync(mockComponent.Object, propertyChangedEvent, CancellationToken.None);
            }

            // Allow time for batch processing
            await Task.Delay(200);

            // Assert
            transactions.Should().BeLessThan(5, "multiple events should be batched into fewer transactions");
            _mockTransaction.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        }

        [Fact]
        public async Task HandleEventAsync_TransactionError_ShouldLogError()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var mockComponent = new Mock<IHydroGardenComponent>();
            mockComponent.Setup(c => c.Id).Returns(deviceId);
            _mockStore.Setup(s => s.LoadAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<string, object>());
            await _sut.AddDeviceAsync(mockComponent.Object);

            var metadata = new PropertyMetadata { IsEditable = true, IsVisible = true };
            var propertyChangedEvent = new HydroGardenPropertyChangedEvent(
                deviceId,
                "TestProperty",
                typeof(string),
                null,
                "New Value",
                metadata);

            _mockTransaction.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Test exception"));

            // Act
            await _sut.HandleEventAsync(mockComponent.Object, propertyChangedEvent, CancellationToken.None);

            // Allow time for batch processing
            await Task.Delay(200);

            // Assert
            _mockLogger.Verify(l => l.Log(It.IsAny<Exception>(), It.IsAny<string>()), Times.AtLeastOnce);
        }
    }
}