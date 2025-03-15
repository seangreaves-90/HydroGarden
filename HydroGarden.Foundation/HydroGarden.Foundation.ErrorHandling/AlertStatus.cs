namespace HydroGarden.Foundation.ErrorHandling
{
    /// <summary>
    /// Represents the current alert status based on error rates.
    /// </summary>
    public class AlertStatus
    {
        /// <summary>
        /// Gets or sets a value indicating whether there are active alerts.
        /// </summary>
        public bool HasActiveAlerts { get; set; }
        
        /// <summary>
        /// Gets or sets the list of active alerts.
        /// </summary>
        public List<ErrorAlert> Alerts { get; set; } = new List<ErrorAlert>();
    }
}