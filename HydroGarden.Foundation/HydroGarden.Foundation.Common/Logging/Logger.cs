using HydroGarden.Foundation.Abstractions.Interfaces.Logging;
using System.Text;


namespace HydroGarden.Foundation.Common.Logging
{
    /// <summary>
    /// Provides logging functionality for HydroGarden components.
    /// </summary>
    public class Logger : ILogger
    {
        private readonly string _logDirectory;
        private readonly object _syncLock = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="Logger"/> class.
        /// </summary>
        /// <param name="logDirectory">The directory where logs should be stored.</param>
        public Logger(string logDirectory = "")
        {
            _logDirectory = string.IsNullOrWhiteSpace(logDirectory)
                ? Environment.CurrentDirectory
                : Path.GetFullPath(logDirectory);

            Directory.CreateDirectory(_logDirectory); // Ensure log directory exists
        }

        /// <inheritdoc/>
        public void Log(string message)
        {
            WriteLogRecord($"[{DateTimeOffset.UtcNow:O}] [INFO] {message}");
        }

        /// <inheritdoc/>
        public void Log(Exception ex, string message)
        {
            var logMessage = new StringBuilder();
            logMessage.AppendLine($"[{DateTimeOffset.UtcNow:O}] [ERROR] {message}");
            logMessage.AppendLine($"Exception: {ex.GetType().Name}");
            logMessage.AppendLine($"Message: {ex.Message}");
            logMessage.AppendLine("StackTrace:");
            logMessage.AppendLine(ex.StackTrace);
            WriteLogRecord(logMessage.ToString());
        }

        /// <inheritdoc/>
        public void Log(object obj, string message)
        {
            WriteLogRecord($"[{DateTimeOffset.UtcNow:O}] [DEBUG] {message}\nData: {obj}");
        }

        /// <summary>
        /// Writes a log entry to the log file with thread safety and retry logic.
        /// </summary>
        /// <param name="logEntry">The log message to write.</param>
        private void WriteLogRecord(string logEntry)
        {
            var fileName = $"log_{DateTime.UtcNow:yyyy-MM-dd}.txt";
            var filePath = Path.Combine(_logDirectory, fileName);
            int retryCount = 3;

            for (int attempt = 1; attempt <= retryCount; attempt++)
            {
                try
                {
                    lock (_syncLock)
                    {
                        using var fileStream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                        using var streamWriter = new StreamWriter(fileStream, Encoding.UTF8);
                        streamWriter.WriteLine(logEntry);
                        streamWriter.Flush();
                    }
                    return; // Exit loop if writing succeeds
                }
                catch (IOException ex) when (attempt < retryCount)
                {
                    Console.WriteLine($"[WARNING] Logging attempt {attempt} failed: {ex.Message}");
                    Thread.Sleep(50 * attempt); // Increase wait time between retries
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Failed to write log: {ex.Message}");
                    break; 
                }
            }
        }
    }
}
