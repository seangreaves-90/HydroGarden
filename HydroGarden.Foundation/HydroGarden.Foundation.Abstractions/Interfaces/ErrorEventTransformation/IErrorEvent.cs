using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HydroGarden.Foundation.Abstractions.Interfaces.ErrorEventTransformation
{
    public interface IErrorEvent
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
        public Dictionary<string, object>? Context { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the error is transient.
        /// </summary>
        public bool IsTransient { get; set; }

        /// <summary>
        /// Gets or sets the type of the original exception.
        /// </summary>
        public string? ExceptionType { get; set; }
    }
}
