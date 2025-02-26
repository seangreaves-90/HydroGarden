using FluentAssertions;
using HydroGarden.Foundation.Abstractions.Interfaces;
using HydroGarden.Foundation.Core.Components.Devices;
using Moq;
using Xunit;

namespace HydroGarden.Foundation.Tests.Unit.Devices
{
    public class PumpDeviceTests
    {
        private readonly Mock<IHydroGardenLogger> _mockLogger;
        private readonly Mock<IHydroGardenEventHandler> _mockEventHandler;
        private readonly Guid _testId;
        private readonly string _testName;
        private readonly double _maxFlowRate;
        private readonly double _minFlowRate;
        private readonly PumpDevice _sut;

        public PumpDeviceTests()
        {
            _mockLogger = new Mock<IHydroGardenLogger>();
            _mockEventHandler = new Mock<IHydroGardenEventHandler>();
            _testId = Guid.NewGuid();
            _testName = "Test Pump";
            _maxFlowRate = 100.0;
            _minFlowRate = 0.0;
            _sut = new PumpDevice(_testId, _testName, _maxFlowRate, _minFlowRate, _mockLogger.Object);
            _sut.SetEventHandler(_mockEventHandler.Object);

            // Setup event handler to handle any events
            _mockEventHandler
                .Setup(e => e.HandleEventAsync(
                    It.IsAny<object>(),
                    It.IsAny<IHydroGardenPropertyChangedEvent>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        }

        [Fact]
        public async Task InitializeAsync_ShouldSetInitialProperties()
        {
            // Act
            await ((IIoTDevice)_sut).InitializeAsync();

            // Assert
            var flowRate = await _sut.GetPropertyAsync<double>("FlowRate");
            var isRunning = await _sut.GetPropertyAsync<bool>("IsRunning");
            var maxFlowRate = await _sut.GetPropertyAsync<double>("MaxFlowRate");
            var minFlowRate = await _sut.GetPropertyAsync<double>("MinFlowRate");

            flowRate.Should().Be(0.0);
            isRunning.Should().BeFalse();
            maxFlowRate.Should().Be(_maxFlowRate);
            minFlowRate.Should().Be(_minFlowRate);
        }

        [Fact]
        public async Task SetFlowRateAsync_ShouldUpdateFlowRateProperty()
        {
            // Arrange
            await ((IIoTDevice)_sut).InitializeAsync();
            double newFlowRate = 50.0;

            // Act
            await _sut.SetFlowRateAsync(newFlowRate);

            // Assert
            var flowRate = await _sut.GetPropertyAsync<double>("FlowRate");
            flowRate.Should().Be(newFlowRate);
        }

        [Fact]
        public async Task SetFlowRateAsync_FlowRateTooHigh_ShouldThrowException()
        {
            // Arrange
            await ((IIoTDevice)_sut).InitializeAsync();
            double tooHighFlowRate = _maxFlowRate + 10;

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
                await _sut.SetFlowRateAsync(tooHighFlowRate));
        }

        [Fact]
        public async Task SetFlowRateAsync_FlowRateTooLow_ShouldThrowException()
        {
            // Arrange
            await ((IIoTDevice)_sut).InitializeAsync();
            double tooLowFlowRate = _minFlowRate - 5;

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
                await _sut.SetFlowRateAsync(tooLowFlowRate));
        }

        [Fact]
        public async Task StartAsync_ShouldSetIsRunningToTrue()
        {
            // Arrange
            await ((IIoTDevice)_sut).InitializeAsync();

            // Directly capture when IsRunning is set to true
            bool isRunningSet = false;

            _mockEventHandler
                .Setup(e => e.HandleEventAsync(
                    It.IsAny<object>(),
                    It.Is<IHydroGardenPropertyChangedEvent>(evt =>
                        evt.PropertyName == "IsRunning" &&
                        evt.NewValue != null &&
                        (bool)evt.NewValue == true),
                    It.IsAny<CancellationToken>()))
                .Callback(() => isRunningSet = true)
                .Returns(Task.CompletedTask);

            // Act
            var startTask = ((IIoTDevice)_sut).StartAsync();

            // Let the tasks run briefly
            await Task.Delay(200);

            // Cleanup
            try
            {
                await ((IIoTDevice)_sut).StopAsync();
                await startTask;
            }
            catch { }

            // Assert - bypass the event checking since we often see it fail
            isRunningSet.Should().BeTrue("IsRunning property should be set to true");
            var isRunning = await _sut.GetPropertyAsync<bool>("IsRunning");
            isRunning.Should().BeFalse(); // it's false because we stopped it
        }

        [Fact]
        public async Task StartAsync_ShouldGenerateCurrentFlowRateValues()
        {
            // Arrange
            await ((IIoTDevice)_sut).InitializeAsync();
            await _sut.SetFlowRateAsync(50.0);

            // Directly capture when CurrentFlowRate is set
            double? capturedFlowRate = null;

            _mockEventHandler
                .Setup(e => e.HandleEventAsync(
                    It.IsAny<object>(),
                    It.Is<IHydroGardenPropertyChangedEvent>(evt =>
                        evt.PropertyName == "CurrentFlowRate" &&
                        evt.NewValue != null),
                    It.IsAny<CancellationToken>()))
                .Callback<object, IHydroGardenPropertyChangedEvent, CancellationToken>(
                    (_, evt, __) => capturedFlowRate = evt.NewValue as double? ?? 0.0)
                .Returns(Task.CompletedTask);

            // Act
            var startTask = ((IIoTDevice)_sut).StartAsync();

            // Give it a longer time than the test timeout
            await Task.Delay(2500);

            // Cleanup
            try
            {
                await ((IIoTDevice)_sut).StopAsync();
                await startTask;
            }
            catch { }

            // Assert for direct captured value
            capturedFlowRate.Should().NotBeNull("CurrentFlowRate property should have been set");

            if (capturedFlowRate.HasValue)
            {
                capturedFlowRate.Value.Should().BeGreaterThan(0);
                capturedFlowRate.Value.Should().BeInRange(45.0, 55.0);
            }
        }

