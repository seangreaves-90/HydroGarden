using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using System;
using System.Collections.Generic;

namespace HydroGarden.Foundation.ErrorHandling.Core.Models
{
    /// <summary>
    /// Represents an error that has been converted to an event.
    /// </summary>
    public class ErrorEvent
    {
        /// <summary>
        /// Gets or sets the unique identifier for this error event.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the device ID associated with this error.
        /// </summary>
        public Guid DeviceId { get; set; }

        /// <summary>
        /// Gets or sets the error code.
        /// </summary>
        public string ErrorCode { get; set; }

        /// <summary>
        /// Gets or sets the error message.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the error occurred.
        /// </summary>
        public DateTimeOffset Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the severity level of the error.
        /// </summary>
        public ErrorSeverity Severity { get; set; }

        /// <summary>
        /// Gets or sets the source of the error.
        /// </summary>
        public ErrorSource Source { get; set; }

        /// <summary>
        /// Gets or sets the exception details.
        /// </summary>
        public string ExceptionDetails { get; set; }

        /// <summary>
        /// Gets or sets the correlation identifier for tracing.
        /// </summary>
        public Guid CorrelationId { get; set; }

        /// <summary>
        /// Gets or sets additional contextual information about the error.
        /// </summary>
        public Dictionary<string, object> Context { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Gets or sets a value indicating whether the error is transient.
        /// </summary>
        public bool IsTransient { get; set; }

        /// <summary>
        /// Gets or sets the type of the original exception.
        /// </summary>
        public string ExceptionType { get; set; }
    }

    /// <summary>
    /// Represents an event indicating a recovery attempt for a previous error.
    /// </summary>
    public class RecoveryEvent
    {
        /// <summary>
        /// Gets or sets the unique identifier for this recovery event.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the device ID associated with this recovery.
        /// </summary>
        public Guid DeviceId { get; set; }

        /// <summary>
        /// Gets or sets the error code being recovered.
        /// </summary>
        public string ErrorCode { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the recovery was attempted.
        /// </summary>
        public DateTimeOffset Timestamp { get; set; }

        /// <summary>
        /// Gets or sets whether the recovery was successful.
        /// </summary>
        public bool IsSuccessful { get; set; }

        /// <summary>
        /// Gets or sets a message describing the recovery action or result.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Gets or sets the correlation identifier for tracing.
        /// </summary>
        public Guid CorrelationId { get; set; }

        /// <summary>
        /// Gets or sets additional contextual information about the recovery.
        /// </summary>
        public Dictionary<string, object> Context { get; set; } = new Dictionary<string, object>();
    }
}