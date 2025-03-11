using HydroGarden.Logger.Abstractions;

namespace HydroGarden.Foundation.Tests.ErrorHandling.Mocks
{
    /// <summary>
    /// Mock logger implementation for testing error handling components.
    /// </summary>
    public class MockLogger : ILogger
    {
        public List<LogEntry> Logs { get; } = new();

        public void Log(string message)
        {
            Logs.Add(new LogEntry(message, null));
        }

        public void Log(Exception? exception, string message)
        {
            Logs.Add(new LogEntry(message, exception));
        }
        
        public void Log(object obj, string message)
        {
            Logs.Add(new LogEntry($"{message}: {obj}", null));
        }
    }

    /// <summary>
    /// Represents a log entry for testing purposes.
    /// </summary>
    public class LogEntry
    {
        public string Message { get; }
        public Exception? Exception { get; }
        public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;

        public LogEntry(string message, Exception? exception)
        {
            Message = message;
            Exception = exception;
        }
    }
}
