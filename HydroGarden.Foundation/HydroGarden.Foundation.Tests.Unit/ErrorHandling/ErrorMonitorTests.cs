using FluentAssertions;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorEventTransformation;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.ErrorHandling;
using HydroGarden.Logger.Abstractions;
using Moq;
using Xunit;

namespace HydroGarden.Foundation.Tests.Unit.ErrorHandling
{
    public class ErrorMonitorTests
    {
        private readonly Mock<ILogger> _mockLogger;
        private readonly Mock<IErrorEventTransformationService> _mockTransformationService;
        private readonly ErrorMonitor _errorMonitor;

        public ErrorMonitorTests()
        {
            _mockLogger = new Mock<ILogger>();
            _mockTransformationService = new Mock<IErrorEventTransformationService>();
            _errorMonitor = new ErrorMonitor(_mockLogger.Object, _mockTransformationService.Object);
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => new ErrorMonitor(
                null, 
                _mockTransformationService.Object));
            
            exception.ParamName.Should().Be("logger");
        }

        [Fact]
        public void Constructor_WithNullTransformationService_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => new ErrorMonitor(
                _mockLogger.Object, 
                null));
            
            exception.ParamName.Should().Be("transformationService");
        }

        [Fact]
        public async Task ReportErrorAsync_ShouldStoreErrorAndPublishEvent()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var errorCode = "DEVICE_TEST_ERROR";
            var error = new ComponentError(
                deviceId,
                errorCode,
                "Test error",
                ErrorSeverity.Error,
                ErrorSource.Device);

            _mockTransformationService
                .Setup(t => t.PublishErrorAsEventAsync(It.IsAny<IApplicationError>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _errorMonitor.ReportErrorAsync(error);

            // Assert
            _mockLogger.Verify(l => l.Log(It.IsAny<string>()), Times.Once);
            _mockTransformationService.Verify(
                t => t.PublishErrorAsEventAsync(It.Is<IApplicationError>(e => e == error), It.IsAny<CancellationToken>()),
                Times.Once);
            
            // Verify error is stored
            var activeErrors = await _errorMonitor.GetActiveErrorsForDeviceAsync(deviceId);
            activeErrors.Should().HaveCount(1);
            activeErrors.First().Should().BeSameAs(error);
        }

        [Fact]
        public async Task ReportErrorAsync_WithNullError_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => 
                _errorMonitor.ReportErrorAsync(null));
        }

        [Fact]
        public async Task ReportErrorAsync_WhenPublishingFails_ShouldStillStoreErrorAndLogFailure()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var errorCode = "DEVICE_TEST_ERROR";
            var error = new ComponentError(
                deviceId,
                errorCode,
                "Test error",
                ErrorSeverity.Error,
                ErrorSource.Device);

            _mockTransformationService
                .Setup(t => t.PublishErrorAsEventAsync(It.IsAny<IApplicationError>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Publishing failed"));

            // Act
            await _errorMonitor.ReportErrorAsync(error);

            // Assert
            _mockLogger.Verify(l => l.Log(It.IsAny<string>()), Times.Once);
            _mockLogger.Verify(l => l.Log(It.IsAny<Exception>(), It.IsAny<string>()), Times.Once);
            
            // Verify error is still stored despite publishing failure
            var activeErrors = await _errorMonitor.GetActiveErrorsForDeviceAsync(deviceId);
            activeErrors.Should().HaveCount(1);
            activeErrors.First().Should().BeSameAs(error);
        }

        [Fact]
        public async Task ReportExceptionAsync_ShouldCreateErrorAndReportIt()
        {
            // Arrange
            var source = new object();
            var exception = new InvalidOperationException("Test exception");
            var errorCode = "TEST_ERROR_CODE";
            var message = "Test error message";
            var deviceId = Guid.NewGuid().ToString();
            var context = new Dictionary<string, object>
            {
                { "DeviceId", deviceId }
            };

            _mockTransformationService
                .Setup(t => t.PublishErrorAsEventAsync(It.IsAny<IApplicationError>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _errorMonitor.ReportExceptionAsync(
                source,
                exception,
                errorCode,
                message,
                ErrorSeverity.Error,
                ErrorSource.Device,
                context);

            // Assert
            _mockTransformationService.Verify(
                t => t.PublishErrorAsEventAsync(
                    It.Is<IApplicationError>(e => 
                        e.ErrorCode == errorCode && 
                        e.Message == message), 
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task ReportExceptionAsync_WithNullSource_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => 
                _errorMonitor.ReportExceptionAsync(
                    null,
                    new Exception(),
                    "CODE",
                    "Message"));
        }

        [Fact]
        public async Task ReportExceptionAsync_WithNullException_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => 
                _errorMonitor.ReportExceptionAsync(
                    new object(),
                    null,
                    "CODE",
                    "Message"));
        }

        [Fact]
        public async Task GetRecentErrorsAsync_ShouldReturnErrorsOrderedByTimestamp()
        {
            // Arrange
            var deviceId1 = Guid.NewGuid();
            var deviceId2 = Guid.NewGuid();
            
            var error1 = new ComponentError(
                deviceId1,
                "ERROR_1",
                "Error 1",
                ErrorSeverity.Error,
                ErrorSource.Device);
            
            await Task.Delay(10); // Ensure different timestamps
            
            var error2 = new ComponentError(
                deviceId2,
                "ERROR_2",
                "Error 2",
                ErrorSeverity.Critical,
                ErrorSource.Service);
            
            await _errorMonitor.ReportErrorAsync(error1);
            await _errorMonitor.ReportErrorAsync(error2);

            // Act
            var recentErrors = await _errorMonitor.GetRecentErrorsAsync();

            // Assert
            recentErrors.Should().HaveCount(2);
            recentErrors.First().Should().Be(error2); // Most recent first
            recentErrors.Last().Should().Be(error1);
        }

        [Fact]
        public async Task GetRecentErrorsAsync_WithLimit_ShouldLimitResults()
        {
            // Arrange
            for (int i = 0; i < 5; i++)
            {
                var error = new ComponentError(
                    Guid.NewGuid(),
                    $"ERROR_{i}",
                    $"Error {i}",
                    ErrorSeverity.Error,
                    ErrorSource.Device);
                
                await _errorMonitor.ReportErrorAsync(error);
                await Task.Delay(10); // Ensure different timestamps
            }

            // Act
            var recentErrors = await _errorMonitor.GetRecentErrorsAsync(3);

            // Assert
            recentErrors.Should().HaveCount(3);
        }

        [Fact]
        public async Task HasActiveErrorsAsync_WithNoErrors_ShouldReturnFalse()
        {
            // Act
            var hasErrors = await _errorMonitor.HasActiveErrorsAsync();

            // Assert
            hasErrors.Should().BeFalse();
        }

        [Fact]
        public async Task HasActiveErrorsAsync_WithErrorsBelowSeverity_ShouldReturnFalse()
        {
            // Arrange
            var error = new ComponentError(
                Guid.NewGuid(),
                "WARNING_ERROR",
                "Warning message",
                ErrorSeverity.Warning,
                ErrorSource.Device);
            
            await _errorMonitor.ReportErrorAsync(error);

            // Act
            var hasErrors = await _errorMonitor.HasActiveErrorsAsync(ErrorSeverity.Error);

            // Assert
            hasErrors.Should().BeFalse();
        }

        [Fact]
        public async Task HasActiveErrorsAsync_WithErrorsAboveSeverity_ShouldReturnTrue()
        {
            // Arrange
            var error = new ComponentError(
                Guid.NewGuid(),
                "CRITICAL_ERROR",
                "Critical error message",
                ErrorSeverity.Critical,
                ErrorSource.Device);
            
            await _errorMonitor.ReportErrorAsync(error);

            // Act
            var hasErrors = await _errorMonitor.HasActiveErrorsAsync(ErrorSeverity.Error);

            // Assert
            hasErrors.Should().BeTrue();
        }

        [Fact]
        public async Task GetActiveErrorsForDeviceAsync_ShouldReturnOnlyDeviceErrors()
        {
            // Arrange
            var deviceId1 = Guid.NewGuid();
            var deviceId2 = Guid.NewGuid();
            
            var error1 = new ComponentError(
                deviceId1,
                "ERROR_1",
                "Error 1",
                ErrorSeverity.Error,
                ErrorSource.Device);
            
            var error2 = new ComponentError(
                deviceId2,
                "ERROR_2",
                "Error 2",
                ErrorSeverity.Error,
                ErrorSource.Device);

            var error3 = new ComponentError(
                deviceId1,
                "ERROR_3",
                "Error 3",
                ErrorSeverity.Critical,
                ErrorSource.Device);
            
            await _errorMonitor.ReportErrorAsync(error1);
            await _errorMonitor.ReportErrorAsync(error2);
            await _errorMonitor.ReportErrorAsync(error3);

            // Act
            var deviceErrors = await _errorMonitor.GetActiveErrorsForDeviceAsync(deviceId1);

            // Assert
            deviceErrors.Should().HaveCount(2);
            deviceErrors.Should().Contain(error1);
            deviceErrors.Should().Contain(error3);
            deviceErrors.Should().NotContain(error2);
        }

        [Fact]
        public async Task ClearErrorAsync_ShouldRemoveErrorByDeviceIdAndCode()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var errorCode = "TEST_ERROR";
            
            var error = new ComponentError(
                deviceId,
                errorCode,
                "Test error",
                ErrorSeverity.Error,
                ErrorSource.Device);
            
            await _errorMonitor.ReportErrorAsync(error);

            // Act
            await _errorMonitor.ClearErrorAsync(deviceId, errorCode);

            // Assert
            var activeErrors = await _errorMonitor.GetActiveErrorsForDeviceAsync(deviceId);
            activeErrors.Should().BeEmpty();
            _mockLogger.Verify(l => l.Log(It.Is<string>(s => s.Contains("Cleared error"))), Times.Once);
        }

        [Fact]
        public async Task ClearErrorAsync_WithNonExistentError_ShouldDoNothing()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var errorCode = "NONEXISTENT_ERROR";

            // Act
            await _errorMonitor.ClearErrorAsync(deviceId, errorCode);

            // Assert
            _mockLogger.Verify(l => l.Log(It.Is<string>(s => s.Contains("Cleared error"))), Times.Once);
        }

        [Fact]
        public async Task MultipleErrors_WithSameDeviceAndCode_ShouldOverwriteOlderError()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var errorCode = "TEST_ERROR";
            
            var error1 = new ComponentError(
                deviceId,
                errorCode,
                "First error",
                ErrorSeverity.Error,
                ErrorSource.Device);
            
            var error2 = new ComponentError(
                deviceId,
                errorCode,
                "Updated error",
                ErrorSeverity.Critical,
                ErrorSource.Device);
            
            await _errorMonitor.ReportErrorAsync(error1);
            await _errorMonitor.ReportErrorAsync(error2);

            // Act
            var activeErrors = await _errorMonitor.GetActiveErrorsForDeviceAsync(deviceId);

            // Assert
            activeErrors.Should().HaveCount(1);
            activeErrors.First().Should().BeSameAs(error2);
            activeErrors.First().Message.Should().Be("Updated error");
            activeErrors.First().Severity.Should().Be(ErrorSeverity.Critical);
        }
    }
}