using System.Runtime.CompilerServices;
using HydroGarden.Foundation.Abstractions.Interfaces.Components;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;

namespace HydroGarden.Foundation.ErrorHandling
{
    /// <summary>
    /// Builder class for creating rich error context with important diagnostic information.
    /// </summary>
    public class ErrorContextBuilder
    {
        private readonly Dictionary<string, object?> _context = [];

        /// <summary>
        /// Creates a new error context builder.
        /// </summary>
        public static ErrorContextBuilder Create() => new();

        /// <summary>
        /// Adds device information to the context.
        /// </summary>
        public ErrorContextBuilder WithDevice(Guid deviceId, string? deviceName = null)
        {
            _context["DeviceId"] = deviceId;
            
            if (!string.IsNullOrEmpty(deviceName))
            {
                _context["DeviceName"] = deviceName;
            }
            
            return this;
        }

        /// <summary>
        /// Adds source information about the component that experienced the error.
        /// </summary>
        public ErrorContextBuilder WithSource(object source)
        {
            _context["SourceType"] = source.GetType().FullName ?? "UnknownType";

            if (source is IComponent component)
            {
                _context["ComponentId"] = component.Id;
                _context["ComponentName"] = component.Name;
                _context["ComponentState"] = component.State.ToString();
            }

            return this;
        }

        /// <summary>
        /// Adds metadata about where the error occurred in the code.
        /// </summary>
        public ErrorContextBuilder WithLocation(
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            var fileName = filePath.Split('\\', '/').LastOrDefault() ?? filePath;
            _context["CallSite"] = $"{fileName}:{memberName}({lineNumber})";
            return this;
        }

        /// <summary>
        /// Adds operation details such as method name and parameters.
        /// </summary>
        public ErrorContextBuilder WithOperation(string? operationName, object? parameters = null)
        {
            _context["Operation"] = operationName;

            if (parameters != null)
            {
                // Handle parameters without storing sensitive or large data
                try
                {
                    // If parameters is a dictionary or collection, extract a summary
                    if (parameters is IDictionary<string, object> dict)
                    {
                        _context["OperationParameterCount"] = dict.Count;
                        _context["OperationParameterKeys"] = string.Join(",", dict.Keys);
                    }
                    else if (parameters is System.Collections.ICollection collection)
                    {
                        _context["OperationParameterCount"] = collection.Count;
                    }
                    else
                    {
                        // For simple objects, just store the type
                        _context["OperationParameterType"] = parameters.GetType().Name;
                    }
                }
                catch
                {
                    // If anything goes wrong during parameter processing, just store the type
                    _context["OperationParameterType"] = parameters.GetType().Name;
                }
            }

            return this;
        }

        /// <summary>
        /// Adds error classification information to the context.
        /// </summary>
        public ErrorContextBuilder WithErrorClassification(
            string? errorCode,
            ErrorSeverity severity,
            ErrorSource source,
            ErrorCategory category)
        {
            _context["ErrorCode"] = errorCode;
            _context["ErrorSeverity"] = severity.ToString();
            _context["ErrorSource"] = source.ToString();
            _context["ErrorCategory"] = category.ToString();
            return this;
        }

        /// <summary>
        /// Adds custom key-value pairs to the context.
        /// </summary>
        public ErrorContextBuilder WithProperty(string key, object? value)
        {
            _context[key] = value;
            return this;
        }

        /// <summary>
        /// Adds multiple properties from a dictionary.
        /// </summary>
        public ErrorContextBuilder WithProperties(IDictionary<string, object?>? properties)
        {
            foreach (var (key, value) in properties)
            {
                _context[key] = value;
            }

            return this;
        }

        /// <summary>
        /// Adds exception details to the context.
        /// </summary>
        public ErrorContextBuilder WithException(Exception exception)
        {
            _context["ExceptionType"] = exception.GetType().Name;
            _context["ExceptionMessage"] = exception.Message;

            // Add exception details recursively for inner exceptions
            var innerException = exception.InnerException;
            int depth = 1;
            
            while (innerException != null && depth <= 3) // Limit depth to avoid excessive nesting
            {
                _context[$"InnerExceptionType{depth}"] = innerException.GetType().Name;
                _context[$"InnerExceptionMessage{depth}"] = innerException.Message;
                
                innerException = innerException.InnerException;
                depth++;
            }

            // Always add a stack trace hash, empty string if no stack trace is available
            _context["StackTraceHash"] = !string.IsNullOrEmpty(exception.StackTrace) 
                ? exception.StackTrace.GetHashCode().ToString()
                : string.Empty.GetHashCode().ToString();

            // Add HResult for system exceptions
            _context["HResult"] = exception.HResult;

            return this;
        }

        /// <summary>
        /// Adds correlation information for tracking related events.
        /// </summary>
        public ErrorContextBuilder WithCorrelation(Guid correlationId)
        {
            _context["CorrelationId"] = correlationId;
            return this;
        }

        /// <summary>
        /// Builds the final context dictionary.
        /// </summary>
        public Dictionary<string, object?>? Build()
        {
            // Add timestamp information
            _context["ContextCreatedAt"] = DateTimeOffset.UtcNow.ToString("o");
            
            return new Dictionary<string, object?>(_context);
        }
    }
}