using HydroGarden.Foundation.Abstractions.Interfaces.ErrorEventTransformation;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Foundation.ErrorHandling;
using HydroGarden.Foundation.ErrorHandling.Events;
using HydroGarden.Foundation.ErrorHandling.Exceptions;
using HydroGarden.Logger.Abstractions;
using Moq;
using Xunit;

namespace HydroGarden.Foundation.Tests.ErrorHandling
{
    public class ErrorHandlingTests
    {
        [Fact]
        public void ComponentError_CreatesExpectedProperties()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var errorCode = "DEVICE_TEST_ERROR";
            var message = "Test error message";
            var context = new Dictionary<string, object>
            {
                { "TestKey", "TestValue" }
            };
            var exception = new InvalidOperationException("Test exception");

            // Act
            var error = new ComponentError(
                deviceId,
                errorCode,
                message,
                ErrorSeverity.Critical,
                ErrorSource.Device,
                context,
                exception,
                ErrorCategory.Device);

            // Assert
            Assert.Equal(deviceId, error.DeviceId);
            Assert.Equal(errorCode, error.ErrorCode);
            Assert.Equal(message, error.Message);
            Assert.Equal(ErrorSeverity.Critical, error.Severity);
            Assert.Equal(ErrorSource.Device, error.Source);
            Assert.Equal(ErrorCategory.Device, error.Category);
            Assert.Same(exception, error.Exception);
            Assert.Equal("TestValue", error.Context["TestKey"]);
            Assert.NotEqual(Guid.Empty, error.CorrelationId);
        }

        [Fact]
        public void ErrorFactory_CreatesDeviceError_WithExpectedProperties()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var errorCode = "DEVICE_TEST_ERROR";
            var message = "Test error message";
            var exception = new InvalidOperationException("Test exception");

            // Act
            var error = ErrorFactory.CreateDeviceError(
                deviceId,
                errorCode,
                message,
                ErrorSeverity.Critical,
                exception);

