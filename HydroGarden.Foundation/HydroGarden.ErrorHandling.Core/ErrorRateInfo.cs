using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;

namespace HydroGarden.Foundation.ErrorHandling
{
    /// <summary>
    /// Provides information about error rates for a specific error code.
    /// </summary>
    public class ErrorRateInfo
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
        /// Gets or sets the first occurrence timestamp.
        /// </summary>
        public DateTimeOffset FirstOccurrence { get; set; }

        /// <summary>
        /// Gets or sets the last occurrence timestamp.
        /// </summary>
        public DateTimeOffset LastOccurrence { get; set; }

        /// <summary>
        /// Gets or sets the maximum severity of the error.
        /// </summary>
        public ErrorSeverity MaxSeverity { get; set; }

        /// <summary>
        /// Gets or sets the device IDs associated with this error code.
        /// </summary>
        public HashSet<Guid> DeviceIds { get; set; } = [];
    }
}