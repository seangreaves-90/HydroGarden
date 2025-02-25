using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using HydroGarden.Foundation.Abstractions.Interfaces;
using HydroGarden.Foundation.Core.Components;
using Moq;
using Xunit;

namespace HydroGarden.Foundation.Tests.Unit.Components
{
    public class HydroGardenComponentBaseTests
    {
        private class TestComponent : HydroGardenComponentBase
        {
            public TestComponent(Guid id, string name, IHydroGardenLogger logger = null)
                : base(id, name, logger)
            {
            }
        }

        private readonly Mock<IHydroGardenLogger> _mockLogger;
        private readonly Mock<IHydroGardenEventHandler> _mockEventHandler;
        private readonly Guid _testId;
        private readonly string _testName;
        private readonly TestComponent _sut;

        public HydroGardenComponentBaseTests()
        {
            _mockLogger = new Mock<IHydroGardenLogger>();
            _mockEventHandler = new Mock<IHydroGardenEventHandler>();
            _testId = Guid.NewGuid();
            _testName = "Test Component";
            _sut = new TestComponent(_testId, _testName, _mockLogger.Object);
            _sut.SetEventHandler(_mockEventHandler.Object);
        }

        [Fact]
        public void Constructor_ShouldInitializePropertiesCorrectly()
        {
            // Arrange & Act - constructor called in setup

            // Assert
            _sut.Id.Should().Be(_testId);
            _sut.Name.Should().Be(_testName);
            _sut.AssemblyType.Should().Be(typeof(TestComponent));
            _sut.State.Should().Be(ComponentState.Created);
        }

        [Fact]
        public async Task SetPropertyAsync_ShouldStorePropertyValue()
        {
            // Arrange
            string propertyName = "TestProperty";
            string propertyValue = "Test Value";

            // Act
            await _sut.SetPropertyAsync(propertyName, propertyValue);
            var result = await _sut.GetPropertyAsync<string>(propertyName);

            // Assert
            result.Should().Be(propertyValue);
        }

        [Fact]
        public async Task SetPropertyAsync_ShouldStorePropertyMetadata()
        {
            // Arrange
            string propertyName = "TestProperty";
            string propertyValue = "Test Value";
            string displayName = "Test Display Name";
            string description = "Test Description";
            bool isEditable = true;
            bool isVisible = true;

            // Act
            await _sut.SetPropertyAsync(propertyName, propertyValue, isEditable, isVisible, displayName, description);
            var metadata = _sut.GetPropertyMetadata(propertyName);

            // Assert
            metadata.Should().NotBeNull();
            metadata.DisplayName.Should().Be(displayName);
            metadata.Description.Should().Be(description);
            metadata.IsEditable.Should().Be(isEditable);
            metadata.IsVisible.Should().Be(isVisible);
        }

