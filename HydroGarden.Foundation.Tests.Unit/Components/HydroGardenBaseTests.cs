using FluentAssertions;
using HydroGarden.Foundation.Abstractions.Interfaces;
using HydroGarden.Foundation.Abstractions.Interfaces.Components;
using HydroGarden.Foundation.Abstractions.Interfaces.Logging;
using HydroGarden.Foundation.Common.PropertyMetadata;
using HydroGarden.Foundation.Core.Components;
using Moq;
using System.Xml.Linq;
using Xunit;

namespace HydroGarden.Foundation.Tests.Unit.Components
{
    public class HydroGardenComponentBaseTests
    {
        private class TestComponent : HydroGardenComponentBase
        {
            public TestComponent(Guid id, string name, IHydroGardenLogger logger)
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
            _sut.Id.Should().Be(_testId);
            _sut.Name.Should().Be(_testName);
            Type type = typeof(TestComponent);
            _sut.AssemblyType.Should().Be(type.FullName);
            _sut.State.Should().Be(ComponentState.Created);
        }

        [Fact]
        public async Task SetPropertyAsync_ShouldStorePropertyValue()
        {
            string propertyName = "TestProperty";
            string propertyValue = "Test Value";
            await _sut.SetPropertyAsync(propertyName, propertyValue);
            var result = await _sut.GetPropertyAsync<string>(propertyName);
            result.Should().Be(propertyValue);
        }

        [Fact]
        public async Task SetPropertyAsync_ShouldStorePropertyMetadata()
        {
            string propertyName = "TestProperty";
            string propertyValue = "Test Value";
            string displayName = "Test Display Name";
            string description = "Test Description";
            bool isEditable = true;
            bool isVisible = true;
            var metadata = new PropertyMetadata(isEditable, isVisible, displayName, description);
            await _sut.SetPropertyAsync(propertyName, propertyValue, metadata);
            var storedMetadata = _sut.GetPropertyMetadata(propertyName);
            storedMetadata.Should().NotBeNull();
            storedMetadata.DisplayName.Should().Be(displayName);
            storedMetadata.Description.Should().Be(description);
            storedMetadata.IsEditable.Should().Be(isEditable);
            storedMetadata.IsVisible.Should().Be(isVisible);
        }

