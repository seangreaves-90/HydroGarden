using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;

namespace HydroGarden.Foundation.ErrorHandling.Extensions
{
    /// <summary>
    /// Extension methods for simplified error handling in components.
    /// </summary>
    public static class ErrorHandlingComponentExtensions
    {
        /// <summary>
        /// Executes an operation with error handling.
        /// </summary>
        /// <typeparam name="T">The return type of the operation.</typeparam>
        /// <param name="source">The source object that is executing the operation.</param>
        /// <param name="errorMonitor">The error monitor to report errors to.</param>
        /// <param name="operation">The operation to execute.</param>
        /// <param name="errorCode">The error code to use if the operation fails.</param>
        /// <param name="errorMessage">The error message to use if the operation fails.</param>
        /// <param name="errorSource">The source of the error.</param>
        /// <param name="context">Additional context information.</param>
        /// <param name="ct">A cancellation token.</param>
        /// <returns>The result of the operation, or default if the operation fails.</returns>
        public static async Task<T> ExecuteWithErrorHandlingAsync<T>(
            this object source,
            IErrorMonitor errorMonitor,
            Func<Task<T>> operation,
            string errorCode,
            string errorMessage,
            ErrorSource errorSource = ErrorSource.Unknown,
            Dictionary<string, object?> context = null,
            CancellationToken ct = default)
        {
            try
            {
                return await operation();
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Don't report cancellation as an error
                throw;
            }
            catch (Exception ex)
            {
                // Build rich context with source information
                var errorContext = ErrorContextBuilder.Create()
                    .WithSource(source)
                    .WithLocation()
                    .WithException(ex);
                
                if (context != null)
                {
                    errorContext.WithProperties(context);
                }

                // Report the error
                await errorMonitor.ReportExceptionAsync(
                    source,
                    ex,
                    errorCode,
                    errorMessage,
                    ErrorSeverity.Error,
                    errorSource,
                    errorContext.Build(),
                    ct);

                return default!;
            }
        }

        /// <summary>
        /// Executes an operation with error handling.
        /// </summary>
        /// <param name="source">The source object that is executing the operation.</param>
        /// <param name="errorMonitor">The error monitor to report errors to.</param>
        /// <param name="operation">The operation to execute.</param>
        /// <param name="errorCode">The error code to use if the operation fails.</param>
        /// <param name="errorMessage">The error message to use if the operation fails.</param>
        /// <param name="errorSource">The source of the error.</param>
        /// <param name="context">Additional context information.</param>
        /// <param name="ct">A cancellation token.</param>
        /// <returns>True if the operation succeeds, false otherwise.</returns>
        public static async Task<bool> ExecuteWithErrorHandlingAsync(
            this object source,
            IErrorMonitor errorMonitor,
            Func<Task> operation,
            string errorCode,
            string errorMessage,
            ErrorSource errorSource = ErrorSource.Unknown,
            IDictionary<string, object?>? context = null,
            CancellationToken ct = default)
        {
            try
            {
                await operation();
                return true;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Don't report cancellation as an error
                throw;
            }
            catch (Exception ex)
            {
                // Build rich context with source information
                var errorContext = ErrorContextBuilder.Create()
                    .WithSource(source)
                    .WithLocation()
                    .WithException(ex);
                
                if (context != null)
                {
                    errorContext.WithProperties(context);
                }

                // Report the error
                await errorMonitor.ReportExceptionAsync(
                    source,
                    ex,
                    errorCode,
                    errorMessage,
                    ErrorSeverity.Error,
                    errorSource,
                    errorContext.Build(),
                    ct);

                return false;
            }
        }
    }
}