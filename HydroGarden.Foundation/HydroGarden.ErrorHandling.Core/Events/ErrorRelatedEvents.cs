using HydroGarden.Foundation.Abstractions.Interfaces.ErrorEventTransformation;
using HydroGarden.Foundation.Abstractions.Interfaces.Events;

namespace HydroGarden.Foundation.ErrorHandling.Events
{
    /// <summary>
    /// Base class for error-related events.
    /// </summary>
    public abstract class ErrorRelatedEvent : IEvent
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ErrorRelatedEvent"/> class.
        /// </summary>
        protected ErrorRelatedEvent()
        {
            EventId = Guid.NewGuid();
            Timestamp = DateTimeOffset.UtcNow;
        }

        /// <inheritdoc/>
        public Guid EventId { get; set; }

        /// <inheritdoc/>
        public DateTimeOffset Timestamp { get; set; }

        /// <inheritdoc/>
        public EventType EventType { get; set; }

        /// <inheritdoc/>
        public Guid SourceId { get; set; }

        /// <inheritdoc/>
        public Guid DeviceId { get; set; }

        /// <inheritdoc/>
        public IEventRoutingData RoutingData { get; set; }

        /// <summary>
        /// Gets or sets the correlation ID for tracing.
        /// </summary>
        public Guid CorrelationId { get; set; }
    }

    /// <summary>
    /// Represents an error event that can be published to the event bus.
    /// </summary>
    public class ErrorOccurredEvent : ErrorRelatedEvent
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ErrorOccurredEvent"/> class.
        /// </summary>
        public ErrorOccurredEvent() : base()
        {
            EventType = EventType.Alert;
        }

        /// <summary>
        /// Gets or sets the error event data.
        /// </summary>
        public IErrorEvent? ErrorData { get; set; }
    }

    /// <summary>
    /// Represents a recovery event that can be published to the event bus.
    /// </summary>
    public class RecoveryAttemptedEvent : ErrorRelatedEvent
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RecoveryAttemptedEvent"/> class.
        /// </summary>
        public RecoveryAttemptedEvent() : base()
        {
            EventType = EventType.System;
        }

        /// <summary>
        /// Gets or sets the recovery event data.
        /// </summary>
        public IRecoveryEvent? RecoveryData { get; set; }
    }
}