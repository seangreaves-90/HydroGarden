namespace HydroGarden.Foundation.Abstractions.Interfaces.Events
{
    /// <summary>
    /// Interface for system events.
    /// </summary>
    public interface ISystemEvent : IEvent
    {
        /// <summary>
        /// Gets the subtype of the system event.
        /// </summary>
        string EventSubType { get; }

        /// <summary>
        /// Gets the event data.
        /// </summary>
        IDictionary<string, object> EventData { get; }
    }
}
