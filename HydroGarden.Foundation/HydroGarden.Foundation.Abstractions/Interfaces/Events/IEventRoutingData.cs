namespace HydroGarden.Foundation.Abstractions.Interfaces.Events
{
    /// <summary>
    /// Interface for event routing data
    /// </summary>
    public interface IEventRoutingData
    {
        /// <summary>
        /// Optional list of specific target component IDs
        /// If empty, the event is routed based on topology
        /// </summary>
        Guid[] TargetIds { get; }

        /// <summary>
        /// Whether this event should be persisted
        /// </summary>
        bool Persist { get; }

        /// <summary>
        /// Event priority - higher priority events are processed first
        /// </summary>
        EventPriority Priority { get; }

        /// <summary>
        /// Whether the event requires acknowledgment from handlers
        /// </summary>
        bool RequiresAcknowledgment { get; }

        /// <summary>
        /// Maximum time to wait for all handlers to process the event
        /// </summary>
        TimeSpan? Timeout { get; }
    }

    /// <summary>
    /// Priority levels for event processing
    /// </summary>
    public enum EventPriority
    {
        Low = 0,
        Normal = 50,
        High = 100,
        Critical = 200
    }
}
