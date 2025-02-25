using FluentAssertions;
using HydroGarden.Foundation.Abstractions.Interfaces;
using HydroGarden.Foundation.Common.PropertyMetadata;
using HydroGarden.Foundation.Core.EventHandlers;
using HydroGarden.Foundation.Core.Services;
using Moq;
using Xunit;

namespace HydroGarden.Foundation.Tests.Unit.Services
{
    public class PersistenceServiceTests
    {
        private readonly Mock<IStore> _mockStore;
        private readonly Mock<IHydroGardenLogger> _mockLogger;
        private readonly PersistenceService _sut;

        public PersistenceServiceTests()
        {
            _mockStore = new Mock<IStore>();
            _mockLogger = new Mock<IHydroGardenLogger>();
            _sut = new PersistenceService(_mockStore.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task AddDeviceAsync_ShouldRegisterDevice()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var mockComponent = new Mock<IHydroGardenComponent>();
            mockComponent.Setup(c => c.Id).Returns(deviceId);

            // Setup store to return null (no stored properties)
            _mockStore.Setup(s => s.LoadAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Dictionary<string, object>)null);

            // Act
            await _sut.AddDeviceAsync(mockComponent.Object);

            // Assert
            // Verify the store was called to load properties
            _mockStore.Verify(s => s.LoadAsync(deviceId, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task AddDeviceAsync_WithStoredProperties_ShouldLoadProperties()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var mockComponent = new Mock<IHydroGardenComponent>();
            mockComponent.Setup(c => c.Id).Returns(deviceId);

            var storedProperties = new Dictionary<string, object>
            {
                { "Name", "Stored Device" },
                { "Value", 123.45 }
            };

            // Setup store to return stored properties
            _mockStore.Setup(s => s.LoadAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(storedProperties);

            // Act
            await _sut.AddDeviceAsync(mockComponent.Object);

            // Assert
            // Verify the store was called to load properties
            _mockStore.Verify(s => s.LoadAsync(deviceId, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task AddDeviceAsync_DuplicateDevice_ShouldThrowException()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var mockComponent = new Mock<IHydroGardenComponent>();
            mockComponent.Setup(c => c.Id).Returns(deviceId);

            // Setup store to return null (no stored properties)
            _mockStore.Setup(s => s.LoadAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Dictionary<string, object>)null);

            // Add the device once
            await _sut.AddDeviceAsync(mockComponent.Object);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _sut.AddDeviceAsync(mockComponent.Object));
        }

        [Fact]
        public async Task HandleEventAsync_ShouldSaveProperties()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var mockComponent = new Mock<IHydroGardenComponent>();
            mockComponent.Setup(c => c.Id).Returns(deviceId);

            // Setup store to return null (no stored properties)
            _mockStore.Setup(s => s.LoadAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Dictionary<string, object>)null);

            // Setup store to not throw when saving properties
            _mockStore.Setup(s => s.SaveAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<IDictionary<string, object>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Register the device
            await _sut.AddDeviceAsync(mockComponent.Object);

            // Create a property changed event
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

            // Wait for the async operation to complete
            await Task.Delay(100);

            // Assert
            // Verify the store was called to save properties
            _mockStore.Verify(s => s.SaveAsync(
                deviceId,
                It.Is<IDictionary<string, object>>(d => d.ContainsKey("TestProperty") && (string)d["TestProperty"] == "New Value"),
                It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        }

        [Fact]
        public async Task HandleEventAsync_UnregisteredDevice_ShouldLogError()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var mockComponent = new Mock<IHydroGardenComponent>();
            mockComponent.Setup(c => c.Id).Returns(deviceId);

            // Create a property changed event for an unregistered device
            var metadata = new PropertyMetadata { IsEditable = true, IsVisible = true };
            var propertyChangedEvent = new HydroGardenPropertyChangedEvent(
                Guid.NewGuid(),
                "TestProperty",
                typeof(string),
                null,
                "New Value",
                metadata);

            // Act
            await _sut.HandleEventAsync(mockComponent.Object, propertyChangedEvent, CancellationToken.None);

            // Wait for the async operation to complete
            await Task.Delay(100);

            // Assert
            // Verify the store was NOT called to save properties
            _mockStore.Verify(s => s.SaveAsync(
                It.IsAny<Guid>(),
                It.IsAny<IDictionary<string, object>>(),
                It.IsAny<CancellationToken>()), Times.Never);

            // Verify that an error was logged
            _mockLogger.Verify(l => l.Log(It.IsAny<string>()), Times.AtLeastOnce);
        }

        [Fact]
        public async Task GetPropertyAsync_RegisteredDevice_ShouldReturnProperty()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var mockComponent = new Mock<IHydroGardenComponent>();
            mockComponent.Setup(c => c.Id).Returns(deviceId);

            // Setup store to return null (no stored properties)
            _mockStore.Setup(s => s.LoadAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Dictionary<string, object>)null);

            // Register the device
            await _sut.AddDeviceAsync(mockComponent.Object);

            // Create a property changed event
            var metadata = new PropertyMetadata { IsEditable = true, IsVisible = true };
            var propertyChangedEvent = new HydroGardenPropertyChangedEvent(
                deviceId,
                "TestProperty",
                typeof(string),
                null,
                "New Value",
                metadata);

            // Process the event
            await _sut.HandleEventAsync(mockComponent.Object, propertyChangedEvent, CancellationToken.None);

            // Wait for the async operation to complete
            await Task.Delay(100);

            // Act
            var property = await _sut.GetPropertyAsync<string>(deviceId, "TestProperty");

            // Assert
            property.Should().Be("New Value");
        }

        [Fact]
        public async Task GetPropertyAsync_UnregisteredDevice_ShouldReturnDefault()
        {
            // Arrange
            var unregisteredDeviceId = Guid.NewGuid();

            // Act
            var property = await _sut.GetPropertyAsync<string>(unregisteredDeviceId, "TestProperty");

            // Assert
            property.Should().BeNull();
        }

        [Fact]
        public async Task GetPropertyAsync_NonExistentProperty_ShouldReturnDefault()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var mockComponent = new Mock<IHydroGardenComponent>();
            mockComponent.Setup(c => c.Id).Returns(deviceId);

            // Setup store to return null (no stored properties)
            _mockStore.Setup(s => s.LoadAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Dictionary<string, object>)null);

            // Register the device
            await _sut.AddDeviceAsync(mockComponent.Object);

            // Act
            var property = await _sut.GetPropertyAsync<string>(deviceId, "NonExistentProperty");

            // Assert
            property.Should().BeNull();
        }

        [Fact]
        public async Task HandleEventAsync_StoreSaveError_ShouldLogError()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var mockComponent = new Mock<IHydroGardenComponent>();
            mockComponent.Setup(c => c.Id).Returns(deviceId);

            // Setup store to return null (no stored properties)
            _mockStore.Setup(s => s.LoadAsync(deviceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Dictionary<string, object>)null);

            // Setup store to throw an exception when saving
            _mockStore.Setup(s => s.SaveAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<IDictionary<string, object>>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidCastException("Test error"));

            // Register the device
            await _sut.AddDeviceAsync(mockComponent.Object);

            // Create a property changed event
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

            // Wait for the async operation to complete
            await Task.Delay(100);

            // Assert
            // Verify that an error was logged
            _mockLogger.Verify(l => l.Log(It.IsAny<Exception>(), It.IsAny<string>()), Times.AtLeastOnce);
        }
    }
}