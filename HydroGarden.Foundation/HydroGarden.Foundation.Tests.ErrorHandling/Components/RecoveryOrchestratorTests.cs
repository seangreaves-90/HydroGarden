using FluentAssertions;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling.RecoveryStrategy;
using HydroGarden.Foundation.ErrorHandling;
using HydroGarden.Foundation.ErrorHandling.Common;
using HydroGarden.Foundation.Tests.ErrorHandling.Mocks;
using HydroGarden.Logger.Abstractions;
using Moq;
using Xunit;

namespace HydroGarden.Foundation.Tests.ErrorHandling.Components
{
    /// <summary>
    /// Unit tests for the RecoveryOrchestrator class.
    /// </summary>
    public class RecoveryOrchestratorTests
    {
        private readonly Guid _deviceId = Guid.NewGuid();
        private readonly Mock<ILogger> _mockLogger;
        private readonly Mock<IErrorMonitor> _mockErrorMonitor;
        private readonly List<IRecoveryStrategy> _strategies;
        private readonly RecoveryOrchestrator _orchestrator;

        public RecoveryOrchestratorTests()
        {
            _mockLogger = new Mock<ILogger>();
            _mockErrorMonitor = new Mock<IErrorMonitor>();
            _strategies = new List<IRecoveryStrategy>();
            _orchestrator = new RecoveryOrchestrator(_mockLogger.Object, _mockErrorMonitor.Object, _strategies);
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => 
                new RecoveryOrchestrator(null!, _mockErrorMonitor.Object, _strategies));
            exception.ParamName.Should().Be("logger");
        }

