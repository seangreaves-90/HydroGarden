using HydroGarden.Foundation.Abstractions.Interfaces.Logging;
using System;
using System.IO;

namespace HydroGarden.Foundation.Common.Logging
{
    /// <summary>
    /// Provides logging functionality for HydroGarden components.
    /// </summary>
    public class HydroGardenLogger : IHydroGardenLogger
    {
        private readonly string _logDirectory;
        private readonly object _syncLock = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="HydroGardenLogger"/> class.
        /// </summary>
        /// <param name="logDirectory">The directory where logs should be stored.</param>
        public HydroGardenLogger(string logDirectory = "")
        {
            if (string.IsNullOrWhiteSpace(logDirectory))
                logDirectory = Environment.CurrentDirectory;

            _logDirectory = Path.GetFullPath(logDirectory);
            Directory.CreateDirectory(_logDirectory);
        }

        /// <inheritdoc/>
        public void Log(string message)
        {
            WriteLogRecord($"[{DateTimeOffset.UtcNow:O}] [INFO] {message}");
        }

        /// <inheritdoc/>
        public void Log(Exception ex, string message)
        {
            WriteLogRecord(
                $"[{DateTimeOffset.UtcNow:O}] [ERROR] {message}\n" +
                $"Exception: {ex.GetType().Name}\n" +
                $"Message: {ex.Message}\n" +
                $"StackTrace:\n{ex.StackTrace}\n"
            );
        }

        /// <inheritdoc/>
        public void Log(object obj, string message)
        {
            WriteLogRecord(
                $"[{DateTimeOffset.UtcNow:O}] [DEBUG] {message}\n" +
                $"Data:\n{obj}\n"
            );
        }

        private void WriteLogRecord(string logEntry)
        {
            var fileName = $"log_{DateTime.UtcNow:yyyy-MM-dd}.txt";
            var filePath = Path.Combine(_logDirectory, fileName);

            lock (_syncLock)
            {
                File.AppendAllText(filePath, logEntry + Environment.NewLine + Environment.NewLine);
            }
        }
    }
}
