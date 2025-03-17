using HydroGarden.Foundation.Abstractions.Interfaces.Events;

namespace HydroGarden.Foundation.Common.Events
{
    /// <summary>
    /// Represents a system event within the HydroGarden system.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="SystemEvent"/> class.
    /// </remarks>
    /// <param name="deviceId">The device ID.</param>
    /// <param name="eventSubType">The subtype of the system event.</param>
    /// <param name="eventData">The event data.</param>
    /// <param name="routingData">Optional routing data.</param>
    public class SystemEvent(
        Guid deviceId,
        string eventSubType,
        IDictionary<string, object> eventData,
        IEventRoutingData? routingData = null) : HydroGardenEventBase(deviceId, routingData), ISystemEvent
    {
        /// <summary>
        /// Gets the subtype of the system event.
        /// </summary>
        public string EventSubType { get; } = eventSubType;

        /// <summary>
        /// Gets the event data.
        /// </summary>
        public IDictionary<string, object> EventData { get; } = eventData;

        /// <summary>
        /// Gets the event type.
        /// </summary>
        public override EventType EventType => EventType.System;

        /// <summary>
        /// Creates a new recovery attempt system event.
        /// </summary>
        /// <param name="deviceId">The device ID.</param>
        /// <param name="errorCode">The error code.</param>
        /// <param name="isSuccessful">Whether the recovery was successful.</param>
        /// <param name="message">The recovery message.</param>
        /// <param name="correlationId">The correlation ID.</param>
        /// <returns>A new <see cref="SystemEvent"/> for a recovery attempt.</returns>
        public static SystemEvent CreateRecoveryAttemptEvent(
            Guid deviceId,
            string errorCode,
            bool isSuccessful,
            string message,
            Guid correlationId)
        {
            var eventData = new Dictionary<string, object>
            {
                ["DeviceId"] = deviceId,
                ["ErrorCode"] = errorCode,
                ["IsSuccessful"] = isSuccessful,
                ["Message"] = message,
                ["CorrelationId"] = correlationId,
                ["Timestamp"] = DateTimeOffset.UtcNow
            };

            return new SystemEvent(deviceId, "RecoveryAttempt", eventData);
        }
    }
}