        [Fact]
        public void Constructor_WithNullErrorMonitor_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => 
                new RecoveryOrchestrator(_mockLogger.Object, null!, _strategies));
            exception.ParamName.Should().Be("errorMonitor");
        }

        [Fact]
        public void Constructor_WithNullStrategies_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => 
                new RecoveryOrchestrator(_mockLogger.Object, _mockErrorMonitor.Object, null!));
            exception.ParamName.Should().Be("strategies");
        }

        [Fact]
        public void Constructor_WithEmptyStrategies_ShouldLogWarning()
        {
            // Arrange & Act
            var orchestrator = new RecoveryOrchestrator(_mockLogger.Object, _mockErrorMonitor.Object, _strategies);

            // Assert
            _mockLogger.Verify(l => l.Log(It.Is<string>(s => 
                s.Contains("Warning") && s.Contains("No recovery strategies registered"))));
        }

        [Fact]
        public void Constructor_WithStrategies_ShouldLogInitialization()
        {
            // Arrange
            _strategies.Add(new MockRecoveryStrategy());
            _strategies.Add(new MockRecoveryStrategy());

            // Act
            var orchestrator = new RecoveryOrchestrator(_mockLogger.Object, _mockErrorMonitor.Object, _strategies);

            // Assert
            _mockLogger.Verify(l => l.Log(It.Is<string>(s => 
                s.Contains("Recovery orchestrator initialized") && s.Contains("2 strategies"))));
        }

        [Fact]
        public async Task AttemptRecoveryAsync_WithNullError_ShouldThrowArgumentNullException()
        {
            // Arrange, Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => 
                _orchestrator.AttemptRecoveryAsync(null));
            exception.ParamName.Should().Be("error");
        }

        [Fact]
        public async Task AttemptRecoveryAsync_WithComponentErrorThatCannotAttemptRecovery_ShouldReturnFalse()
        {
            // Arrange
            var error = ComponentError.CreateTransient(
                _deviceId,
                ErrorCodes.Communication.TIMEOUT,
                "Connection timeout");

            // Make error exceed max recovery attempts
            for (int i = 0; i < error.MaxRecoveryAttempts; i++)
            {
                error.RecordRecoveryAttempt();
            }

            // Act
            var result = await _orchestrator.AttemptRecoveryAsync(error);

            // Assert
            result.Should().BeFalse();
            _mockLogger.Verify(l => l.Log(It.Is<string>(s => 
                s.Contains("Cannot attempt recovery") && s.Contains("backoff period not elapsed"))));
        }

        [Fact]
        public async Task AttemptRecoveryAsync_WithDeviceAlreadyRecovering_ShouldReturnFalse()
        {
            // Arrange
            var error = ComponentError.CreateTransient(
                _deviceId,
                ErrorCodes.Communication.TIMEOUT,
                "Connection timeout");

            var mockStrategy = new TestMockRecoveryStrategy("TestStrategy")
            {
                CanRecoverValue = true,
                RecoverySuccessful = true
            };
            _strategies.Add(mockStrategy);

            // Simulate a device already in recovery by making first call but not letting it complete
            var recoveryTask = _orchestrator.AttemptRecoveryAsync(error);
            
            // Act - Try to recover the same device again while first recovery is still in progress
            var result = await _orchestrator.AttemptRecoveryAsync(error);

            // Assert
            result.Should().BeFalse();
            _mockLogger.Verify(l => l.Log(It.Is<string>(s => 
                s.Contains("Recovery already in progress"))));
            
            // Complete first task
            await recoveryTask;
        }

        [Fact]
        public async Task AttemptRecoveryAsync_WithNoApplicableStrategies_ShouldReturnFalse()
        {
            // Arrange
            var error = ComponentError.CreateTransient(
                _deviceId,
                ErrorCodes.Communication.TIMEOUT,
                "Connection timeout");

            var mockStrategy = new MockRecoveryStrategy { CanRecoverValue = false };
            _strategies.Add(mockStrategy);

            // Act
            var result = await _orchestrator.AttemptRecoveryAsync(error);

            // Assert
            result.Should().BeFalse();
            _mockLogger.Verify(l => l.Log(It.Is<string>(s => 
                s.Contains("No applicable recovery strategies found"))));
            mockStrategy.AttemptedRecoveries.Should().BeEmpty();
        }

        [Fact]
        public async Task AttemptRecoveryAsync_WithSuccessfulStrategy_ShouldReturnTrueAndRegisterSuccess()
        {
            // Arrange
            var error = ComponentError.CreateTransient(
                _deviceId,
                ErrorCodes.Communication.TIMEOUT,
                "Connection timeout");

            var mockStrategy = new MockRecoveryStrategy
            {
                CanRecoverValue = true,
                RecoverySuccessful = true
            };
            _strategies.Add(mockStrategy);

            // Act
            var result = await _orchestrator.AttemptRecoveryAsync(error);

            // Assert
            result.Should().BeTrue();
            _mockLogger.Verify(l => l.Log(It.Is<string>(s => 
                s.Contains("Recovery successful") && s.Contains(mockStrategy.Name))));
            mockStrategy.AttemptedRecoveries.Should().ContainSingle();
            mockStrategy.AttemptedRecoveries[0].Should().Be(error);
            
            _mockErrorMonitor.Verify(m => m.RegisterRecoveryAttemptAsync(
                error.DeviceId, error.ErrorCode!, true, It.IsAny<CancellationToken>()));
        }

        [Fact]
        public async Task AttemptRecoveryAsync_WithFailedStrategy_ShouldTryNextStrategy()
        {
            // Arrange
            var error = ComponentError.CreateTransient(
                _deviceId,
                ErrorCodes.Communication.TIMEOUT,
                "Connection timeout");

            var failingStrategy = new TestMockRecoveryStrategy("FailingStrategy")
            {
                CanRecoverValue = true,
                RecoverySuccessful = false
            };
            
            var successfulStrategy = new TestMockRecoveryStrategy("SuccessfulStrategy")
            {
                CanRecoverValue = true,
                RecoverySuccessful = true
            };
            
            _strategies.Add(failingStrategy);
            _strategies.Add(successfulStrategy);

            // Act
            var result = await _orchestrator.AttemptRecoveryAsync(error);

            // Assert
            result.Should().BeTrue();
            failingStrategy.AttemptedRecoveries.Should().ContainSingle();
            successfulStrategy.AttemptedRecoveries.Should().ContainSingle();
            _mockLogger.Verify(l => l.Log(It.Is<string>(s => 
                s.Contains("Recovery strategy") && s.Contains("failed") && s.Contains("trying next strategy"))));
        }

        [Fact]
        public async Task AttemptRecoveryAsync_WithAllStrategiesFailed_ShouldReturnFalseAndRegisterFailure()
        {
            // Arrange
            var error = ComponentError.CreateTransient(
                _deviceId,
                ErrorCodes.Communication.TIMEOUT,
                "Connection timeout");

            var failingStrategy1 = new TestMockRecoveryStrategy("FailingStrategy1")
            {
                CanRecoverValue = true,
                RecoverySuccessful = false
            };
            
            var failingStrategy2 = new TestMockRecoveryStrategy("FailingStrategy2")
            {
                CanRecoverValue = true,
                RecoverySuccessful = false
            };
            
            _strategies.Add(failingStrategy1);
            _strategies.Add(failingStrategy2);

            // Act
            var result = await _orchestrator.AttemptRecoveryAsync(error);

            // Assert
            result.Should().BeFalse();
            failingStrategy1.AttemptedRecoveries.Should().ContainSingle();
            failingStrategy2.AttemptedRecoveries.Should().ContainSingle();
            _mockLogger.Verify(l => l.Log(It.Is<string>(s => 
                s.Contains("All recovery strategies failed"))));
            
            _mockErrorMonitor.Verify(m => m.RegisterRecoveryAttemptAsync(
                error.DeviceId, error.ErrorCode!, false, It.IsAny<CancellationToken>()));
        }

        [Fact]
        public async Task AttemptRecoveryAsync_WithStrategyThrowingException_ShouldHandleExceptionAndContinue()
        {
            // Arrange
            var error = ComponentError.CreateTransient(
                _deviceId,
                ErrorCodes.Communication.TIMEOUT,
                "Connection timeout");

            var throwingStrategy = new TestMockRecoveryStrategy("ThrowingStrategy")
            {
                CanRecoverValue = true,
                ThrowExceptionOnRecovery = true
            };
            
            var successfulStrategy = new TestMockRecoveryStrategy("SuccessfulStrategy")
            {
                CanRecoverValue = true,
                RecoverySuccessful = true
            };
            
            _strategies.Add(throwingStrategy);
            _strategies.Add(successfulStrategy);

            // Act
            var result = await _orchestrator.AttemptRecoveryAsync(error);

            // Assert
            result.Should().BeTrue();
            throwingStrategy.AttemptedRecoveries.Should().ContainSingle();
            successfulStrategy.AttemptedRecoveries.Should().ContainSingle();
            _mockLogger.Verify(l => l.Log(It.IsAny<Exception>(), It.IsAny<string>()));
        }

        [Fact]
        public async Task AttemptRecoveryAsync_ShouldOrderStrategiesByPriority()
        {
            // Arrange
            var error = ComponentError.CreateTransient(
                _deviceId,
                ErrorCodes.Communication.TIMEOUT,
                "Connection timeout");

            // Create recovery strategies with different priorities using the configurable mock
            var highPriorityStrategy = new ConfigurableMockRecoveryStrategy(
                "HighPriorityStrategy", 
                _ => true,
                (_, __) => 
                {
                    _mockLogger.Object.Log("Executing high priority strategy");
                    return Task.FromResult(true);
                })
                { Priority = 10 };
            
            var lowPriorityStrategy = new ConfigurableMockRecoveryStrategy(
                "LowPriorityStrategy", 
                _ => true,
                (_, __) => 
                {
                    _mockLogger.Object.Log("Executing low priority strategy");
                    return Task.FromResult(true);
                })
                { Priority = 100 };
            
            // Add strategies in reverse priority order to ensure sorting works
            _strategies.Add(lowPriorityStrategy);
            _strategies.Add(highPriorityStrategy);

            // Act
            var result = await _orchestrator.AttemptRecoveryAsync(error);

            // Assert
            result.Should().BeTrue();
            // Verify that the high priority strategy was called first
            var logSequence = _mockLogger.Invocations
                .Where(i => i.Arguments.Any(a => a is string s && 
                    (s.Contains("Executing high priority") || s.Contains("Executing low priority"))))
                .Select(i => i.Arguments[0] as string)
                .ToList();
            
            logSequence.Should().HaveCount(1); // Only the high priority one should be executed
            logSequence[0].Should().Be("Executing high priority strategy");
        }

        [Fact]
        public async Task AttemptRecoveryAsync_WithExceptionDuringOrchestration_ShouldLogAndReturnFalse()
        {
            // Arrange
            var error = ComponentError.CreateTransient(
                _deviceId,
                ErrorCodes.Communication.TIMEOUT,
                "Connection timeout");

            // Mock error monitor that throws during registration
            _mockErrorMonitor.Setup(m => m.RegisterRecoveryAttemptAsync(
                    It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Test exception"));
            
            var mockStrategy = new MockRecoveryStrategy
            {
                CanRecoverValue = true,
                RecoverySuccessful = true
            };
            _strategies.Add(mockStrategy);

            // Act
            var result = await _orchestrator.AttemptRecoveryAsync(error);

            // Assert
            result.Should().BeFalse();
            _mockLogger.Verify(l => l.Log(It.IsAny<Exception>(), It.Is<string>(s => 
                s.Contains("Exception during recovery orchestration"))));
        }
    }

    /// <summary>
    /// Extended mock recovery strategy for testing priority-based execution
    /// </summary>
    /// <summary>
    /// Extension of MockRecoveryStrategy with a configurable name
    /// </summary>
    internal class TestMockRecoveryStrategy : MockRecoveryStrategy
    {
        private readonly string _name;

        public TestMockRecoveryStrategy(string name)
        {
            _name = name;
        }

        public override string Name => _name;
    }

    internal class ConfigurableMockRecoveryStrategy : IRecoveryStrategy
    {
        private readonly Func<IApplicationError?, bool> _canRecoverFunc;
        private readonly Func<IApplicationError?, CancellationToken, Task<bool>> _attemptRecoveryFunc;

        public string Name { get; }
        public int Priority { get; set; } = 100;

        public ConfigurableMockRecoveryStrategy(
            string name,
            Func<IApplicationError?, bool> canRecoverFunc,
            Func<IApplicationError?, CancellationToken, Task<bool>> attemptRecoveryFunc)
        {
            Name = name;
            _canRecoverFunc = canRecoverFunc;
            _attemptRecoveryFunc = attemptRecoveryFunc;
        }

        public bool CanRecover(IApplicationError? error) => _canRecoverFunc(error);

        public Task<bool> AttemptRecoveryAsync(IApplicationError? error, CancellationToken ct = default) =>
            _attemptRecoveryFunc(error, ct);
    }
}
