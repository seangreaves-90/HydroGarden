using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.ErrorHandling.Common;

namespace HydroGarden.Foundation.ErrorHandling.Exceptions
{
    /// <summary>
    /// Exception thrown when a service fails to initialize.
    /// </summary>
    public class ServiceInitializationException : ApplicationException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceInitializationException"/> class.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="serviceName">The name of the service that failed to initialize.</param>
        /// <param name="innerException">The inner exception.</param>
        /// <param name="context">Additional context information.</param>
        public ServiceInitializationException(
            string message,
            string serviceName,
            Exception? innerException = null,
            IDictionary<string, object>? context = null)
            : base(
                message,
                ErrorCodes.Service.INITIALIZATION_FAILED,
                ErrorSeverity.Critical,
                ErrorSource.Service,
                innerException,
                null,
                context != null 
                    ? new Dictionary<string, object>(context) { { "ServiceName", serviceName } }
                    : new Dictionary<string, object> { { "ServiceName", serviceName } },
                ErrorCategory.Service)
        {
        }
    }

    /// <summary>
    /// Exception thrown when a service operation times out.
    /// </summary>
    public class ServiceTimeoutException : ApplicationException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceTimeoutException"/> class.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="serviceName">The name of the service that timed out.</param>
        /// <param name="operationName">The name of the operation that timed out.</param>
        /// <param name="timeout">The timeout value in milliseconds.</param>
        /// <param name="innerException">The inner exception.</param>
        public ServiceTimeoutException(
            string message,
            string serviceName,
            string operationName,
            int timeout,
            Exception? innerException = null)
            : base(
                message,
                ErrorCodes.Service.OPERATION_TIMEOUT,
                ErrorSeverity.Error,
                ErrorSource.Service,
                innerException,
                null,
                new Dictionary<string, object>
                {
                    { "ServiceName", serviceName },
                    { "OperationName", operationName },
                    { "TimeoutMs", timeout }
                },
                ErrorCategory.Service)
        {
        }
    }

    /// <summary>
    /// Exception thrown when a service dependency is unavailable.
    /// </summary>
    public class DependencyUnavailableException : ApplicationException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DependencyUnavailableException"/> class.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="serviceName">The name of the service that depends on the unavailable dependency.</param>
        /// <param name="dependencyName">The name of the unavailable dependency.</param>
        /// <param name="innerException">The inner exception.</param>
        public DependencyUnavailableException(
            string message,
            string serviceName,
            string dependencyName,
            Exception? innerException = null)
            : base(
                message,
                ErrorCodes.Service.DEPENDENCY_UNAVAILABLE,
                ErrorSeverity.Critical,
                ErrorSource.Service,
                innerException,
                null,
                new Dictionary<string, object>
                {
                    { "ServiceName", serviceName },
                    { "DependencyName", dependencyName }
                },
                ErrorCategory.Service)
        {
        }
    }

    /// <summary>
    /// Exception thrown when a service configuration is invalid.
    /// </summary>
    public class ServiceConfigurationException : ApplicationException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceConfigurationException"/> class.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="serviceName">The name of the service with invalid configuration.</param>
        /// <param name="configParameter">The name of the invalid configuration parameter, if applicable.</param>
        /// <param name="innerException">The inner exception.</param>
        public ServiceConfigurationException(
            string message,
            string serviceName,
            string? configParameter = null,
            Exception? innerException = null)
            : base(
                message,
                ErrorCodes.Service.CONFIGURATION_INVALID,
                ErrorSeverity.Error,
                ErrorSource.Service,
                innerException,
                null,
                new Dictionary<string, object>
                {
                    { "ServiceName", serviceName },
                    { "ConfigParameter", configParameter ?? "Unknown" }
                },
                ErrorCategory.Service)
        {
        }
    }

    /// <summary>
    /// Exception thrown when a service has concurrent access conflicts.
    /// </summary>
    public class ConcurrencyException : ApplicationException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ConcurrencyException"/> class.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="resourceName">The name of the resource with concurrency issues.</param>
        /// <param name="innerException">The inner exception.</param>
        public ConcurrencyException(
            string message,
            string resourceName,
            Exception? innerException = null)
            : base(
                message,
                ErrorCodes.Service.CONCURRENT_ACCESS_CONFLICT,
                ErrorSeverity.Error,
                ErrorSource.Service,
                innerException,
                null,
                new Dictionary<string, object>
                {
                    { "ResourceName", resourceName }
                },
                ErrorCategory.Service)
        {
        }
    }
}