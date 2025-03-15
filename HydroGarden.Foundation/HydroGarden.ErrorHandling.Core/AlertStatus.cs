using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using System;
using System.Collections.Generic;

namespace HydroGarden.ErrorHandling.Core
{
    /// <summary>
    /// Represents the alert status based on monitored errors.
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
        public IReadOnlyCollection<ErrorAlert> Alerts { get; set; } = Array.Empty<ErrorAlert>();
    }

    /// <summary>
    /// Represents an active error alert.
    /// </summary>
    public class ErrorAlert
    {
        /// <summary>
        /// Gets or sets the error code.
        /// </summary>
        public string ErrorCode { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the error count.
        /// </summary>
        public int Count { get; set; }

        /// <summary>
        /// Gets or sets the error severity.
        /// </summary>
        public ErrorSeverity Severity { get; set; }

        /// <summary>
        /// Gets or sets the first occurrence timestamp.
        /// </summary>
        public DateTimeOffset FirstOccurrence { get; set; }

        /// <summary>
        /// Gets or sets the last occurrence timestamp.
        /// </summary>
        public DateTimeOffset LastOccurrence { get; set; }
    }
}