            // Assert
            Assert.Equal(deviceId, error.DeviceId);
            Assert.Equal(errorCode, error.ErrorCode);
            Assert.Equal(message, error.Message);
            Assert.Equal(ErrorSeverity.Critical, error.Severity);
            Assert.Equal(ErrorSource.Device, error.Source);
            Assert.Equal(ErrorCategory.Device, error.Category);
            Assert.Same(exception, error.Exception);
        }

        [Fact]
        public async Task ErrorMonitor_ReportsError_CallsTransformationService()
        {
            // Arrange
            var loggerMock = new Mock<ILogger>();
            var transformationServiceMock = new Mock<IErrorEventTransformationService>();
            var monitor = new ErrorMonitor(loggerMock.Object, transformationServiceMock.Object);

            var deviceId = Guid.NewGuid();
            var error = new ComponentError(
                deviceId,
                "DEVICE_TEST_ERROR",
                "Test error message",
                ErrorSeverity.Error,
                ErrorSource.Device);

            // Act
            await monitor.ReportErrorAsync(error);

            // Assert
            transformationServiceMock.Verify(x => x.PublishErrorAsEventAsync(
                It.Is<IApplicationError>(e => e.DeviceId == deviceId && e.ErrorCode == "DEVICE_TEST_ERROR"),
                It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public void ErrorEvent_FromApplicationError_MapsAllProperties()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var correlationId = Guid.NewGuid();
            var exception = new InvalidOperationException("Test exception");
            var context = new Dictionary<string, object>
            {
                { "TestKey", "TestValue" }
            };

            var error = new ComponentError(
                deviceId,
                "DEVICE_TEST_ERROR",
                "Test error message",
                ErrorSeverity.Error,
                ErrorSource.Device,
                context,
                exception);

            // Act
            var errorEvent = ErrorEvent.FromApplicationError(error);

            // Assert
            Assert.Equal(deviceId, errorEvent.DeviceId);
            Assert.Equal("DEVICE_TEST_ERROR", errorEvent.ErrorCode);
            Assert.Equal("Test error message", errorEvent.Message);
            Assert.Equal(ErrorSeverity.Error, errorEvent.Severity);
            Assert.Equal(ErrorSource.Device, errorEvent.Source);
            Assert.Equal("TestValue", errorEvent.Context["TestKey"]);
            Assert.Contains("System.InvalidOperationException", errorEvent.ExceptionDetails);
            Assert.Equal("System.InvalidOperationException", errorEvent.ExceptionType);
        }

        [Fact]
        public void DeviceInitializationException_CreatesCorrectProperties()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var message = "Device initialization failed";
            var innerException = new TimeoutException("Connection timed out");

            // Act
            var exception = new DeviceInitializationException(
                message,
                deviceId,
                innerException);

            // Assert
            Assert.Equal(message, exception.Message);
            Assert.Equal(deviceId, exception.DeviceId);
            Assert.Equal(ErrorCodes.Device.INITIALIZATION_FAILED, exception.ErrorCode);
            Assert.Equal(ErrorSeverity.Error, exception.Severity);
            Assert.Equal(ErrorSource.Device, exception.Source);
            Assert.Equal(ErrorCategory.Device, exception.Category);
            Assert.Same(innerException, exception.InnerException);
        }

        [Fact]
        public void ErrorContextBuilder_BuildsContextWithAllProperties()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var exception = new InvalidOperationException("Test exception");

            // Act
            var context = ErrorContextBuilder.Create()
                .WithDevice(deviceId, "TestDevice")
                .WithLocation()
                .WithOperation("TestOperation")
                .WithException(exception)
                .WithProperty("CustomKey", "CustomValue")
                .WithErrorClassification(
                    "DEVICE_TEST_ERROR",
                    ErrorSeverity.Critical,
                    ErrorSource.Device,
                    ErrorCategory.Device)
                .Build();

            // Assert
            Assert.Equal(deviceId, context["DeviceId"]);
            Assert.Equal("TestDevice", context["DeviceName"]);
            Assert.Equal("TestOperation", context["Operation"]);
            Assert.Equal("InvalidOperationException", context["ExceptionType"]);
            Assert.Equal("Test exception", context["ExceptionMessage"]);
            Assert.Equal("CustomValue", context["CustomKey"]);
            Assert.Equal("DEVICE_TEST_ERROR", context["ErrorCode"]);
            Assert.Equal("Critical", context["ErrorSeverity"]);
            Assert.Equal("Device", context["ErrorSource"]);
            Assert.Equal("Device", context["ErrorCategory"]);
            Assert.Contains("ContextCreatedAt", context.Keys);
        }

        [Fact]
        public async Task ErrorEventTransformationService_PublishesErrorAsEvent()
        {
            // Arrange
            var eventBusMock = new Mock<IEventBus>();
            var loggerMock = new Mock<ILogger>();

            var service = new ErrorEventTransformationService(
                eventBusMock.Object,
                loggerMock.Object);

            var deviceId = Guid.NewGuid();
            var error = new ComponentError(
                deviceId,
                "DEVICE_TEST_ERROR",
                "Test error message",
                ErrorSeverity.Error,
                ErrorSource.Device);

            // Act
            await service.PublishErrorAsEventAsync(error);

            // Assert
            eventBusMock.Verify(x => x.PublishAsync(
                It.Is<IEvent>(e => e is ErrorOccurredEvent),
                It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public void TransformErrorToEvent_CreatesCorrectErrorEvent()
        {
            // Arrange
            var eventBusMock = new Mock<IEventBus>();
            var loggerMock = new Mock<ILogger>();

            var service = new ErrorEventTransformationService(
                eventBusMock.Object,
                loggerMock.Object);

            var deviceId = Guid.NewGuid();
            var error = new ComponentError(
                deviceId,
                "DEVICE_TEST_ERROR",
                "Test error message",
                ErrorSeverity.Error,
                ErrorSource.Device);

            // Act
            var errorEvent = service.TransformErrorToEvent(error);

            // Assert
            Assert.Equal(deviceId, errorEvent.DeviceId);
            Assert.Equal("DEVICE_TEST_ERROR", errorEvent.ErrorCode);
            Assert.Equal("Test error message", errorEvent.Message);
            Assert.Equal(ErrorSeverity.Error, errorEvent.Severity);
            Assert.Equal(ErrorSource.Device, errorEvent.Source);
        }

        [Fact]
        public void TransformToPublishableEvent_CreatesCorrectErrorOccurredEvent()
        {
            // Arrange
            var eventBusMock = new Mock<IEventBus>();
            var loggerMock = new Mock<ILogger>();

            var service = new ErrorEventTransformationService(
                eventBusMock.Object,
                loggerMock.Object);

            var deviceId = Guid.NewGuid();
            var correlationId = Guid.NewGuid();
            
            var errorEvent = new ErrorEvent(
                deviceId,
                "DEVICE_TEST_ERROR",
                "Test error message",
                ErrorSeverity.Error,
                ErrorSource.Device,
                null,
                null,
                correlationId,
                new Dictionary<string, object>());

            // Act
            var publishableEvent = service.TransformToPublishableEvent(errorEvent);

            // Assert
            Assert.IsType<ErrorOccurredEvent>(publishableEvent);
            
            var typedEvent = (ErrorOccurredEvent)publishableEvent;
            Assert.Equal(deviceId, typedEvent.DeviceId);
            Assert.Equal(correlationId, typedEvent.CorrelationId);
            Assert.Equal(EventType.Error, typedEvent.EventType);
            Assert.Same(errorEvent, typedEvent.ErrorData);
            
            Assert.IsType<ErrorEventRoutingData>(typedEvent.RoutingData);
            var routingData = (ErrorEventRoutingData)typedEvent.RoutingData;
            Assert.Equal(deviceId, routingData.DeviceId);
            Assert.Equal(ErrorSeverity.Error, routingData.Severity);
        }
    }
}