        [Fact]
        public async Task StopAsync_ShouldSetIsRunningToFalse()
        {
            // Arrange
            await _sut.InitializeAsync();
            var startTask = _sut.StartAsync();

            // Wait for IsRunning to be set to true
            await Task.Delay(100);

            // Act
            await _sut.StopAsync();

            // Try to ensure the startTask completes
            try { await startTask; } catch { }

            // Assert
            var isRunning = await _sut.GetPropertyAsync<bool>("IsRunning");
            isRunning.Should().BeFalse();
        }

        [Fact]
        public async Task Dispose_ShouldCleanUpResources()
        {
            // Arrange
            await _sut.InitializeAsync();
            var cts = new CancellationTokenSource();
            var startTask = _sut.StartAsync(cts.Token); // Pass a CancellationToken

            await Task.Delay(100); // Give some time to start

            // Act
            cts.Cancel(); // Cancel the task
            _sut.Dispose();

            // Ensure the startTask completes without blocking indefinitely
            try { await startTask; } catch (OperationCanceledException) { }

            // Assert - verify it's disposed
            _sut.State.Should().Be(ComponentState.Disposed);
        }


        [Fact]
        public async Task ZeroFlowRate_ShouldNotGenerateActualValue()
        {
            // Arrange
            await _sut.InitializeAsync();
            await _sut.SetFlowRateAsync(0.0);

            // Setup event handler to observe CurrentFlowRate
            var currentFlowRateEvents = 0;
            _mockEventHandler
                .Setup(e => e.HandleEventAsync(
                    It.IsAny<object>(),
                    It.Is<IHydroGardenPropertyChangedEvent>(evt =>
                        evt.PropertyName == "CurrentFlowRate"),
                    It.IsAny<CancellationToken>()))
                .Callback(() => currentFlowRateEvents++)
                .Returns(Task.CompletedTask);

            // Act
            var startTask = _sut.StartAsync();

            // Allow some time for monitoring to begin
            await Task.Delay(1500);

            // Assert
            var currentFlowRate = await _sut.GetPropertyAsync<double>("CurrentFlowRate");
            currentFlowRate.Should().Be(0.0);

            // Cleanup
            await _sut.StopAsync();
            try { await startTask; } catch { }
        }

        [Fact]
        public async Task MonitorTimer_ShouldUpdateCurrentFlowRateProperty()
        {
            // Arrange
            await _sut.InitializeAsync();
            await _sut.SetFlowRateAsync(75.0);

            // Setup event handler to count CurrentFlowRate events
            var currentFlowRateEvents = 0;
            _mockEventHandler
                .Setup(e => e.HandleEventAsync(
                    It.IsAny<object>(),
                    It.Is<IHydroGardenPropertyChangedEvent>(evt =>
                        evt.PropertyName == "CurrentFlowRate"),
                    It.IsAny<CancellationToken>()))
                .Callback(() => Interlocked.Increment(ref currentFlowRateEvents))
                .Returns(Task.CompletedTask);

            // Act
            var startTask = _sut.StartAsync();

            // Allow time for multiple timer callbacks
            await Task.Delay(2200);

            // Assert
            await _sut.StopAsync();
            try { await startTask; } catch { }

            // At least one CurrentFlowRate event should have been raised
            currentFlowRateEvents.Should().BeGreaterThan(0, "Timer should have triggered at least one CurrentFlowRate update");
        }

        [Fact]
        public async Task FlowRateSimulation_ShouldBeWithinExpectedRange()
        {
            // Arrange
            await _sut.InitializeAsync();
            double testFlowRate = 80.0;
            await _sut.SetFlowRateAsync(testFlowRate);

            // Create event completion source to track when the CurrentFlowRate is set
            var eventCompletion = new TaskCompletionSource<double>();

            // Setup event handler to signal completion with the flow rate value
            _mockEventHandler
                .Setup(e => e.HandleEventAsync(
                    It.IsAny<object>(),
                    It.Is<IHydroGardenPropertyChangedEvent>(evt =>
                        evt.PropertyName == "CurrentFlowRate" &&
                        evt.NewValue != null),
                    It.IsAny<CancellationToken>()))
                .Callback<object, IHydroGardenPropertyChangedEvent, CancellationToken>(
                    (_, evt, __) => eventCompletion.TrySetResult((double)(evt.NewValue ?? 0.0)))
                .Returns(Task.CompletedTask);


            // Act
            var startTask = _sut.StartAsync();

            // Wait for the CurrentFlowRate property to be set (with timeout)
            var completionTask = await Task.WhenAny(eventCompletion.Task, Task.Delay(2000));

            // Stop the pump
            await _sut.StopAsync();
            try { await startTask; } catch { }

            // Assert
            completionTask.Should().Be(eventCompletion.Task, "CurrentFlowRate should have been set");

            if (completionTask == eventCompletion.Task)
            {
                var currentFlowRate = await eventCompletion.Task;
                // Expected range is between 98% and 102% of the set flow rate
                currentFlowRate.Should().BeInRange(testFlowRate * 0.98, testFlowRate * 1.02);
            }
        }
    }
}