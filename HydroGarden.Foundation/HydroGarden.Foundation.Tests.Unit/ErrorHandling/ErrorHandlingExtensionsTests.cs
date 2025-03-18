using FluentAssertions;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.ErrorHandling;
using HydroGarden.Foundation.ErrorHandling.Common;
using HydroGarden.Foundation.ErrorHandling.Exceptions;
using HydroGarden.Foundation.ErrorHandling.Extensions;
using Moq;
using Xunit;

namespace HydroGarden.Foundation.Tests.Unit.ErrorHandling
{
    public class ErrorHandlingExtensionsTests
    {
        private readonly Mock<IErrorMonitor> _mockErrorMonitor = new();

        [Fact]
        public async Task ReportExceptionAsync_ShouldBuildContextAndReportError()
        {
        // Arrange
        var source = new object();
        var exception = new InvalidOperationException("Test exception");
        var errorCode = "TEST_ERROR";
        var message = "Test error message";

        _mockErrorMonitor
            .Setup(m => m.ReportExceptionAsync(
                It.IsAny<object>(),
                It.IsAny<Exception>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<ErrorSeverity>(),
                It.IsAny<ErrorSource>(),
                It.IsAny<IDictionary<string, object>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await ErrorHandlingExtensions.ReportExceptionAsync(
        _mockErrorMonitor.Object,
            source,
            exception,
            errorCode,
        message);

        // Assert
        _mockErrorMonitor.Verify(m => m.ReportExceptionAsync(
            It.Is<object>(o => o == source),
            It.Is<Exception>(e => e == exception),
            It.Is<string>(s => s == errorCode),
            It.Is<string>(s => s == message),
            It.IsAny<ErrorSeverity>(),
            It.IsAny<ErrorSource>(),
            It.Is<IDictionary<string, object>>(d => 
                d.ContainsKey("SourceType") && 
                d.ContainsKey("CallSite") &&
                d.ContainsKey("ExceptionType")),
            It.IsAny<CancellationToken>()),
            Times.Once);
        }

        [Fact]
        public async Task ReportExceptionAsync_WithApplicationException_ShouldUseExceptionProperties()
        {
            // Arrange
            var source = new object();
            var deviceId = Guid.NewGuid();
            var exception = new DeviceInitializationException(
                "Device initialization failed",
                deviceId);

            _mockErrorMonitor
                .Setup(m => m.ReportErrorAsync(
                    It.IsAny<IApplicationError>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _mockErrorMonitor.Object.ReportExceptionAsync(
                source,
                exception);

            // Assert
            _mockErrorMonitor.Verify(m => m.ReportErrorAsync(
                It.Is<IApplicationError>(e => 
                    e.DeviceId == deviceId && 
                    e.ErrorCode == ErrorCodes.Device.INITIALIZATION_FAILED &&
                    e.Severity == ErrorSeverity.Error &&
                    e.Source == ErrorSource.Device),
                It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task ReportDeviceErrorAsync_ShouldCreateAndReportDeviceError()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var errorCode = ErrorCodes.Device.SENSOR_MALFUNCTION;
            var message = "Sensor malfunction detected";
            var exception = new InvalidOperationException("Reading out of range");

            _mockErrorMonitor
                .Setup(m => m.ReportErrorAsync(
                    It.IsAny<IApplicationError>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _mockErrorMonitor.Object.ReportDeviceErrorAsync(
                deviceId,
                errorCode,
                message,
                ErrorSeverity.Critical,
                exception);

            // Assert
            _mockErrorMonitor.Verify(m => m.ReportErrorAsync(
                It.Is<IApplicationError>(e => 
                    e.DeviceId == deviceId && 
                    e.ErrorCode == errorCode &&
                    e.Message == message &&
                    e.Severity == ErrorSeverity.Critical &&
                    e.Source == ErrorSource.Device &&
                    e.Exception == exception),
                It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task ReportServiceErrorAsync_ShouldCreateAndReportServiceError()
        {
            // Arrange
            var serviceName = "TestService";
            var errorCode = ErrorCodes.Service.INITIALIZATION_FAILED;
            var message = "Service initialization failed";

            _mockErrorMonitor
                .Setup(m => m.ReportErrorAsync(
                    It.IsAny<IApplicationError>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _mockErrorMonitor.Object.ReportServiceErrorAsync(
                serviceName,
                errorCode,
                message);

            // Assert
            _mockErrorMonitor.Verify(m => m.ReportErrorAsync(
                It.Is<IApplicationError>(e => 
                    e.ErrorCode == errorCode &&
                    e.Message == message &&
                    e.Source == ErrorSource.Service &&
                    e.Context.ContainsKey("ServiceName") &&
                    e.Context["ServiceName"].ToString() == serviceName),
                It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task ReportDeviceInitializationErrorAsync_ShouldUseCorrectErrorCode()
        {
        // Arrange
        var deviceId = Guid.NewGuid();
        var message = "Failed to initialize device";
        var capturedError = (IApplicationError)null;

        _mockErrorMonitor
        .Setup(m => m.ReportErrorAsync(
        It.IsAny<IApplicationError>(),
        It.IsAny<CancellationToken>()))
        .Callback<IApplicationError, CancellationToken>((e, _) => capturedError = e)
        .Returns(Task.CompletedTask);

        // Act
        await ErrorHandlingExtensions.ReportDeviceInitializationErrorAsync(
            _mockErrorMonitor.Object,
            deviceId,
            message);

        // Assert
        _mockErrorMonitor.Verify(m => m.ReportErrorAsync(
            It.IsAny<IApplicationError>(),
            It.IsAny<CancellationToken>()), 
        Times.Once);
        
        capturedError.Should().NotBeNull();
        capturedError.DeviceId.Should().Be(deviceId);
        capturedError.ErrorCode.Should().Be(ErrorCodes.Device.INITIALIZATION_FAILED);
        capturedError.Message.Should().Be(message);
        capturedError.Severity.Should().Be(ErrorSeverity.Critical);
        capturedError.Source.Should().Be(ErrorSource.Device);
        }

        [Fact]
        public async Task ReportDeviceCommunicationErrorAsync_ShouldUseCorrectErrorCode()
        {
        // Arrange
        var deviceId = Guid.NewGuid();
        var message = "Communication lost with device";
        var capturedError = (IApplicationError)null;

        _mockErrorMonitor
        .Setup(m => m.ReportErrorAsync(
        It.IsAny<IApplicationError>(),
        It.IsAny<CancellationToken>()))
        .Callback<IApplicationError, CancellationToken>((e, _) => capturedError = e)
        .Returns(Task.CompletedTask);

        // Act
        await ErrorHandlingExtensions.ReportDeviceCommunicationErrorAsync(
            _mockErrorMonitor.Object,
            deviceId,
            message);

        // Assert
        _mockErrorMonitor.Verify(m => m.ReportErrorAsync(
            It.IsAny<IApplicationError>(),
            It.IsAny<CancellationToken>()), 
        Times.Once);
        
        capturedError.Should().NotBeNull();
        capturedError.DeviceId.Should().Be(deviceId);
        capturedError.ErrorCode.Should().Be(ErrorCodes.Device.COMMUNICATION_LOST);
        capturedError.Message.Should().Be(message);
        capturedError.Severity.Should().Be(ErrorSeverity.Critical);
        capturedError.Source.Should().Be(ErrorSource.Device);
        }

        [Fact]
        public async Task ReportServiceInitializationErrorAsync_ShouldUseCorrectErrorCode()
        {
        // Arrange
        var serviceName = "TestService";
        var message = "Service initialization failed";
        var capturedError = (IApplicationError)null;

        _mockErrorMonitor
        .Setup(m => m.ReportErrorAsync(
        It.IsAny<IApplicationError>(),
        It.IsAny<CancellationToken>()))
        .Callback<IApplicationError, CancellationToken>((e, _) => capturedError = e)
        .Returns(Task.CompletedTask);

        // Act
        await ErrorHandlingExtensions.ReportServiceInitializationErrorAsync(
            _mockErrorMonitor.Object,
            serviceName,
            message);

        // Assert
        _mockErrorMonitor.Verify(m => m.ReportErrorAsync(
            It.IsAny<IApplicationError>(),
            It.IsAny<CancellationToken>()), 
        Times.Once);
        
        capturedError.Should().NotBeNull();
        capturedError.ErrorCode.Should().Be(ErrorCodes.Service.INITIALIZATION_FAILED);
        capturedError.Message.Should().Be(message);
        capturedError.Severity.Should().Be(ErrorSeverity.Critical);
        capturedError.Source.Should().Be(ErrorSource.Service);
        capturedError.Context.Should().ContainKey("ServiceName");
        capturedError.Context["ServiceName"].Should().Be(serviceName);
        }
    }
}