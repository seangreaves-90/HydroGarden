using System.Text.Json;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;

namespace HydroGarden.Foundation.ErrorHandling.Models
{
    /// <summary>
    /// Represents a persisted error record in the system.
    /// </summary>
    public class ErrorRecord : IApplicationError
    {
        /// <summary>
        /// Gets or sets the device ID associated with this error.
        /// </summary>
        public Guid DeviceId { get; set; }

        /// <summary>
        /// Gets or sets the error code.
        /// </summary>
        public string? ErrorCode { get; set; }

        /// <summary>
        /// Gets or sets the error message.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the error severity.
        /// </summary>
        public ErrorSeverity Severity { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the error occurred.
        /// </summary>
        public DateTimeOffset Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the error category.
        /// </summary>
        public ErrorCategory Category { get; set; }

        /// <summary>
        /// Gets or sets the error source.
        /// </summary>
        public ErrorSource Source { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier for this error (for storage/db purposes).
        /// </summary>
        public Guid ErrorId { get; set; }

        /// <summary>
        /// Gets or sets the component ID associated with this error.
        /// </summary>
        public Guid ComponentId { get; set; }

        /// <summary>
        /// Gets or sets the error details.
        /// </summary>
        public string Details { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether the error has been acknowledged.
        /// </summary>
        public bool IsAcknowledged { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the error was acknowledged.
        /// </summary>
        public DateTimeOffset? AcknowledgedTimestamp { get; set; }

        /// <summary>
        /// Gets or sets the user who acknowledged the error.
        /// </summary>
        public string? AcknowledgedBy { get; set; }

        /// <summary>
        /// Gets or sets whether the error has been resolved.
        /// </summary>
        public bool IsResolved { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the error was resolved.
        /// </summary>
        public DateTimeOffset? ResolvedTimestamp { get; set; }

        /// <summary>
        /// Gets the context information about the error (from interface).
        /// </summary>
        public Dictionary<string, object?> Context { get; } = new Dictionary<string, object>();

        /// <summary>
        /// Gets the exception associated with this error, if any.
        /// </summary>
        public Exception? Exception { get; set; }

        /// <summary>
        /// Gets the correlation ID for tracking related errors.
        /// </summary>
        public Guid CorrelationId { get; set; }
        
        /// <summary>
        /// Gets or sets additional context data associated with the error (for storage/serialization).
        /// </summary>
        public IDictionary<string, object>? ContextData
        {
            get { return Context; }
            set
            {
                if (value != null)
                {
                    foreach (var item in value)
                    {
                        Context[item.Key] = item.Value;
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the serialized context data for storage.
        /// </summary>
        public string? SerializedContextData { get; set; }

        /// <summary>
        /// Creates a new ErrorRecord from an IApplicationError.
        /// </summary>
        /// <param name="error">The error to create a record from.</param>
        /// <returns>A new ErrorRecord.</returns>
        public static ErrorRecord FromApplicationError(IApplicationError error)
        {
            var record = new ErrorRecord
            {
                DeviceId = error.DeviceId,
                ErrorCode = error.ErrorCode,
                Message = error.Message,
                Timestamp = error.Timestamp,
                Severity = error.Severity,
                Category = error.Category,
                Source = error.Source,
                Exception = error.Exception,
                CorrelationId = error.CorrelationId
            };

            // Set properties from the error that aren't part of the standard interface
            if (error is ErrorRecord existingRecord)
            {
                record.ErrorId = existingRecord.ErrorId;
                record.ComponentId = existingRecord.ComponentId;
                record.Details = existingRecord.Details;
                record.IsAcknowledged = existingRecord.IsAcknowledged;
                record.AcknowledgedTimestamp = existingRecord.AcknowledgedTimestamp;
                record.AcknowledgedBy = existingRecord.AcknowledgedBy;
                record.IsResolved = existingRecord.IsResolved;
                record.ResolvedTimestamp = existingRecord.ResolvedTimestamp;
            }

            // Copy context data
            if (error.Context != null && error.Context.Count > 0)
            {
                foreach (var item in error.Context)
                {
                    record.Context[item.Key] = item.Value;
                }
                record.SerializedContextData = JsonSerializer.Serialize(error.Context);
            }

            return record;
        }

        /// <summary>
        /// Deserializes the context data from storage.
        /// </summary>
        public void DeserializeContextData()
        {
            if (!string.IsNullOrEmpty(SerializedContextData))
            {
                try
                {
                    var contextData = JsonSerializer.Deserialize<Dictionary<string, object>>(SerializedContextData);
                    if (contextData != null)
                    {
                        Context.Clear();
                        foreach (var item in contextData)
                        {
                            Context[item.Key] = item.Value;
                        }
                    }
                }
                catch (Exception)
                {
                    // Failed to deserialize - context remains empty
                    Context.Clear();
                }
            }
            else
            {
                Context.Clear();
            }
        }
    }
}