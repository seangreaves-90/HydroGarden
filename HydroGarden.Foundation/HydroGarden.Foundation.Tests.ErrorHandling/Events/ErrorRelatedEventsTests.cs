using FluentAssertions;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorEventTransformation;
using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Foundation.ErrorHandling.Events;
using Moq;
using Xunit;

namespace HydroGarden.Foundation.Tests.ErrorHandling.Events
{
    /// <summary>
    /// Unit tests for error-related event classes.
    /// </summary>
    public class ErrorRelatedEventsTests
    {
        [Fact]
        public void ErrorOccurredEvent_Constructor_ShouldInitializeCommonProperties()
        {
            // Arrange & Act
            var errorEvent = new ErrorOccurredEvent();
            
            // Assert
            errorEvent.EventId.Should().NotBeEmpty();
            errorEvent.Timestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
            errorEvent.EventType.Should().Be(EventType.Alert);
            errorEvent.CorrelationId.Should().NotBeEmpty();
        }

        [Fact]
        public void ErrorOccurredEvent_WithErrorData_ShouldStoreData()
        {
            // Arrange
            var errorEvent = new ErrorOccurredEvent();
            var mockErrorData = new Mock<IErrorEvent>();
            
            // Act
            errorEvent.ErrorData = mockErrorData.Object;
            
            // Assert
            errorEvent.ErrorData.Should().BeSameAs(mockErrorData.Object);
        }

        [Fact]
        public void RecoveryAttemptedEvent_Constructor_ShouldInitializeCommonProperties()
        {
            // Arrange & Act
            var recoveryEvent = new RecoveryAttemptedEvent();
            
            // Assert
            recoveryEvent.EventId.Should().NotBeEmpty();
            recoveryEvent.Timestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
            recoveryEvent.EventType.Should().Be(EventType.System);
            recoveryEvent.CorrelationId.Should().NotBeEmpty();
        }

        [Fact]
        public void RecoveryAttemptedEvent_WithRecoveryData_ShouldStoreData()
        {
            // Arrange
            var recoveryEvent = new RecoveryAttemptedEvent();
            var mockRecoveryData = new Mock<IRecoveryEvent>();
            
            // Act
            recoveryEvent.RecoveryData = mockRecoveryData.Object;
            
            // Assert
            recoveryEvent.RecoveryData.Should().BeSameAs(mockRecoveryData.Object);
        }

        [Fact]
        public void ErrorRelatedEvent_SourceId_ShouldBeSettable()
        {
            // Arrange
            var errorEvent = new ErrorOccurredEvent();
            var sourceId = Guid.NewGuid();
            
            // Act
            errorEvent.SourceId = sourceId;
            
            // Assert
            errorEvent.SourceId.Should().Be(sourceId);
        }

        [Fact]
        public void ErrorRelatedEvent_DeviceId_ShouldBeSettable()
        {
            // Arrange
            var errorEvent = new ErrorOccurredEvent();
            var deviceId = Guid.NewGuid();
            
            // Act
            errorEvent.DeviceId = deviceId;
            
            // Assert
            errorEvent.DeviceId.Should().Be(deviceId);
        }

        [Fact]
        public void ErrorRelatedEvent_RoutingData_ShouldBeSettable()
        {
            // Arrange
            var errorEvent = new ErrorOccurredEvent();
            var mockRoutingData = new Mock<IEventRoutingData>();
            
            // Act
            errorEvent.RoutingData = mockRoutingData.Object;
            
            // Assert
            errorEvent.RoutingData.Should().BeSameAs(mockRoutingData.Object);
        }

        [Fact]
        public void ErrorRelatedEvent_CorrelationId_ShouldBeSettable()
        {
            // Arrange
            var errorEvent = new ErrorOccurredEvent();
            var correlationId = Guid.NewGuid();
            
            // Act
            errorEvent.CorrelationId = correlationId;
            
            // Assert
            errorEvent.CorrelationId.Should().Be(correlationId);
        }
    }
}
