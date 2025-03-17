using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.ErrorHandling.Common;

namespace HydroGarden.Foundation.ErrorHandling.Exceptions
{
    /// <summary>
    /// Exception thrown when a service fails to initialize.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="ServiceInitializationException"/> class.
    /// </remarks>
    /// <param name="message">The error message.</param>
    /// <param name="serviceName">The name of the service that failed to initialize.</param>
    /// <param name="innerException">The inner exception.</param>
    /// <param name="context">Additional context information.</param>
    public class ServiceInitializationException(
        string message,
        string serviceName,
        Exception? innerException = null,
        IDictionary<string, object>? context = null) : ApplicationException(
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

    /// <summary>
    /// Exception thrown when a service operation times out.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="ServiceTimeoutException"/> class.
    /// </remarks>
    /// <param name="message">The error message.</param>
    /// <param name="serviceName">The name of the service that timed out.</param>
    /// <param name="operationName">The name of the operation that timed out.</param>
    /// <param name="timeout">The timeout value in milliseconds.</param>
    /// <param name="innerException">The inner exception.</param>
    public class ServiceTimeoutException(
        string message,
        string serviceName,
        string operationName,
        int timeout,
        Exception? innerException = null) : ApplicationException(
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

    /// <summary>
    /// Exception thrown when a service dependency is unavailable.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="DependencyUnavailableException"/> class.
    /// </remarks>
    /// <param name="message">The error message.</param>
    /// <param name="serviceName">The name of the service that depends on the unavailable dependency.</param>
    /// <param name="dependencyName">The name of the unavailable dependency.</param>
    /// <param name="innerException">The inner exception.</param>
    public class DependencyUnavailableException(
        string message,
        string serviceName,
        string dependencyName,
        Exception? innerException = null) : ApplicationException(
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

    /// <summary>
    /// Exception thrown when a service configuration is invalid.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="ServiceConfigurationException"/> class.
    /// </remarks>
    /// <param name="message">The error message.</param>
    /// <param name="serviceName">The name of the service with invalid configuration.</param>
    /// <param name="configParameter">The name of the invalid configuration parameter, if applicable.</param>
    /// <param name="innerException">The inner exception.</param>
    public class ServiceConfigurationException(
        string message,
        string serviceName,
        string? configParameter = null,
        Exception? innerException = null) : ApplicationException(
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

    /// <summary>
    /// Exception thrown when a service has concurrent access conflicts.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="ConcurrencyException"/> class.
    /// </remarks>
    /// <param name="message">The error message.</param>
    /// <param name="resourceName">The name of the resource with concurrency issues.</param>
    /// <param name="innerException">The inner exception.</param>
    public class ConcurrencyException(
        string message,
        string resourceName,
        Exception? innerException = null) : ApplicationException(
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