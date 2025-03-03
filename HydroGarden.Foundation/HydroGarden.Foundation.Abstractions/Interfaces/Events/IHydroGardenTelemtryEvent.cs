namespace HydroGarden.Foundation.Abstractions.Interfaces.Events
{
    /// <summary>
    /// Interface for telemetry events
    /// </summary>
    public interface IHydroGardenTelemetryEvent : IHydroGardenEvent
    {
        /// <summary>
        /// The telemetry readings in this event
        /// </summary>
        IDictionary<string, object> Readings { get; }

        /// <summary>
        /// Units of measurement for each reading
        /// </summary>
        IDictionary<string, string>? Units { get; }
    }

    /// <summary>
    /// Specialized handler for telemetry events
    /// </summary>
    public interface IHydroGardenTelemetryEventHandler : IHydroGardenEventHandler 
    { 
    
    }
    
}
