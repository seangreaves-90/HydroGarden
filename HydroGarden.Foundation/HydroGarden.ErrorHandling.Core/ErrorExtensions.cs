using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.ErrorHandling.Core.Models;

namespace HydroGarden.ErrorHandling.Core
{
    /// <summary>
    /// Extension methods for working with application errors.
    /// </summary>
    public static class ErrorExtensions
    {
        /// <summary>
        /// Attempts to get the error ID from an application error.
        /// </summary>
        /// <param name="error">The application error.</param>
        /// <returns>The error ID if available, otherwise the correlation ID.</returns>
        public static Guid GetErrorId(this IApplicationError error)
        {
            // If it's our ErrorRecord type, get the ErrorId property
            if (error is ErrorRecord errorRecord)
            {
                return errorRecord.ErrorId;
            }
            
            // If it's our ComponentError type, just use the correlation ID
            if (error is ComponentError)
            {
                return error.CorrelationId;
            }
            
            // Check if the error context contains an ErrorId
            if (error.Context != null && 
                error.Context.TryGetValue("ErrorId", out var errorIdObj) && 
                errorIdObj is string errorIdStr && 
                Guid.TryParse(errorIdStr, out var errorId))
            {
                return errorId;
            }
            
            // Fall back to using the correlation ID
            return error.CorrelationId;
        }
    }
}