        [Fact]
        public async Task SetPropertyAsync_ShouldRaisePropertyChangedEvent()
        {
            string propertyName = "TestProperty";
            string propertyValue = "Test Value";

            _mockEventHandler
                .Setup(e => e.HandleEventAsync(
                    It.IsAny<object>(),
                    It.IsAny<IHydroGardenPropertyChangedEvent>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            await _sut.SetPropertyAsync(propertyName, propertyValue);

            _mockEventHandler.Verify(e => e.HandleEventAsync(
                It.Is<object>(o => o == _sut),
                It.Is<IHydroGardenPropertyChangedEvent>(evt =>
                    evt.DeviceId == _testId &&
                    evt.PropertyName == propertyName &&
                    (evt.NewValue != null ? evt.NewValue.ToString() : string.Empty) == propertyValue), // ✅ Single expression
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UpdatePropertyOptimisticAsync_ShouldUpdateValueAndRaiseEvent()
        {
            // Arrange
            string propertyName = "TestProperty";
            int initialValue = 5;
            await _sut.SetPropertyAsync(propertyName, initialValue);

            _mockEventHandler
                .Setup(e => e.HandleEventAsync(
                    It.IsAny<object>(),
                    It.IsAny<IHydroGardenPropertyChangedEvent>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _sut.UpdatePropertyOptimisticAsync<int>(propertyName, current => current + 10);

            // Assert
            result.Should().BeTrue("the update should succeed");
            var updatedValue = await _sut.GetPropertyAsync<int>(propertyName);
            updatedValue.Should().Be(15);

            _mockEventHandler.Verify(e => e.HandleEventAsync(
                It.Is<object>(o => o == _sut),
                It.Is<IHydroGardenPropertyChangedEvent>(evt =>
                    evt.DeviceId == _testId &&
                    evt.PropertyName == propertyName &&
                    evt.OldValue is int && (int)evt.OldValue == 5 &&
                    evt.NewValue is int && (int)evt.NewValue == 15),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UpdatePropertyOptimisticAsync_ForNonExistentProperty_ShouldAddProperty()
        {
            // Arrange
            string propertyName = "NewProperty";

            _mockEventHandler
                .Setup(e => e.HandleEventAsync(
                    It.IsAny<object>(),
                    It.IsAny<IHydroGardenPropertyChangedEvent>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _sut.UpdatePropertyOptimisticAsync<string>(propertyName, _ => "New Value");

            // Assert
            result.Should().BeTrue("the update should succeed");
            var value = await _sut.GetPropertyAsync<string>(propertyName);
            value.Should().Be("New Value");

            _mockEventHandler.Verify(e => e.HandleEventAsync(
                It.Is<object>(o => o == _sut),
                It.Is<IHydroGardenPropertyChangedEvent>(evt =>
                    evt.DeviceId == _testId &&
                    evt.PropertyName == propertyName &&
                    evt.NewValue is string && (string)evt.NewValue == "New Value"),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetPropertyAsync_NonExistentProperty_ShouldReturnDefault()
        {
            string nonExistentProperty = "NonExistentProperty";
            var result = await _sut.GetPropertyAsync<string>(nonExistentProperty);
            result.Should().BeNull();
        }

        [Fact]
        public async Task GetPropertyAsync_WrongType_ShouldReturnDefault()
        {
            string propertyName = "TestProperty";
            string propertyValue = "Test Value";
            await _sut.SetPropertyAsync(propertyName, propertyValue);
            var result = await _sut.GetPropertyAsync<int>(propertyName);
            result.Should().Be(default(int));
        }

        [Fact]
        public async Task GetProperties_ShouldReturnAllProperties()
        {
            await _sut.SetPropertyAsync("Property1", "Value1");
            await _sut.SetPropertyAsync("Property2", 42);
            await _sut.SetPropertyAsync("Property3", true);
            var properties = _sut.GetProperties();
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
            var metadata1 = new PropertyMetadata(true, true, "Display1", "Description1");
            var metadata2 = new PropertyMetadata(false, true, "Display2", "Description2");

            await _sut.SetPropertyAsync("Property1", "Value1", metadata1);
            await _sut.SetPropertyAsync("Property2", 42, metadata2);

            var metadata = _sut.GetAllPropertyMetadata();
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
            await _sut.SetPropertyAsync("Property1", "Original Value");
            var properties = new Dictionary<string, object>
            {
                { "Property1", "Updated Value" },
                { "Property2", 42 }
            };
            await _sut.LoadPropertiesAsync(properties);
            var loadedValue1 = await _sut.GetPropertyAsync<string>("Property1");
            var loadedValue2 = await _sut.GetPropertyAsync<int>("Property2");
            loadedValue1.Should().Be("Updated Value");
            loadedValue2.Should().Be(42);
        }

        [Fact]
        public async Task LoadPropertiesAsync_WithMetadata_ShouldLoadBothPropertiesAndMetadata()
        {
            var properties = new Dictionary<string, object>
            {
                { "Property1", "Value1" },
                { "Property2", 42 }
            };
            var metadata = new Dictionary<string, IPropertyMetadata>
            {
                {
                    "Property1",
                    new Common.PropertyMetadata.PropertyMetadata(
                        true, true, "Display1", "Description1")
                },
                {
                    "Property2",
                    new Common.PropertyMetadata.PropertyMetadata(
                        false, true, "Display2", "Description2")
                }
            };
            await _sut.LoadPropertiesAsync(properties, metadata);
            var loadedValue1 = await _sut.GetPropertyAsync<string>("Property1");
            var loadedValue2 = await _sut.GetPropertyAsync<int>("Property2");
            var loadedMetadata1 = _sut.GetPropertyMetadata("Property1");
            var loadedMetadata2 = _sut.GetPropertyMetadata("Property2");
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
            _sut.Dispose();
            _sut.State.Should().Be(ComponentState.Disposed);
        }

        [Fact]
        public async Task SetPropertyAsync_NoEventHandler_ShouldLogMessage()
        {
            var component = new TestComponent(_testId, _testName, _mockLogger.Object);
            await component.SetPropertyAsync("TestProperty", "Test Value");
            _mockLogger.Verify(l => l.Log(It.IsAny<string>()), Times.Once);
        }
    }
}