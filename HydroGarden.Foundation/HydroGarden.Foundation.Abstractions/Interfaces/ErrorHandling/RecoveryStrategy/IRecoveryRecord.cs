using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling.RecoveryStrategy
{
    /// <summary>
    /// Represents a record of a recovery operation.
    /// </summary>
    public interface IRecoveryRecord
    {
        /// <summary>
        /// Gets or sets the unique identifier for this recovery record.
        /// </summary>
        public Guid RecordId { get; set; }

        /// <summary>
        /// Gets or sets the device ID that was recovered.
        /// </summary>
        public Guid DeviceId { get; set; }

        /// <summary>
        /// Gets or sets the error code that was recovered.
        /// </summary>
        public string? ErrorCode { get; set; }

        /// <summary>
        /// Gets or sets whether the recovery was successful.
        /// </summary>
        public bool IsSuccessful { get; set; }

        /// <summary>
        /// Gets or sets the strategy that was used for recovery.
        /// </summary>
        public string? StrategyUsed { get; set; }

        /// <summary>
        /// Gets or sets the number of attempts made.
        /// </summary>
        public int AttemptCount { get; set; }

        /// <summary>
        /// Gets or sets the correlation ID for tracking related operations.
        /// </summary>
        public Guid CorrelationId { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of the recovery operation.
        /// </summary>
        public DateTimeOffset Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the duration of the recovery operation.
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Gets or sets additional details about the recovery.
        /// </summary>
        public string? Details { get; set; }
    }
}
