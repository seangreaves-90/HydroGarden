using System.Runtime.CompilerServices;
using HydroGarden.Foundation.Abstractions.Interfaces.Components;

namespace HydroGarden.Foundation.Common.ErrorHandling
{
    /// <summary>
    /// Builder class for creating rich error context with important diagnostic information.
    /// </summary>
    public class ErrorContextBuilder
    {
        private readonly Dictionary<string, object> _context = new();

        /// <summary>
        /// Creates a new error context builder.
        /// </summary>
        public static ErrorContextBuilder Create() => new();

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
        public ErrorContextBuilder WithOperation(string operationName, object? parameters = null)
        {
            _context["Operation"] = operationName;

            if (parameters != null)
            {
                _context["OperationParameters"] = parameters;
            }

            return this;
        }

        /// <summary>
        /// Adds custom key-value pairs to the context.
        /// </summary>
        public ErrorContextBuilder WithProperty(string key, object value)
        {
            _context[key] = value;
            return this;
        }

        /// <summary>
        /// Adds multiple properties from a dictionary.
        /// </summary>
        public ErrorContextBuilder WithProperties(IDictionary<string, object> properties)
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

            if (exception.InnerException != null)
            {
                _context["InnerExceptionType"] = exception.InnerException.GetType().Name;
                _context["InnerExceptionMessage"] = exception.InnerException.Message;
            }

            // Only create a hash of the stack trace to avoid storing full traces
            if (!string.IsNullOrEmpty(exception.StackTrace))
            {
                _context["StackTraceHash"] = exception.StackTrace.GetHashCode().ToString();
            }

            return this;
        }

        /// <summary>
        /// Builds the final context dictionary.
        /// </summary>
        public Dictionary<string, object> Build()
        {
            _context["ContextCreatedAt"] = DateTimeOffset.UtcNow.ToString("o");
            return new Dictionary<string, object>(_context);
        }
    }
}