        [Fact]
        public async Task SetPropertyAsync_ShouldRaisePropertyChangedEvent()
        {
            // Arrange
            string propertyName = "TestProperty";
            string propertyValue = "Test Value";

            _mockEventHandler
                .Setup(e => e.HandleEventAsync(
                    It.IsAny<object>(),
                    It.IsAny<IHydroGardenPropertyChangedEvent>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _sut.SetPropertyAsync(propertyName, propertyValue);

            // Assert
            _mockEventHandler.Verify(e => e.HandleEventAsync(
                It.Is<object>(o => o == _sut),
                It.Is<IHydroGardenPropertyChangedEvent>(evt =>
                    evt.DeviceId == _testId &&
                    evt.PropertyName == propertyName &&
                    evt.NewValue.ToString() == propertyValue),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetPropertyAsync_NonExistentProperty_ShouldReturnDefault()
        {
            // Arrange
            string nonExistentProperty = "NonExistentProperty";

            // Act
            var result = await _sut.GetPropertyAsync<string>(nonExistentProperty);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task GetPropertyAsync_WrongType_ShouldReturnDefault()
        {
            // Arrange
            string propertyName = "TestProperty";
            string propertyValue = "Test Value";

            // Act
            await _sut.SetPropertyAsync(propertyName, propertyValue);
            var result = await _sut.GetPropertyAsync<int>(propertyName);

            // Assert
            result.Should().Be(default(int));
        }

        [Fact]
        public async Task GetProperties_ShouldReturnAllProperties()
        {
            // Arrange
            await _sut.SetPropertyAsync("Property1", "Value1");
            await _sut.SetPropertyAsync("Property2", 42);
            await _sut.SetPropertyAsync("Property3", true);

            // Act
            var properties = _sut.GetProperties();

            // Assert
            properties.Should().NotBeNull();
            properties.Count.Should().Be(3);
            properties.Should().ContainKey("Property1");
            properties["Property1"].Should().Be("Value1");
            properties.Should().ContainKey("Property2");
            properties["Property2"].Should().Be(42);
            properties.Should().ContainKey("Property3");
            properties["Property3"].Should().Be(true);
        }

        [Fact]
        public async Task GetAllPropertyMetadata_ShouldReturnAllMetadata()
        {
            // Arrange
            await _sut.SetPropertyAsync("Property1", "Value1", true, true, "Display1", "Description1");
            await _sut.SetPropertyAsync("Property2", 42, false, true, "Display2", "Description2");

            // Act
            var metadata = _sut.GetAllPropertyMetadata();

            // Assert
            metadata.Should().NotBeNull();
            metadata.Count.Should().Be(2);
            metadata.Should().ContainKey("Property1");
            metadata["Property1"].DisplayName.Should().Be("Display1");
            metadata["Property1"].Description.Should().Be("Description1");
            metadata["Property1"].IsEditable.Should().BeTrue();
            metadata["Property1"].IsVisible.Should().BeTrue();

            metadata.Should().ContainKey("Property2");
            metadata["Property2"].DisplayName.Should().Be("Display2");
            metadata["Property2"].Description.Should().Be("Description2");
            metadata["Property2"].IsEditable.Should().BeFalse();
            metadata["Property2"].IsVisible.Should().BeTrue();
        }

        [Fact]
        public async Task LoadPropertiesAsync_ShouldOverwriteExistingProperties()
        {
            // Arrange
            await _sut.SetPropertyAsync("Property1", "Original Value");
            var properties = new Dictionary<string, object>
            {
                { "Property1", "Updated Value" },
                { "Property2", 42 }
            };

            // Act
            await _sut.LoadPropertiesAsync(properties);
            var loadedValue1 = await _sut.GetPropertyAsync<string>("Property1");
            var loadedValue2 = await _sut.GetPropertyAsync<int>("Property2");

            // Assert
            loadedValue1.Should().Be("Updated Value");
            loadedValue2.Should().Be(42);
        }

        [Fact]
        public async Task LoadPropertiesAsync_WithMetadata_ShouldLoadBothPropertiesAndMetadata()
        {
            // Arrange
            var properties = new Dictionary<string, object>
            {
                { "Property1", "Value1" },
                { "Property2", 42 }
            };

            var metadata = new Dictionary<string, IPropertyMetadata>
            {
                {
                    "Property1",
                    new HydroGarden.Foundation.Common.PropertyMetadata.PropertyMetadata
                    {
                        IsEditable = true,
                        IsVisible = true,
                        DisplayName = "Display1",
                        Description = "Description1"
                    }
                },
                {
                    "Property2",
                    new HydroGarden.Foundation.Common.PropertyMetadata.PropertyMetadata
                    {
                        IsEditable = false,
                        IsVisible = true,
                        DisplayName = "Display2",
                        Description = "Description2"
                    }
                }
            };

            // Act
            await _sut.LoadPropertiesAsync(properties, metadata);
            var loadedValue1 = await _sut.GetPropertyAsync<string>("Property1");
            var loadedValue2 = await _sut.GetPropertyAsync<int>("Property2");
            var loadedMetadata1 = _sut.GetPropertyMetadata("Property1");
            var loadedMetadata2 = _sut.GetPropertyMetadata("Property2");

            // Assert
            loadedValue1.Should().Be("Value1");
            loadedValue2.Should().Be(42);

            loadedMetadata1.Should().NotBeNull();
            loadedMetadata1.DisplayName.Should().Be("Display1");
            loadedMetadata1.Description.Should().Be("Description1");
            loadedMetadata1.IsEditable.Should().BeTrue();
            loadedMetadata1.IsVisible.Should().BeTrue();

            loadedMetadata2.Should().NotBeNull();
            loadedMetadata2.DisplayName.Should().Be("Display2");
            loadedMetadata2.Description.Should().Be("Description2");
            loadedMetadata2.IsEditable.Should().BeFalse();
            loadedMetadata2.IsVisible.Should().BeTrue();
        }

        [Fact]
        public void Dispose_ShouldSetStateToDisposed()
        {
            // Arrange & Act
            _sut.Dispose();

            // Assert
            _sut.State.Should().Be(ComponentState.Disposed);
        }

        [Fact]
        public async Task SetPropertyAsync_NoEventHandler_ShouldLogMessage()
        {
            // Arrange
            var component = new TestComponent(_testId, _testName, _mockLogger.Object);
            // Intentionally not setting an event handler

            // Act
            await component.SetPropertyAsync("TestProperty", "Test Value");

            // Assert
            _mockLogger.Verify(l => l.Log(It.IsAny<string>()), Times.Once);
        }
    }
}