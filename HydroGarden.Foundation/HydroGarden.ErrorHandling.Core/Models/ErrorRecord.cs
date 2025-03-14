using System;
using System.Collections.Generic;
using System.Text.Json;
using HydroGarden.Foundation.Abstractions.Interfaces.Errors;

namespace HydroGarden.ErrorHandling.Core.Models
{
    /// <summary>
    /// Represents a persisted error record in the system.
    /// </summary>
    public class ErrorRecord : IApplicationError
    {
        /// <summary>
        /// Gets or sets the unique identifier for this error.
        /// </summary>
        public Guid ErrorId { get; set; }

        /// <summary>
        /// Gets or sets the device ID associated with this error.
        /// </summary>
        public Guid DeviceId { get; set; }

        /// <summary>
        /// Gets or sets the component ID associated with this error.
        /// </summary>
        public Guid ComponentId { get; set; }

        /// <summary>
        /// Gets or sets the error code.
        /// </summary>
        public string ErrorCode { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the error message.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the error details.
        /// </summary>
        public string Details { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the timestamp when the error occurred.
        /// </summary>
        public DateTimeOffset Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the error severity.
        /// </summary>
        public ErrorSeverity Severity { get; set; }

        /// <summary>
        /// Gets or sets the error category.
        /// </summary>
        public ErrorCategory Category { get; set; }

        /// <summary>
        /// Gets or sets the error source.
        /// </summary>
        public string Source { get; set; } = string.Empty;

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
        /// Gets or sets additional context data associated with the error.
        /// </summary>
        public IDictionary<string, object>? ContextData { get; set; }

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
                ErrorId = error.ErrorId,
                DeviceId = error.DeviceId,
                ComponentId = error.ComponentId,
                ErrorCode = error.ErrorCode,
                Message = error.Message,
                Details = error.Details,
                Timestamp = error.Timestamp,
                Severity = error.Severity,
                Category = error.Category,
                Source = error.Source,
                IsAcknowledged = error.IsAcknowledged,
                AcknowledgedTimestamp = error.AcknowledgedTimestamp,
                AcknowledgedBy = error.AcknowledgedBy,
                IsResolved = error.IsResolved,
                ResolvedTimestamp = error.ResolvedTimestamp
            };

            // Serialize context data if present
            if (error.ContextData != null && error.ContextData.Count > 0)
            {
                record.ContextData = new Dictionary<string, object>(error.ContextData);
                record.SerializedContextData = JsonSerializer.Serialize(error.ContextData);
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
                    ContextData = JsonSerializer.Deserialize<Dictionary<string, object>>(SerializedContextData);
                }
                catch (Exception)
                {
                    // Failed to deserialize - create empty dictionary
                    ContextData = new Dictionary<string, object>();
                }
            }
            else
            {
                ContextData = new Dictionary<string, object>();
            }
        }
    }
}