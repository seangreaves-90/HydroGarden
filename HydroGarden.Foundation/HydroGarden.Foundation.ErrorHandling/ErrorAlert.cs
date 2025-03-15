using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;

namespace HydroGarden.Foundation.ErrorHandling
{
    /// <summary>
    /// Represents an error alert based on rate detection.
    /// </summary>
    public class ErrorAlert
    {
        /// <summary>
        /// Gets or sets the error code.
        /// </summary>
        public string ErrorCode { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the count of errors.
        /// </summary>
        public int Count { get; set; }
        
        /// <summary>
        /// Gets or sets the severity of the error.
        /// </summary>
        public ErrorSeverity Severity { get; set; }
        
        /// <summary>
        /// Gets or sets the timestamp of the first occurrence.
        /// </summary>
        public DateTime FirstOccurrence { get; set; }
        
        /// <summary>
        /// Gets or sets the timestamp of the last occurrence.
        /// </summary>
        public DateTime LastOccurrence { get; set; }
    }
}