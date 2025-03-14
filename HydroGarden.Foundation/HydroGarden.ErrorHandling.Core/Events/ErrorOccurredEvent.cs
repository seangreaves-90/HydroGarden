using HydroGarden.Foundation.Abstractions.Interfaces.ErrorEventTransformation;
using HydroGarden.Foundation.Abstractions.Interfaces.Events;

namespace HydroGarden.Foundation.ErrorHandling.Events
{
    /// <summary>
    /// Represents an event that occurs when an error is detected.
    /// </summary>
    public class ErrorOccurredEvent : IEvent
    {
        /// <inheritdoc/>
        public Guid EventId { get; } = Guid.NewGuid();

        /// <inheritdoc/>
        public Guid SourceId { get; set; }

        /// <inheritdoc/>
        public EventType EventType { get; set; }

        /// <summary>
        /// Gets or sets the error data.
        /// </summary>
        public IErrorEvent ErrorData { get; set; } = null!;

        /// <inheritdoc/>
        public Guid CorrelationId { get; set; }

        /// <inheritdoc/>
        public IEventRoutingData? RoutingData { get; set; }

        /// <inheritdoc/>
        public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
        
        /// <summary>
        /// Gets or sets the device ID associated with this error.
        /// </summary>
        public Guid DeviceId { get; set; }
    }
}
