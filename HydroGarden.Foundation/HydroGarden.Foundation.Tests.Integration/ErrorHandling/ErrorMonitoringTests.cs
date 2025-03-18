using FluentAssertions;

using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Foundation.Common.Events;
using HydroGarden.Foundation.ErrorHandling;
using HydroGarden.Foundation.ErrorHandling.Events;
using HydroGarden.Logger.Abstractions;
using Moq;


namespace HydroGarden.Foundation.Tests.Integration.ErrorHandling
{
    /// <summary>
    /// Integration tests for error monitoring functionality.
    /// </summary>
    public class ErrorMonitoringTests
    {
        private readonly Mock<ILogger> _mockLogger;
        private readonly Mock<IEventBus> _mockEventBus;
        private readonly ErrorEventTransformationService _transformationService;
        private readonly ErrorMonitor _errorMonitor;
        private readonly List<ErrorOccurredEvent> _publishedErrorEvents = [];
        
        public ErrorMonitoringTests()
        {
            _mockLogger = new Mock<ILogger>();
            _mockEventBus = new Mock<IEventBus>();
            
            // Set up event bus to track published error events
            _mockEventBus
                .Setup(e => e.PublishAsync(
                    It.IsAny<object>(),
                    It.IsAny<IEvent>(),
                    It.IsAny<CancellationToken>()))
                .Callback<object, IEvent, CancellationToken>((_, evt, _) => 
                {
                    if (evt is ErrorOccurredEvent errorEvent)
                    {
                        _publishedErrorEvents.Add(errorEvent);
                    }
                })
                .ReturnsAsync(new PublishResult 
                { 
                    EventId = Guid.NewGuid(),
                    SuccessCount = 1,
                    HandlerCount = 1
                });
            
            _transformationService = new ErrorEventTransformationService(
                _mockEventBus.Object,
                _mockLogger.Object);
                
            _errorMonitor = new ErrorMonitor(
                _mockLogger.Object,
                _transformationService);
        }
        
        [Fact]
        public async Task ErrorMonitor_ShouldAggregateErrors()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var errorCode = "TEST_ERROR";
            
            // Create multiple errors with the same code and device
            var error1 = ErrorFactory.CreateDeviceError(
                deviceId,
                errorCode,
                "First occurrence",
                ErrorSeverity.Warning);
                
            var error2 = ErrorFactory.CreateDeviceError(
                deviceId,
                errorCode,
                "Second occurrence",
                ErrorSeverity.Warning);
                
            var error3 = ErrorFactory.CreateDeviceError(
                deviceId,
                errorCode,
                "Third occurrence"); // Increased severity
            
            // Keep track of published events
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
            
            // Act - Report multiple errors in sequence
            await _errorMonitor.ReportErrorAsync(error1);
            await _errorMonitor.ReportErrorAsync(error2);
            await _errorMonitor.ReportErrorAsync(error3);
            
            // Assert
            // Verify error events were published through the EventBus
            _mockEventBus.Verify(
                e => e.PublishAsync(
                    It.IsAny<object>(),
                    It.IsAny<IEvent>(),
                    It.IsAny<CancellationToken>()),
                Times.Exactly(3));
                
            // Now we can look at error rate statistics from the monitor
            var errorRates = _errorMonitor.GetErrorRates();
            errorRates.Should().ContainKey(errorCode);
            errorRates[errorCode].Count.Should().Be(3);
            errorRates[errorCode].MaxSeverity.Should().Be(ErrorSeverity.Error);
            errorRates[errorCode].DeviceIds.Should().Contain(deviceId);
        }
        
        [Fact]
        public async Task ErrorMonitor_ShouldDetectHighErrorRates()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var errorCode = "HIGH_RATE_ERROR";
            
            // Create a monitoring class to track error rate alerts
            var errorAlerts = new List<ErrorOccurredEvent>();
            
            // Set up event bus to track published alerts
            _mockEventBus
                .Setup(e => e.PublishAsync(
                    It.IsAny<object>(),
                    It.Is<IEvent>(evt => evt is ErrorOccurredEvent && 
                                         ((ErrorOccurredEvent)evt).ErrorData.ErrorCode.Contains("RATE")),
                    It.IsAny<CancellationToken>()))
                .Callback<object, IEvent, CancellationToken>((_, evt, _) => 
                {
                    if (evt is ErrorOccurredEvent errorEvent)
                    {
                        errorAlerts.Add(errorEvent);
                    }
                })
                .ReturnsAsync(new PublishResult 
                { 
                    EventId = Guid.NewGuid(),
                    SuccessCount = 1,
                    HandlerCount = 1
                });
            
