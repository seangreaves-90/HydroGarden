using HydroGarden.Foundation.Abstractions.Interfaces.Components;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.Common.ErrorHandling;

namespace HydroGarden.Foundation.Common.Extensions
{
    public static class ErrorHandlingExtensions
    {
        // Extension for safe execution with error handling
        public static async Task<T?> ExecuteWithErrorHandlingAsync<T>(
            this object source,
            IErrorMonitor errorMonitor,
            Func<Task<T>> operation,
            string errorCode,
            string errorMessage,
            ErrorSource errorSource,
            IDictionary<string, object>? additionalContext = null,
            CancellationToken ct = default)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex)
            {
                // Combine any provided context with source information
                var context = additionalContext ?? new Dictionary<string, object>();
                AddSourceContext(source, context);

                // Report the error through the monitor
                await errorMonitor.ReportErrorAsync(
                    new ComponentError(
                        GetDeviceId(source),
                        errorCode,
                        errorMessage,
                        ErrorSeverity.Error,
                        context,
                        ex),
                    ct);

                // Default return value
                return default;
            }
        }

        // Overload for void operations
        public static async Task ExecuteWithErrorHandlingAsync(
            this object source,
            IErrorMonitor errorMonitor,
            Func<Task> operation,
            string errorCode,
            string errorMessage,
            ErrorSource errorSource,
            IDictionary<string, object>? additionalContext = null,
            CancellationToken ct = default)
        {
            try
            {
                await operation();
            }
            catch (Exception ex)
            {
                var context = additionalContext ?? new Dictionary<string, object>();
                AddSourceContext(source, context);

                await errorMonitor.ReportErrorAsync(
                    new ComponentError(
                        GetDeviceId(source),
                        errorCode,
                        errorMessage,
                        ErrorSeverity.Error,
                        context,
                        ex),
                    ct);
            }
        }

        // Extension to report errors from exceptions
        public static Task ReportExceptionAsync(
            this IErrorMonitor errorMonitor,
            object source,
            Exception exception,
            string errorCode,
            string errorMessage,
            ErrorSeverity severity = ErrorSeverity.Error,
            ErrorSource errorSource = ErrorSource.Unknown,
            IDictionary<string, object>? additionalContext = null,
            CancellationToken ct = default)
        {
            var context = additionalContext ?? new Dictionary<string, object>();
            AddSourceContext(source, context);

            return errorMonitor.ReportErrorAsync(
                new ComponentError(
                    GetDeviceId(source),
                    errorCode,
                    errorMessage,
                    severity,
                    context,
                    exception),
                ct);
        }

        // Helper to add source context information
        private static void AddSourceContext(object source, IDictionary<string, object> context)
        {
            var sourceType = source.GetType();
            if (sourceType.FullName != null) context["SourceType"] = sourceType.FullName;

            if (source is IComponent component)
            {
                context["ComponentId"] = component.Id;
                context["ComponentName"] = component.Name;
                context["ComponentState"] = component.State.ToString();
            }
        }

        // Helper to extract device ID from source (if available)
        private static Guid GetDeviceId(object source)
        {
            if (source is IComponent component)
            {
                return component.Id;
            }

            return Guid.Empty; // Non-device errors use empty GUID
        }
    }
}