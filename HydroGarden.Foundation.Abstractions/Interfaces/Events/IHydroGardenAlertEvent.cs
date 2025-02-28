namespace HydroGarden.Foundation.Abstractions.Interfaces.Events
{
    /// <summary>
    /// Alert severity levels
    /// </summary>
    public enum AlertSeverity
    {
        Info = 0,
        Warning = 50,
        Error = 100,
        Critical = 200
    }

    /// <summary>
    /// Interface for alert events
    /// </summary>
    public interface IHydroGardenAlertEvent : IHydroGardenEvent
    {
        /// <summary>
        /// The severity of the alert
        /// </summary>
        AlertSeverity Severity { get; }

        /// <summary>
        /// The alert message
        /// </summary>
        string Message { get; }

        /// <summary>
        /// Additional information about the alert
        /// </summary>
        IDictionary<string, object>? AlertData { get; }

        /// <summary>
        /// Whether the alert has been acknowledged
        /// </summary>
        bool IsAcknowledged { get; set; }
    }
}
