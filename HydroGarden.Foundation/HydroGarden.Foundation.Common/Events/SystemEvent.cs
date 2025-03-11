using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Foundation.Common.Events.Extensions;

namespace HydroGarden.Foundation.Common.Events
{
    /// <summary>
    /// Represents a system event within the HydroGarden system.
    /// </summary>
    public class SystemEvent : HydroGardenEventBase, ISystemEvent
    {
        /// <summary>
        /// Gets the sub-type of the system event.
        /// </summary>
        public string EventSubType { get; }

        /// <summary>
        /// Gets the event data.
        /// </summary>
        public IDictionary<string, object> EventData { get; }

        /// <summary>
        /// Gets the event type.
        /// </summary>
        public override EventType EventType => EventType.System;

        /// <summary>
        /// Initializes a new instance of the <see cref="SystemEvent"/> class.
        /// </summary>
        /// <param name="deviceId">The device ID.</param>
        /// <param name="eventSubType">The sub-type of the system event.</param>
        /// <param name="eventData">The event data.</param>
        /// <param name="routingData">Optional routing data.</param>
        public SystemEvent(
            Guid deviceId,
            string eventSubType,
            IDictionary<string, object> eventData,
            IEventRoutingData? routingData = null)
            : base(deviceId, routingData)
        {
            EventSubType = eventSubType;
            EventData = eventData;
        }

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