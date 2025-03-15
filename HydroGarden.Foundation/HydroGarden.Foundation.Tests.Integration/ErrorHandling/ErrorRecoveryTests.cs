using FluentAssertions;
using HydroGarden.ErrorHandling.Core;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Foundation.ErrorHandling;
using HydroGarden.Foundation.ErrorHandling.Events;
using HydroGarden.Logger.Abstractions;
using Moq;
using Xunit;

namespace HydroGarden.Foundation.Tests.Integration.ErrorHandling
{
    /// <summary>
    /// Integration tests that verify error recovery functionality.
    /// </summary>
    public class ErrorRecoveryTests
    {
        private readonly Mock<ILogger> _mockLogger;
        private readonly Mock<IEventBus> _mockEventBus;
        private readonly Mock<IErrorMonitor> _mockErrorMonitor;
        
        public ErrorRecoveryTests()
        {
            _mockLogger = new Mock<ILogger>();
            _mockEventBus = new Mock<IEventBus>();
            _mockErrorMonitor = new Mock<IErrorMonitor>();
        }
        
        [Fact]
        public async Task ComponentWithErrorHandling_ShouldAttemptRecovery()
        {
            // Arrange - Create a test component with error handling
            var deviceId = Guid.NewGuid();
            var component = new TestComponentWithRecovery(
                deviceId,
                "Test Component",
                _mockErrorMonitor.Object,
                _mockLogger.Object);
            
            // Mock error monitor to track reported errors
            var reportedErrors = new List<IApplicationError>();
            _mockErrorMonitor
                .Setup(m => m.ReportErrorAsync(It.IsAny<IApplicationError>(), It.IsAny<CancellationToken>()))
                .Callback<IApplicationError, CancellationToken>((error, _) => reportedErrors.Add(error))
                .Returns(Task.CompletedTask);
            
            // Setup the event bus
            var publishedEvents = new List<IEvent>();
            _mockEventBus
                .Setup(e => e.PublishAsync(
                    It.IsAny<object>(),
                    It.IsAny<IEvent>(),
                    It.IsAny<CancellationToken>()))
                .Callback<object, IEvent, CancellationToken>((_, evt, _) => publishedEvents.Add(evt))
                .ReturnsAsync(new PublishResult 
                { 
                    EventId = Guid.NewGuid(),
                    SuccessCount = 1,
                    HandlerCount = 1
                });
            
            // Act - Perform operation that will fail
            await component.PerformOperationWithFailureAsync();
            
            // Verify initial failure was reported
            reportedErrors.Should().ContainSingle();
            reportedErrors[0].ErrorCode.Should().Be("OPERATION_FAILED");
            
            // Act - Trigger recovery
            component.RecoveryAttemptCount.Should().Be(0);
            await component.AttemptRecoveryAsync();
            
            // Assert recovery was attempted
            component.RecoveryAttemptCount.Should().Be(1);
            component.LastRecoverySuccessful.Should().BeTrue();
            
            // Act - Try operation again after recovery
            var result = await component.PerformOperationWithFailureAsync();
            
            // Assert operation succeeds after recovery
            result.Should().BeTrue();
        }
        
        [Fact]
        public async Task ErrorContext_ShouldProvideRecoveryInformation()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            
            // Create an error with recovery information in context
            var errorWithRecovery = new ComponentError(
                deviceId,
                "RECOVERABLE_ERROR",
                "This error can be recovered from",
                ErrorSeverity.Error,
                ErrorSource.Device,
                new Dictionary<string, object>
                {
                    ["IsRecoverable"] = true,
                    ["RecoveryStrategy"] = "Restart",
                    ["MaxRecoveryAttempts"] = 3,
                    ["RecoveryDelay"] = TimeSpan.FromSeconds(5).TotalMilliseconds
                });
            
            // Create an error without recovery information
            var nonRecoverableError = new ComponentError(
                deviceId,
                "FATAL_ERROR",
                "This error cannot be recovered from",
                ErrorSeverity.Critical,
                ErrorSource.Device);
            
            // Act - Check if errors are recoverable
            bool canRecoverFirst = ErrorRecoveryHelper.IsRecoverable(errorWithRecovery);
            bool canRecoverSecond = ErrorRecoveryHelper.IsRecoverable(nonRecoverableError);
            
            // Get recovery strategy
            var recoveryStrategy = ErrorRecoveryHelper.GetRecoveryStrategy(errorWithRecovery);
            
            // Assert
            canRecoverFirst.Should().BeTrue();
            canRecoverSecond.Should().BeFalse();
            
            recoveryStrategy.IsRecoverable.Should().BeTrue();
            recoveryStrategy.Strategy.Should().Be("Restart");
            recoveryStrategy.MaxAttempts.Should().Be(3);
            recoveryStrategy.DelayMilliseconds.Should().Be(5000);
        }
        
