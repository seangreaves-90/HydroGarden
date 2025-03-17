using FluentAssertions;
using HydroGarden.Foundation.Abstractions.Interfaces;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Foundation.ErrorHandling;
using HydroGarden.Foundation.ErrorHandling.Events;
using HydroGarden.Logger.Abstractions;
using Moq;
using Xunit;

namespace HydroGarden.Foundation.Tests.Unit.ErrorHandling
{
    public class ErrorEventTransformationTests
    {
        private readonly Mock<IEventBus> _mockEventBus;
        private readonly Mock<ILogger> _mockLogger;
        private readonly ErrorEventTransformationService _transformationService;

        public ErrorEventTransformationTests()
        {
            _mockEventBus = new Mock<IEventBus>();
            _mockLogger = new Mock<ILogger>();
            _transformationService = new ErrorEventTransformationService(_mockEventBus.Object, _mockLogger.Object);
        }

        [Fact]
        public void Constructor_WithNullEventBus_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => new ErrorEventTransformationService(
                null, 
                _mockLogger.Object));
            
            exception.ParamName.Should().Be("eventBus");
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => new ErrorEventTransformationService(
                _mockEventBus.Object, 
                null));
            
            exception.ParamName.Should().Be("logger");
        }

        [Fact]
        public void TransformErrorToEvent_ShouldCreateErrorEvent()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            // Use a specific Guid for testing
            var correlationId = new Guid("30fd1ad3-a5c0-4aad-b0ab-c6ddc9dacf2d");
            var errorCode = "TEST_ERROR";
            var message = "Test error message";
            var exception = new InvalidOperationException("Test exception");
            var context = new Dictionary<string, object>
            {
                { "TestKey", "TestValue" }
            };

            var error = new ComponentError(
                deviceId,
                errorCode,
                message,
                ErrorSeverity.Critical,
                ErrorSource.Device,
                context,
                exception,
                null,      // category
                correlationId);  // Use the specific correlationId

            // Act
            var errorEvent = _transformationService.TransformErrorToEvent(error);

            // Assert
            errorEvent.Should().NotBeNull();
            errorEvent.DeviceId.Should().Be(deviceId);
            errorEvent.ErrorCode.Should().Be(errorCode);
            errorEvent.Message.Should().Be(message);
            errorEvent.Severity.Should().Be(ErrorSeverity.Critical);
            errorEvent.Source.Should().Be(ErrorSource.Device);
            errorEvent.CorrelationId.Should().Be(correlationId);
            errorEvent.Context.Should().ContainKey("TestKey");
            errorEvent.Context["TestKey"].Should().Be("TestValue");
            errorEvent.ExceptionDetails.Should().NotBeNull();
            errorEvent.ExceptionDetails.Should().Contain("InvalidOperationException");
            errorEvent.ExceptionType.Should().Be("System.InvalidOperationException");
        }

        [Fact]
        public void TransformErrorToEvent_WithNullError_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                _transformationService.TransformErrorToEvent(null));
        }

        [Fact]
        public void TransformToPublishableEvent_ShouldCreateErrorOccurredEvent()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var correlationId = Guid.NewGuid();
            var errorCode = "TEST_ERROR";
            var message = "Test error message";
            
            var errorEvent = new ErrorEvent(
                deviceId,
                errorCode,
                message,
                ErrorSeverity.Critical,
                ErrorSource.Device,
                null,
                null,
                correlationId,
                new Dictionary<string, object>());

            // Act
            var publishableEvent = _transformationService.TransformToPublishableEvent(errorEvent);

            // Assert
            publishableEvent.Should().NotBeNull();
            publishableEvent.Should().BeOfType<ErrorOccurredEvent>();
            
            var typedEvent = (ErrorOccurredEvent)publishableEvent;
            typedEvent.SourceId.Should().Be(deviceId);
            typedEvent.DeviceId.Should().Be(deviceId);
            typedEvent.CorrelationId.Should().Be(correlationId);
            typedEvent.EventType.Should().Be(EventType.Error);
            typedEvent.ErrorData.Should().BeSameAs(errorEvent);
            
            typedEvent.RoutingData.Should().NotBeNull();
            typedEvent.RoutingData.Should().BeOfType<ErrorEventRoutingData>();
            
            var routingData = (ErrorEventRoutingData)typedEvent.RoutingData;
            routingData.DeviceId.Should().Be(deviceId);
            routingData.Severity.Should().Be(ErrorSeverity.Critical);
        }

        [Fact]
        public void TransformToPublishableEvent_WithNullErrorEvent_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                _transformationService.TransformToPublishableEvent(null));
        }

        [Fact]
        public void ExtractErrorEvent_WithErrorOccurredEvent_ShouldReturnErrorData()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var errorEvent = new ErrorEvent(
                deviceId,
                "TEST_ERROR",
                "Test error message",
                ErrorSeverity.Error,
                ErrorSource.Device,
                null,
                null,
                Guid.NewGuid(),
                new Dictionary<string, object>());
            
            var occuredEvent = new ErrorOccurredEvent
            {
                DeviceId = deviceId,
                SourceId = deviceId,
                ErrorData = errorEvent,
                EventType = EventType.Error
            };

            // Act
            var extractedErrorEvent = _transformationService.ExtractErrorEvent(occuredEvent);

            // Assert
            extractedErrorEvent.Should().NotBeNull();
            extractedErrorEvent.Should().BeSameAs(errorEvent);
        }

        [Fact]
        public void ExtractErrorEvent_WithNonErrorEvent_ShouldReturnNull()
        {
            // Arrange
            var mockEvent = new Mock<IEvent>();
            mockEvent.Setup(e => e.EventType).Returns(EventType.Command);

            // Act
            var extractedErrorEvent = _transformationService.ExtractErrorEvent(mockEvent.Object);

            // Assert
            extractedErrorEvent.Should().BeNull();
        }

        [Fact]
        public async Task PublishErrorAsEventAsync_ShouldTransformAndPublishEvent()
        {
            // Arrange
            var deviceId = Guid.NewGuid();
            var error = new ComponentError(
                deviceId,
                "TEST_ERROR",
                "Test error message",
                ErrorSeverity.Error,
                ErrorSource.Device);

            _mockEventBus
                .Setup(e => e.PublishAsync(It.IsAny<object>(), It.IsAny<IEvent>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Mock<IPublishResult>().Object);

            // Act
            await _transformationService.PublishErrorAsEventAsync(error);

            // Assert
            _mockEventBus.Verify(e => e.PublishAsync(
                It.IsAny<object>(),
                It.Is<IEvent>(evt => evt.GetType() == typeof(ErrorOccurredEvent) && 
                    ((ErrorOccurredEvent)evt).DeviceId == deviceId && 
                    ((ErrorOccurredEvent)evt).EventType == EventType.Error),
                It.IsAny<CancellationToken>()),
                Times.Once);
            
            _mockLogger.Verify(l => l.Log(It.Is<string>(s => s.Contains("Published error as event"))), Times.Once);
        }

        [Fact]
        public async Task PublishErrorAsEventAsync_WithPublishFailure_ShouldLogErrorAndRethrow()
        {
            // Arrange
            var error = new ComponentError(
                Guid.NewGuid(),
                "TEST_ERROR",
                "Test error message",
                ErrorSeverity.Error,
                ErrorSource.Device);

            var exception = new InvalidOperationException("Publishing failed");
            _mockEventBus
                .Setup(e => e.PublishAsync(It.IsAny<object>(), It.IsAny<IEvent>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(exception);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => 
                _transformationService.PublishErrorAsEventAsync(error));
            
            _mockLogger.Verify(l => l.Log(It.IsAny<Exception>(), It.Is<string>(s => s.Contains("Failed to publish error"))), Times.Once);
        }

        [Fact]
        public async Task PublishErrorAsEventAsync_WithNullError_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => 
                _transformationService.PublishErrorAsEventAsync(null));
        }
    }
}