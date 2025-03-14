using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.Abstractions.Interfaces.Events;

namespace HydroGarden.Foundation.ErrorHandling.Events
{
    /// <summary>
    /// Implementation of event routing data for error events.
    /// </summary>
    public class ErrorEventRoutingData : IEventRoutingData
    {
        /// <summary>
        /// Gets or sets the device ID associated with this error.
        /// </summary>
        public Guid DeviceId { get; set; }

        /// <summary>
        /// Gets or sets the severity of the error.
        /// </summary>
        public ErrorSeverity Severity { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ErrorEventRoutingData"/> class.
        /// </summary>
        public ErrorEventRoutingData()
        {
            // Set default values
            TargetIds = [];
            Persist = true;
            Priority = EventPriority.High;
            RequiresAcknowledgment = false;
            Timeout = TimeSpan.FromSeconds(30);
            Severity = ErrorSeverity.Error;
        }

        /// <inheritdoc/>
        public Guid[] TargetIds { get; set; }

        /// <inheritdoc/>
        public bool Persist { get; set; }

        /// <inheritdoc/>
        public EventPriority Priority { get; set; }

        /// <inheritdoc/>
        public bool RequiresAcknowledgment { get; set; }

        /// <inheritdoc/>
        public TimeSpan? Timeout { get; set; }

        /// <summary>
        /// Sets the priority based on the error severity.
        /// </summary>
        public void SetPriorityFromSeverity()
        {
            Priority = Severity switch
            {
                ErrorSeverity.Warning => EventPriority.Normal,
                ErrorSeverity.Error => EventPriority.High,
                ErrorSeverity.Critical => EventPriority.Critical,
                ErrorSeverity.Catastrophic => EventPriority.Critical,
                _ => EventPriority.Normal
            };
        }
    }
}