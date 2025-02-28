using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HydroGarden.Foundation.Abstractions.Interfaces
{
    /// <summary>
    /// Base interface for all HydroGarden events
    /// </summary>
    public interface IHydroGardenEvent
    {
        /// <summary>
        /// The source component that raised the event
        /// </summary>
        Guid DeviceId { get; }

        /// <summary>
        /// Unique identifier for the event
        /// </summary>
        Guid EventId { get; }

        /// <summary>
        /// The time when the event was created
        /// </summary>
        DateTimeOffset Timestamp { get; }

        /// <summary>
        /// The source component that raised the event
        /// </summary>
        Guid SourceId { get; }

        /// <summary>
        /// The type of the event
        /// </summary>
        EventType EventType { get; }

        /// <summary>
        /// Optional routing data - used to determine how this event should be processed
        /// </summary>
        IEventRoutingData? RoutingData { get; }
    }

    /// <summary>
    /// Classification of event types in the system
    /// </summary>
    public enum EventType
    {
        /// <summary>
        /// Property value change on a component
        /// </summary>
        PropertyChanged,

        /// <summary>
        /// Device lifecycle events (initialized, started, stopped)
        /// </summary>
        Lifecycle,

        /// <summary>
        /// Command events requesting action from a component
        /// </summary>
        Command,

        /// <summary>
        /// Telemetry/sensor reading events
        /// </summary>
        Telemetry,

        /// <summary>
        /// Alert/notification events requiring attention
        /// </summary>
        Alert,

        /// <summary>
        /// System status events
        /// </summary>
        System,

        /// <summary>
        /// Timer/scheduler events
        /// </summary>
        Timer,

        /// <summary>
        /// Custom event types defined by device implementations
        /// </summary>
        Custom
    }
}
