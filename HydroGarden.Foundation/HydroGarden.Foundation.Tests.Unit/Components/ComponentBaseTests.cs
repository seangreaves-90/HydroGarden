using FluentAssertions;
using HydroGarden.Foundation.Abstractions.Interfaces;
using HydroGarden.Foundation.Abstractions.Interfaces.Components;
using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.Common.PropertyMetadata;
using HydroGarden.Foundation.Core.Components;
using HydroGarden.Logger.Abstractions;
using Moq;
using Xunit;

namespace HydroGarden.Foundation.Tests.Unit.Components
{
    public class ComponentBaseTests
    {
        private class TestComponent : ComponentBase
        {
            public TestComponent(Guid id, string? name, IErrorMonitor errorMonitor, IEventBus? eventBus = null, ILogger? logger = null)
                : base(id, name, errorMonitor, eventBus, logger)
            {
            }

            // Public method to expose the protected ValidateProperty method for testing
            public bool TestValidateProperty(string name, object? value, IPropertyMetadata metadata)
            {
                return ValidateProperty(name, value, metadata);
            }
        }

        private readonly Mock<ILogger> _mockLogger;
        private readonly Mock<IPropertyChangedEventHandler> _mockEventHandler;
        private readonly Mock<IErrorMonitor> _mockErrorMonitor;
        private readonly Mock<IEventBus> _mockEventBus;
        private readonly Guid _testId;
        private readonly string? _testName;
        private readonly TestComponent _sut;

