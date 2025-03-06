using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using HydroGarden.Foundation.Abstractions.Interfaces.Components;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.Common.ErrorHandling;
using HydroGarden.Foundation.Common.ErrorHandling.Constants;
using HydroGarden.Foundation.Common.Policies;

namespace HydroGarden.Foundation.Common.Extensions
{
    /// <summary>
    /// Provides error handling extension methods.
    /// </summary>
    public static class ErrorHandlingExtensions
    {
        /// <summary>
        /// Executes an operation with comprehensive error handling and reporting.
        /// </summary>
        public static async Task<T?> ExecuteWithErrorHandlingAsync<T>(
            this object source,
            IErrorMonitor errorMonitor,
            Func<Task<T>> operation,
            string errorCode,
            string errorMessage,
            ErrorSource errorSource,
            IDictionary<string, object>? additionalContext = null,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0,
            CancellationToken ct = default)
        {
            if (errorMonitor == null) throw new ArgumentNullException(nameof(errorMonitor));
            if (operation == null) throw new ArgumentNullException(nameof(operation));
            if (string.IsNullOrEmpty(errorCode)) throw new ArgumentException("Error code cannot be empty", nameof(errorCode));

            var stopwatch = Stopwatch.StartNew();

            try
            {
                var result = await operation();
                stopwatch.Stop();

                // Track successful operation with performance metrics
                if (additionalContext != null)
                {
                    additionalContext["ExecutionTime"] = stopwatch.ElapsedMilliseconds;
                }

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                // Create rich context
                var context = CreateErrorContext(
                    source,
                    additionalContext,
                    stopwatch.ElapsedMilliseconds,
                    memberName,
                    filePath,
                    lineNumber);

                // Report the error to the monitor
                await errorMonitor.ReportErrorAsync(
                    new ComponentError(
                        GetDeviceId(source),
                        errorCode,
                        errorMessage,
                        DetermineErrorSeverity(ex),
                        IsLikelyRecoverable(ex, errorCode),
                        errorSource,
                        IsLikelyTransient(ex),
                        context,
                        ex),
                    ct);

                return default;
            }
        }

        /// <summary>
        /// Executes a void operation with comprehensive error handling and reporting.
        /// </summary>
        public static async Task ExecuteWithErrorHandlingAsync(
            this object source,
            IErrorMonitor errorMonitor,
            Func<Task> operation,
            string errorCode,
            string errorMessage,
            ErrorSource errorSource,
            IDictionary<string, object>? additionalContext = null,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0,
            CancellationToken ct = default)
        {
            await ExecuteWithErrorHandlingAsync<object>(
                source,
                errorMonitor,
                async () =>
                {
                    await operation();
                    return null;
                },
                errorCode,
                errorMessage,
                errorSource,
                additionalContext,
                memberName,
                filePath,
                lineNumber,
                ct);
        }

        /// <summary>
        /// Executes an operation with error handling and circuit breaker protection.
        /// </summary>
        public static async Task<T?> ExecuteWithCircuitBreakerAsync<T>(
            this object source,
            IErrorMonitor errorMonitor,
            CircuitBreakerFactory circuitBreakerFactory,
            string serviceName,
            Func<Task<T>> operation,
            string errorCode,
            string errorMessage,
            ErrorSource errorSource,
            IDictionary<string, object>? additionalContext = null,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0,
            CancellationToken ct = default)
        {
            if (errorMonitor == null) throw new ArgumentNullException(nameof(errorMonitor));
            if (circuitBreakerFactory == null) throw new ArgumentNullException(nameof(circuitBreakerFactory));
            if (operation == null) throw new ArgumentNullException(nameof(operation));
            if (string.IsNullOrEmpty(errorCode)) throw new ArgumentException("Error code cannot be empty", nameof(errorCode));

            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Execute with circuit breaker protection
                var result = await circuitBreakerFactory.ExecuteWithCircuitBreakerAsync(serviceName, operation);
                stopwatch.Stop();

                // Track successful operation with performance metrics
                if (additionalContext != null)
                {
                    additionalContext["ExecutionTime"] = stopwatch.ElapsedMilliseconds;
                }

                return result;
            }
            catch (CircuitBreakerOpenException cbEx)
            {
                stopwatch.Stop();

                // Create rich context
                var context = CreateErrorContext(
                    source,
                    additionalContext,
                    stopwatch.ElapsedMilliseconds,
                    memberName,
                    filePath,
                    lineNumber);

                context["CircuitBreaker"] = serviceName;
                context["CircuitBreakerLastFailure"] = cbEx.LastFailureTime.ToString("o");

                // Report the circuit breaker error
                await errorMonitor.ReportErrorAsync(
                    new ComponentError(
                        GetDeviceId(source),
                        ErrorCodes.Recovery.CIRCUIT_OPEN,
                        $"Circuit breaker open for service: {serviceName}",
                        ErrorSeverity.Error,
                        true,
                        errorSource,
                        true,
                        context,
                        cbEx),
                    ct);

                return default;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                // Create rich context
                var context = CreateErrorContext(
                    source,
                    additionalContext,
                    stopwatch.ElapsedMilliseconds,
                    memberName,
                    filePath,
                    lineNumber);

                context["CircuitBreaker"] = serviceName;

                // Report the error to the monitor
                await errorMonitor.ReportErrorAsync(
                    new ComponentError(
                        GetDeviceId(source),
                        errorCode,
                        errorMessage,
                        DetermineErrorSeverity(ex),
                        IsLikelyRecoverable(ex, errorCode),
                        errorSource,
                        IsLikelyTransient(ex),
                        context,
                        ex),
                    ct);

                return default;
            }
        }