        [Fact]
        public async Task ComponentHierarchy_ShouldHandleCascadingRecovery()
        {
            // Arrange - Create a parent component with child components
            var parentId = Guid.NewGuid();
            var child1Id = Guid.NewGuid();
            var child2Id = Guid.NewGuid();
            
            var recoverySequence = new List<string>();
            
            var parentComponent = new TestComponentWithRecovery(
                parentId,
                "Parent Component",
                _mockErrorMonitor.Object,
                _mockLogger.Object,
                onRecover: () => recoverySequence.Add("Parent"));
            
            var child1 = new TestComponentWithRecovery(
                child1Id,
                "Child 1",
                _mockErrorMonitor.Object,
                _mockLogger.Object,
                onRecover: () => recoverySequence.Add("Child1"));
            
            var child2 = new TestComponentWithRecovery(
                child2Id,
                "Child 2",
                _mockErrorMonitor.Object,
                _mockLogger.Object,
                onRecover: () => recoverySequence.Add("Child2"));
            
            // Setup parent-child relationships
            parentComponent.AddChild(child1);
            parentComponent.AddChild(child2);
            
            // Mock error monitor
            _mockErrorMonitor
                .Setup(m => m.ReportErrorAsync(It.IsAny<IApplicationError>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            
            // Act - Perform cascading recovery
            await parentComponent.PerformCascadingRecoveryAsync();
            
            // Assert
            recoverySequence.Should().HaveCount(3);
            // Children should recover before parent
            recoverySequence[0].Should().BeOneOf("Child1", "Child2");
            recoverySequence[1].Should().BeOneOf("Child1", "Child2");
            recoverySequence[2].Should().Be("Parent");
            
            parentComponent.LastRecoverySuccessful.Should().BeTrue();
            child1.LastRecoverySuccessful.Should().BeTrue();
            child2.LastRecoverySuccessful.Should().BeTrue();
        }
    }
    
    /// <summary>
    /// Test component that implements error recovery functionality.
    /// </summary>
    public class TestComponentWithRecovery
    {
        private readonly Guid _id;
        private readonly string _name;
        private readonly IErrorMonitor _errorMonitor;
        private readonly ILogger _logger;
        private readonly Action? _onRecover;
        private readonly List<TestComponentWithRecovery> _children = new();
        private bool _operationFailure = true;
        
        public int RecoveryAttemptCount { get; private set; }
        public bool LastRecoverySuccessful { get; private set; }
        
        public TestComponentWithRecovery(
            Guid id,
            string name,
            IErrorMonitor errorMonitor,
            ILogger logger,
            Action? onRecover = null)
        {
            _id = id;
            _name = name;
            _errorMonitor = errorMonitor;
            _logger = logger;
            _onRecover = onRecover;
        }
        
        public void AddChild(TestComponentWithRecovery child)
        {
            _children.Add(child);
        }
        
        public async Task<bool> PerformOperationWithFailureAsync()
        {
            try
            {
                if (_operationFailure)
                {
                    throw new InvalidOperationException("Operation failed as expected");
                }
                
                return true;
            }
            catch (Exception ex)
            {
                // Report the error
                var error = ErrorFactory.CreateDeviceError(
                    _id,
                    "OPERATION_FAILED",
                    $"Operation failed in component {_name}",
                    ErrorSeverity.Error,
                    ex,
                    new Dictionary<string, object>
                    {
                        ["IsRecoverable"] = true,
                        ["RecoveryStrategy"] = "Reset",
                        ["ComponentName"] = _name
                    });
                    
                await _errorMonitor.ReportErrorAsync(error);
                return false;
            }
        }
        
        public async Task AttemptRecoveryAsync()
        {
            _logger.Log($"Attempting recovery for component {_name}...");
            
            try
            {
                // Simulate recovery process
                await Task.Delay(10); // Simulate some work
                
                // Fix the underlying issue
                _operationFailure = false;
                
                RecoveryAttemptCount++;
                LastRecoverySuccessful = true;
                
                _onRecover?.Invoke();
                
                _logger.Log($"Recovery successful for component {_name}");
            }
            catch (Exception ex)
            {
                LastRecoverySuccessful = false;
                _logger.Log(ex, $"Recovery failed for component {_name}");
            }
        }
        
        public async Task PerformCascadingRecoveryAsync()
        {
            // Recover children first
            foreach (var child in _children)
            {
                await child.AttemptRecoveryAsync();
            }
            
            // Then recover self
            await AttemptRecoveryAsync();
        }
    }
    
    /// <summary>
    /// Helper methods for error recovery.
    /// </summary>
    public static class ErrorRecoveryHelper
    {
        public static bool IsRecoverable(IApplicationError error)
        {
            if (error.Context != null && 
                error.Context.TryGetValue("IsRecoverable", out var isRecoverable) && 
                isRecoverable is bool recoverable)
            {
                return recoverable;
            }
            
            // Default behavior based on severity
            return error.Severity != ErrorSeverity.Critical;
        }
        
        public static RecoveryStrategy GetRecoveryStrategy(IApplicationError error)
        {
            var strategy = new RecoveryStrategy
            {
                IsRecoverable = IsRecoverable(error),
                Strategy = "Unknown",
                MaxAttempts = 1,
                DelayMilliseconds = 1000
            };
            
            if (error.Context != null)
            {
                if (error.Context.TryGetValue("RecoveryStrategy", out var strategyValue) && 
                    strategyValue is string strategyString)
                {
                    strategy.Strategy = strategyString;
                }
                
                if (error.Context.TryGetValue("MaxRecoveryAttempts", out var maxAttemptsValue) && 
                    maxAttemptsValue is int maxAttempts)
                {
                    strategy.MaxAttempts = maxAttempts;
                }
                
                if (error.Context.TryGetValue("RecoveryDelay", out var delayValue) && 
                    delayValue is double delay)
                {
                    strategy.DelayMilliseconds = (int)delay;
                }
            }
            
            return strategy;
        }
    }
    
    /// <summary>
    /// Describes how to recover from an error.
    /// </summary>
    public class RecoveryStrategy
    {
        public bool IsRecoverable { get; set; }
        public string Strategy { get; set; } = string.Empty;
        public int MaxAttempts { get; set; }
        public int DelayMilliseconds { get; set; }
    }
}