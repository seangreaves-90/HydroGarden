using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.ErrorHandling.Common;
using HydroGarden.Foundation.ErrorHandling.Exceptions;

namespace HydroGarden.Foundation.ErrorHandling
{
    /// <summary>
    /// Extension methods for error handling.
    /// </summary>
    public static class ErrorHandlingExtensions
    {
        /// <summary>
        /// Reports an error from an exception to the error monitor.
        /// </summary>
        /// <param name="monitor">The error monitor.</param>
        /// <param name="source">The source of the error.</param>
        /// <param name="exception">The exception that occurred.</param>
        /// <param name="errorCode">An optional error code. If not specified, it will be derived from the exception.</param>
        /// <param name="message">An optional message. If not specified, the exception message will be used.</param>
        /// <param name="severity">The severity of the error.</param>
        /// <param name="errorSource">The source of the error.</param>
        /// <param name="context">Additional context for the error.</param>
        /// <param name="ct">A cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static Task ReportExceptionAsync(
            this IErrorMonitor monitor,
            object source,
            Exception exception,
            string? errorCode = null,
            string? message = null,
            ErrorSeverity severity = ErrorSeverity.Error,
            ErrorSource errorSource = ErrorSource.Unknown,
            IDictionary<string, object>? context = null,
            CancellationToken ct = default)
        {
            // Create a context builder for rich error context
            var contextBuilder = ErrorContextBuilder.Create()
                .WithSource(source)
                .WithException(exception)
                .WithLocation();

            if (context != null)
            {
                contextBuilder.WithProperties(context);
            }

            // If the exception is our application exception, use its properties
            if (exception is Exceptions.ApplicationException appException)
            {
                return monitor.ReportErrorAsync(
                    new ComponentError(
                        appException.DeviceId ?? Guid.Empty,
                        appException.ErrorCode,
                        message ?? appException.Message,
                        appException.Severity,
                        appException.Source,
                        contextBuilder.Build(),
                        exception,
                        appException.Category),
                    ct);
            }

            return monitor.ReportExceptionAsync(
                source,
                exception,
                errorCode ?? ErrorFactory.DeriveErrorCodeFromException(exception),
                message ?? exception.Message,
                severity,
                errorSource,
                contextBuilder.Build(),
                ct);
        }

        /// <summary>
        /// Reports a device error to the error monitor.
        /// </summary>
        /// <param name="monitor">The error monitor.</param>
        /// <param name="deviceId">The ID of the device.</param>
        /// <param name="errorCode">The error code.</param>
        /// <param name="message">The error message.</param>
        /// <param name="severity">The severity of the error.</param>
        /// <param name="exception">The exception that caused the error, if any.</param>
        /// <param name="context">Additional context for the error.</param>
        /// <param name="ct">A cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static Task ReportDeviceErrorAsync(
            this IErrorMonitor monitor,
            Guid deviceId,
            string errorCode,
            string message,
            ErrorSeverity severity = ErrorSeverity.Error,
            Exception? exception = null,
            IDictionary<string, object>? context = null,
            CancellationToken ct = default)
        {
            var error = ErrorFactory.CreateDeviceError(
                deviceId,
                errorCode,
                message,
                severity,
                exception,
                context);

            return monitor.ReportErrorAsync(error, ct);
        }

        /// <summary>
        /// Reports a service error to the error monitor.
        /// </summary>
        /// <param name="monitor">The error monitor.</param>
        /// <param name="serviceName">The name of the service.</param>
        /// <param name="errorCode">The error code.</param>
        /// <param name="message">The error message.</param>
        /// <param name="severity">The severity of the error.</param>
        /// <param name="exception">The exception that caused the error, if any.</param>
        /// <param name="context">Additional context for the error.</param>
        /// <param name="ct">A cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static Task ReportServiceErrorAsync(
            this IErrorMonitor monitor,
            string serviceName,
            string errorCode,
            string message,
            ErrorSeverity severity = ErrorSeverity.Error,
            Exception? exception = null,
            IDictionary<string, object>? context = null,
            CancellationToken ct = default)
        {
            var error = ErrorFactory.CreateServiceError(
                serviceName,
                errorCode,
                message,
                severity,
                exception,
                context);

            return monitor.ReportErrorAsync(error, ct);
        }

        /// <summary>
        /// Reports a device initialization error to the error monitor.
        /// </summary>
        /// <param name="monitor">The error monitor.</param>
        /// <param name="deviceId">The ID of the device.</param>
        /// <param name="message">The error message.</param>
        /// <param name="exception">The exception that caused the error, if any.</param>
        /// <param name="context">Additional context for the error.</param>
        /// <param name="ct">A cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static Task ReportDeviceInitializationErrorAsync(
            this IErrorMonitor monitor,
            Guid deviceId,
            string message,
            Exception? exception = null,
            IDictionary<string, object>? context = null,
            CancellationToken ct = default)
        {
            return monitor.ReportDeviceErrorAsync(
                deviceId,
                ErrorCodes.Device.INITIALIZATION_FAILED,
                message,
                ErrorSeverity.Critical,
                exception,
                context,
                ct);
        }

        /// <summary>
        /// Reports a device communication error to the error monitor.
        /// </summary>
        /// <param name="monitor">The error monitor.</param>
        /// <param name="deviceId">The ID of the device.</param>
        /// <param name="message">The error message.</param>
        /// <param name="exception">The exception that caused the error, if any.</param>
        /// <param name="context">Additional context for the error.</param>
        /// <param name="ct">A cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static Task ReportDeviceCommunicationErrorAsync(
            this IErrorMonitor monitor,
            Guid deviceId,
            string message,
            Exception? exception = null,
            IDictionary<string, object>? context = null,
            CancellationToken ct = default)
        {
            return monitor.ReportDeviceErrorAsync(
                deviceId,
                ErrorCodes.Device.COMMUNICATION_LOST,
                message,
                ErrorSeverity.Critical,
                exception,
                context,
                ct);
        }

        /// <summary>
        /// Reports a service initialization error to the error monitor.
        /// </summary>
        /// <param name="monitor">The error monitor.</param>
        /// <param name="serviceName">The name of the service.</param>
        /// <param name="message">The error message.</param>
        /// <param name="exception">The exception that caused the error, if any.</param>
        /// <param name="context">Additional context for the error.</param>
        /// <param name="ct">A cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static Task ReportServiceInitializationErrorAsync(
            this IErrorMonitor monitor,
            string serviceName,
            string message,
            Exception? exception = null,
            IDictionary<string, object>? context = null,
            CancellationToken ct = default)
        {
            return monitor.ReportServiceErrorAsync(
                serviceName,
                ErrorCodes.Service.INITIALIZATION_FAILED,
                message,
                ErrorSeverity.Critical,
                exception,
                context,
                ct);
        }


    }
}