            // Act - Generate warnings and check for alerts
            for (int i = 0; i < 2; i++)
            {
                var warningError = ErrorFactory.CreateDeviceError(
                    deviceId,
                    errorCode,
                    $"Warning {i+1}",
                    ErrorSeverity.Warning);
                    
                await _errorMonitor.ReportErrorAsync(warningError);
            }
            
            // Check if alert was triggered (shouldn't be yet)
            var alertStatus1 = _errorMonitor.GetAlertStatus();
            bool alertTriggered1 = alertStatus1.HasActiveAlerts;
            
            // Generate one more warning
            var finalWarning = ErrorFactory.CreateDeviceError(
                deviceId,
                errorCode,
                "Warning 3",
                ErrorSeverity.Warning);
                
            await _errorMonitor.ReportErrorAsync(finalWarning);
            
            // Generate a critical error
            var criticalError = ErrorFactory.CreateDeviceError(
                deviceId,
                "CRITICAL_ERROR",
                "Critical failure",
                ErrorSeverity.Critical);
                
            await _errorMonitor.ReportErrorAsync(criticalError);
            
            // Check alert status after critical error
            var alertStatus2 = _errorMonitor.GetAlertStatus();
            bool alertTriggered2 = alertStatus2.Alerts.Any(e => e.Severity == ErrorSeverity.Critical);
            
            // Assert
            // Verify that we reported all errors
            _mockEventBus.Verify(
                e => e.PublishAsync(
                    It.IsAny<object>(),
                    It.IsAny<IEvent>(),
                    It.IsAny<CancellationToken>()),
                Times.AtLeast(4)); // At least 3 warnings + 1 critical
                
            alertTriggered1.Should().BeFalse("Two warnings shouldn't trigger alert yet");
            alertTriggered2.Should().BeTrue("Critical error should trigger alert");
            
            alertStatus2.Alerts.Should().Contain(a => a.ErrorCode == "CRITICAL_ERROR" && a.Severity == ErrorSeverity.Critical);
            var errorRates = _errorMonitor.GetErrorRates();
            errorRates.Should().ContainKey("CRITICAL_ERROR");
            errorRates.Should().ContainKey(errorCode);
            errorRates[errorCode].Count.Should().Be(3);
        }
        
        [Fact]
        public async Task ErrorMonitor_ShouldCorrelateRelatedErrors()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var correlationId = Guid.NewGuid();
            
            // Create a parent error with specified correlation ID
            var parentError = new ComponentError(
                deviceId,
                "PARENT_ERROR",
                "Parent error",
                ErrorSeverity.Error,
                ErrorSource.Device,
                null,  // context
                null,  // exception
                null,  // category
                correlationId);
            
            // Create child errors with same correlation ID
            var childError1 = new ComponentError(
                deviceId,
                "CHILD_ERROR_1",
                "First child error",
                ErrorSeverity.Warning,
                ErrorSource.Device,
                null,   // context
                null,   // exception
                null,   // category
                correlationId);
                
            var childError2 = new ComponentError(
                deviceId,
                "CHILD_ERROR_2",
                "Second child error",
                ErrorSeverity.Warning, // Using Warning instead of Information
                ErrorSource.Device,
                null,   // context
                null,   // exception
                null,   // category
                correlationId);
            
            // Create unrelated error
            var unrelatedError = new ComponentError(
                deviceId,
                "UNRELATED_ERROR",
                "Unrelated error",
                ErrorSeverity.Warning,
                ErrorSource.Device);
            
            // Act - Report errors
            await _errorMonitor.ReportErrorAsync(parentError);
            await _errorMonitor.ReportErrorAsync(childError1);
            await _errorMonitor.ReportErrorAsync(childError2);
            await _errorMonitor.ReportErrorAsync(unrelatedError);
            
            // Get correlated errors from the monitor
            var correlatedErrors = _errorMonitor.GetCorrelatedErrors(correlationId);
            
            // Assert
            correlatedErrors.Should().HaveCount(3);
            correlatedErrors.Should().Contain(e => e.ErrorCode == "PARENT_ERROR");
            correlatedErrors.Should().Contain(e => e.ErrorCode == "CHILD_ERROR_1");
            correlatedErrors.Should().Contain(e => e.ErrorCode == "CHILD_ERROR_2");
            
            // Verify all errors were published
            _mockEventBus.Verify(
                e => e.PublishAsync(
                    It.IsAny<object>(),
                    It.IsAny<IEvent>(),
                    It.IsAny<CancellationToken>()),
                Times.Exactly(4)); // Parent + 2 children + unrelated
        }
    }
}