        /// <summary>
        /// Reports an exception directly to the error monitor.
        /// </summary>
        public static Task ReportExceptionAsync(
            this IErrorMonitor errorMonitor,
            object source,
            Exception exception,
            string errorCode,
            string errorMessage,
            ErrorSeverity severity = ErrorSeverity.Error,
            ErrorSource errorSource = ErrorSource.Unknown,
            IDictionary<string, object>? additionalContext = null,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0,
            CancellationToken ct = default)
        {
            if (errorMonitor == null) throw new ArgumentNullException(nameof(errorMonitor));
            if (exception == null) throw new ArgumentNullException(nameof(exception));
            if (string.IsNullOrEmpty(errorCode)) throw new ArgumentException("Error code cannot be empty", nameof(errorCode));

            // Create rich context
            var context = CreateErrorContext(
                source,
                additionalContext,
                0,
                memberName,
                filePath,
                lineNumber);

            // Report the error
            return errorMonitor.ReportErrorAsync(
                new ComponentError(
                    GetDeviceId(source),
                    errorCode,
                    errorMessage,
                    severity,
                    severity < ErrorSeverity.Critical,
                    errorSource,
                    severity < ErrorSeverity.Error,
                    context,
                    exception),
                ct);
        }

        /// <summary>
        /// Creates a rich error context from the provided information.
        /// </summary>
        private static Dictionary<string, object> CreateErrorContext(
            object source,
            IDictionary<string, object>? additionalContext,
            long elapsedMilliseconds,
            string memberName,
            string filePath,
            int lineNumber)
        {
            var context = new Dictionary<string, object>();

            // Add caller information
            var fileName = filePath.Substring(filePath.LastIndexOf('\\') + 1);
            context["CallSite"] = $"{fileName}:{memberName}({lineNumber})";
            context["ElapsedMs"] = elapsedMilliseconds;

            // Add source information
            var sourceType = source.GetType();
            if (sourceType.FullName != null) context["SourceType"] = sourceType.FullName;

            // Add component-specific information
            if (source is IComponent component)
            {
                context["ComponentId"] = component.Id;
                context["ComponentName"] = component.Name;
                context["ComponentState"] = component.State.ToString();
            }

            // Add any additional context
            if (additionalContext != null)
            {
                foreach (var (key, value) in additionalContext)
                {
                    context[key] = value;
                }
            }

            return context;
        }

        /// <summary>
        /// Determines a device ID from the source if possible.
        /// </summary>
        private static Guid GetDeviceId(object source)
        {
            if (source is IComponent component)
            {
                return component.Id;
            }

            return Guid.Empty;
        }

        /// <summary>
        /// Determines error severity based on exception type.
        /// </summary>
        private static ErrorSeverity DetermineErrorSeverity(Exception ex)
        {
            // Critical exceptions indicate system failure
            if (ex is OutOfMemoryException ||
                ex is StackOverflowException ||
                ex is ThreadAbortException)
            {
                return ErrorSeverity.Critical;
            }

            // Exceptions that suggest invalid state
            if (ex is InvalidOperationException ||
                ex is ArgumentException ||
                ex is NullReferenceException)
            {
                return ErrorSeverity.Error;
            }

            // Common transient issues
            if (ex is TimeoutException ||
                ex is IOException ||
                ex is System.Net.WebException ||
                ex is System.Net.Sockets.SocketException ||
                ex is System.Net.Http.HttpRequestException)
            {
                return ErrorSeverity.Warning;
            }

            // Default to Error
            return ErrorSeverity.Error;
        }

        /// <summary>
        /// Determines if an exception is likely to be recoverable.
        /// </summary>
        private static bool IsLikelyRecoverable(Exception ex, string errorCode)
        {
            // Check if the error code is explicitly unrecoverable
            if (ErrorCodes.IsUnrecoverable(errorCode))
            {
                return false;
            }

            // Critical exceptions indicate system failure
            if (ex is OutOfMemoryException ||
                ex is StackOverflowException ||
                ex is ThreadAbortException)
            {
                return false;
            }

            // Default to recoverable
            return true;
        }

        /// <summary>
        /// Determines if an exception is likely to be transient.
        /// </summary>
        private static bool IsLikelyTransient(Exception ex)
        {
            // Common transient issues
            return ex is TimeoutException ||
                   ex is IOException ||
                   ex is System.Net.WebException ||
                   ex is System.Net.Sockets.SocketException ||
                   ex is System.Net.Http.HttpRequestException ||
                   ex is TaskCanceledException;
        }
    }
}