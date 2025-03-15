using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;

namespace HydroGarden.Foundation.ErrorHandling
{
    /// <summary>
    /// Provides information about the rate of errors for a particular error code.
    /// </summary>
    public class ErrorRateInfo
    {
        /// <summary>
        /// Gets or sets the error code.
        /// </summary>
        public string ErrorCode { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the count of errors with this code.
        /// </summary>
        public int Count { get; set; }
        
        /// <summary>
        /// Gets or sets the timestamp of the first occurrence.
        /// </summary>
        public DateTime FirstOccurrence { get; set; }
        
        /// <summary>
        /// Gets or sets the timestamp of the last occurrence.
        /// </summary>
        public DateTime LastOccurrence { get; set; }
        
        /// <summary>
        /// Gets or sets the maximum severity seen for this error.
        /// </summary>
        public ErrorSeverity MaxSeverity { get; set; }
        
        /// <summary>
        /// Gets or sets the device IDs that have reported this error.
        /// </summary>
        public HashSet<Guid> DeviceIds { get; set; } = new HashSet<Guid>();
    }
}