        public ComponentBaseTests()
        {
            _mockLogger = new Mock<ILogger>();
            _mockEventHandler = new Mock<IPropertyChangedEventHandler>();
            _mockErrorMonitor = new Mock<IErrorMonitor>();
            _mockEventBus = new Mock<IEventBus>();
            _testId = Guid.NewGuid();
            _testName = "Test Component";
            _sut = new TestComponent(_testId, _testName, _mockErrorMonitor.Object, _mockEventBus.Object, _mockLogger.Object);
            _sut.SetEventHandler(_mockEventHandler.Object as IPropertyChangedEventHandler<IEvent>);

            // Setup event bus for property change events
            _mockEventBus
                .Setup(eb => eb.PublishAsync(
                    It.IsAny<object>(),
                    It.IsAny<IPropertyChangedEvent>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Mock<IPublishResult>().Object);

            // Setup event bus for state change events
            _mockEventBus
                .Setup(eb => eb.PublishAsync(
                    It.IsAny<object>(),
                    It.IsAny<IStateChangeEvent>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Mock<IPublishResult>().Object);
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
        public async Task TransitionToStateAsync_WithValidTransition_ShouldSucceed()
        {
            // Arrange
            var result = await _sut.TransitionToStateAsync(ComponentState.Initializing);

            // Assert
            result.Should().BeTrue();
            _sut.State.Should().Be(ComponentState.Initializing);

            // Verify state change event was published
            _mockEventBus.Verify(e => e.PublishAsync(
                It.Is<object>(o => o == _sut),
                It.Is<IStateChangeEvent>(evt =>
                    evt.DeviceId == _testId &&
                    evt.OldState == ComponentState.Created &&
                    evt.NewState == ComponentState.Initializing),
                It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        }

        [Fact]
        public async Task TransitionToStateAsync_WithInvalidTransition_ShouldFail()
        {
            // Arrange - try to go from Created directly to Running (invalid)
            var result = await _sut.TransitionToStateAsync(ComponentState.Running);

            // Assert
            result.Should().BeFalse();
            _sut.State.Should().Be(ComponentState.Created); // State should not change

            // Verify error was reported
            _mockErrorMonitor.Verify(e => e.ReportErrorAsync(
                It.IsAny<IApplicationError>(),
                It.IsAny<CancellationToken>()), Times.AtLeastOnce);

            // We can't directly verify the extension method, but we can verify the parameters
            // were correctly passed by checking the log message
            _mockLogger.Verify(l => l.Log(It.Is<string>(s => s.Contains("Invalid state transition"))), Times.Once);
        }

        [Fact]
        public async Task PropertyValidation_WhenValidatorRegistered_ShouldValidatePropertyBeforeUpdate()
        {
            // Arrange
            string propertyName = "validatedProperty";
            bool validatorCalled = false;

            _sut.RegisterPropertyValidator(propertyName, (value, metadata) =>
            {
                validatorCalled = true;
                return value is int intValue && intValue > 0;
            });

            // Act - set a valid value
            await _sut.SetPropertyAsync(propertyName, 10);

            // Assert
            validatorCalled.Should().BeTrue();
            var storedValue = await _sut.GetPropertyAsync<int>(propertyName);
            storedValue.Should().Be(10);

            // Reset flag
            validatorCalled = false;

            // Act - try to set an invalid value
            await _sut.SetPropertyAsync(propertyName, -5);

            // Assert
            validatorCalled.Should().BeTrue();
            storedValue = await _sut.GetPropertyAsync<int>(propertyName);
            storedValue.Should().Be(10); // Value should not have changed
        }

        [Fact]
        public void TestValidateProperty_WithNoValidator_ShouldReturnTrue()
        {
            // Arrange
            string propertyName = "unvalidatedProperty";
            var metadata = new PropertyMetadata(true, true, propertyName, "Test property");

            // Act
            var result = _sut.TestValidateProperty(propertyName, "any value", metadata);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void RegisterPropertyValidator_ThenRemove_ShouldWorkCorrectly()
        {
            // Arrange
            string propertyName = "testProperty";
            bool validatorCalled = false;

            // Register validator
            _sut.RegisterPropertyValidator(propertyName, (value, metadata) =>
            {
                validatorCalled = true;
                return true;
            });

            // Act - validate with validator
            var metadata = new PropertyMetadata(true, true, propertyName, "Test property");
            var result1 = _sut.TestValidateProperty(propertyName, "test", metadata);

            // Assert
            validatorCalled.Should().BeTrue();
            result1.Should().BeTrue();

            // Act - remove validator
            var removed = _sut.RemovePropertyValidator(propertyName);

            // Assert
            removed.Should().BeTrue();

            // Reset flag
            validatorCalled = false;

            // Act - validate after removing validator
            var result2 = _sut.TestValidateProperty(propertyName, "test", metadata);

            // Assert
            validatorCalled.Should().BeFalse(); // Validator should not be called
            result2.Should().BeTrue(); // Should pass validation by default
        }

        [Fact]
        public async Task InitializeAsync_ShouldTransitionThroughCorrectStates()
        {
            // Act
            var result = await _sut.InitializeAsync();

            // Assert
            result.Should().BeTrue();
            _sut.State.Should().Be(ComponentState.Ready);

            // Verify state transitions occurred in the right order
            _mockEventBus.Verify(e => e.PublishAsync(
                It.Is<object>(o => o == _sut),
                It.Is<IStateChangeEvent>(evt =>
                    evt.OldState == ComponentState.Created &&
                    evt.NewState == ComponentState.Initializing),
                It.IsAny<CancellationToken>()), Times.AtLeastOnce);

            _mockEventBus.Verify(e => e.PublishAsync(
                It.Is<object>(o => o == _sut),
                It.Is<IStateChangeEvent>(evt =>
                    evt.OldState == ComponentState.Initializing &&
                    evt.NewState == ComponentState.Ready),
                It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        }

        [Fact]
        public async Task StartAsync_AfterInitialization_ShouldTransitionToRunning()
        {
            // Arrange
            await _sut.InitializeAsync();

            // Act
            var result = await _sut.StartAsync();

            // Assert
            result.Should().BeTrue();
            _sut.State.Should().Be(ComponentState.Running);

            // Verify state transition occurred
            _mockEventBus.Verify(e => e.PublishAsync(
                It.Is<object>(o => o == _sut),
                It.Is<IStateChangeEvent>(evt =>
                    evt.OldState == ComponentState.Ready &&
                    evt.NewState == ComponentState.Running),
                It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        }

        [Fact]
        public async Task StopAsync_WhenRunning_ShouldTransitionToReady()
        {
            // Arrange
            await _sut.InitializeAsync();
            await _sut.StartAsync();

            // Act
            var result = await _sut.StopAsync();

            // Assert
            result.Should().BeTrue();
            _sut.State.Should().Be(ComponentState.Ready);

            // Verify state transitions occurred in the right order
            _mockEventBus.Verify(e => e.PublishAsync(
                It.Is<object>(o => o == _sut),
                It.Is<IStateChangeEvent>(evt =>
                    evt.OldState == ComponentState.Running &&
                    evt.NewState == ComponentState.Stopping),
                It.IsAny<CancellationToken>()), Times.AtLeastOnce);

            _mockEventBus.Verify(e => e.PublishAsync(
                It.Is<object>(o => o == _sut),
                It.Is<IStateChangeEvent>(evt =>
                    evt.OldState == ComponentState.Stopping &&
                    evt.NewState == ComponentState.Ready),
                It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        }

        [Fact]
        public async Task HandleErrorAsync_ShouldTransitionToErrorState()
        {
            // Arrange
            var mockError = new Mock<IApplicationError>();
            mockError.Setup(e => e.DeviceId).Returns(_testId);

            // Act
            var result = await _sut.HandleErrorAsync(mockError.Object);

            // Assert
            result.Should().BeTrue();
            _sut.State.Should().Be(ComponentState.Error);

            // Verify state transition occurred
            _mockEventBus.Verify(e => e.PublishAsync(
                It.Is<object>(o => o == _sut),
                It.Is<IStateChangeEvent>(evt =>
                    evt.OldState == ComponentState.Created &&
                    evt.NewState == ComponentState.Error),
                It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        }

        [Fact]
        public async Task RecoverFromErrorAsync_ShouldReinitializeComponent()
        {
            // Arrange
            var mockError = new Mock<IApplicationError>();
            mockError.Setup(e => e.DeviceId).Returns(_testId);
            await _sut.HandleErrorAsync(mockError.Object);

            // Act
            var result = await _sut.RecoverFromErrorAsync();

            // Assert
            result.Should().BeTrue();
            _sut.State.Should().Be(ComponentState.Ready);

            // Verify reinitialize state transitions occurred
            _mockEventBus.Verify(e => e.PublishAsync(
                It.Is<object>(o => o == _sut),
                It.Is<IStateChangeEvent>(evt =>
                    evt.OldState == ComponentState.Error &&
                    evt.NewState == ComponentState.Initializing),
                It.IsAny<CancellationToken>()), Times.AtLeastOnce);

            _mockEventBus.Verify(e => e.PublishAsync(
                It.Is<object>(o => o == _sut),
                It.Is<IStateChangeEvent>(evt =>
                    evt.OldState == ComponentState.Initializing &&
                    evt.NewState == ComponentState.Ready),
                It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        }

        [Fact]
        public void Dispose_ShouldTransitionToDisposedState()
        {
            // Act
            _sut.Dispose();

            // Assert
            _sut.State.Should().Be(ComponentState.Disposed);

            // Verify state transition
            _mockEventBus.Verify(e => e.PublishAsync(
                It.Is<object>(o => o == _sut),
                It.Is<IStateChangeEvent>(evt =>
                    evt.OldState == ComponentState.Created &&
                    evt.NewState == ComponentState.Disposed),
                It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        }

        [Fact]
        public async Task LoadPropertiesAsync_EnsuresRequiredPropertiesPresent()
        {
            // Arrange
            var properties = new Dictionary<string, object?>
            {
                { "CustomProperty", "Value" }
            };

            // Act
            await _sut.LoadPropertiesAsync(properties);

            // Assert
            var allProps = _sut.GetProperties();
            allProps.Should().ContainKey("Id");
            allProps.Should().ContainKey("Name");
            allProps.Should().ContainKey("AssemblyType");
            allProps.Should().ContainKey("State");
            allProps.Should().ContainKey("CustomProperty");

            allProps["Id"].Should().Be(_testId);
            allProps["Name"].Should().Be(_testName);
            allProps["State"].Should().Be(ComponentState.Created);
            allProps["CustomProperty"].Should().Be("Value");
        }

        [Fact]
        public async Task UpdatePropertyOptimisticAsync_ValidatesValue_WhenRequested()
        {
            // Arrange
            string propertyName = "validatedProperty";
            bool validatorCalled = false;

            _sut.RegisterPropertyValidator(propertyName, (value, metadata) =>
            {
                validatorCalled = true;
                return value is int intValue && intValue > 0;
            });

            // Act - set initial value
            await _sut.SetPropertyAsync(propertyName, 5);

            // Reset flag
            validatorCalled = false;

            // Act - try update with valid value
            var result1 = await _sut.UpdatePropertyOptimisticAsync<int>(propertyName, v => v + 10);

            // Assert
            result1.Should().BeTrue();
            validatorCalled.Should().BeTrue();
            var value1 = await _sut.GetPropertyAsync<int>(propertyName);
            value1.Should().Be(15);

            // Reset flag
            validatorCalled = false;

            // Act - try update with invalid value
            var result2 = await _sut.UpdatePropertyOptimisticAsync<int>(propertyName, _ => -5);

            // Assert
            result2.Should().BeFalse();
            validatorCalled.Should().BeTrue();
            var value2 = await _sut.GetPropertyAsync<int>(propertyName);
            value2.Should().Be(15); // Value should not have changed
        }
    }
}
