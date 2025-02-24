using HydroGarden.Foundation.Abstractions.Interfaces;
using System;
using System.IO;

namespace HydroGarden.Foundation.Common.Logging
{
#if DEBUG
    /// <summary>
    /// Debug-only plain text logger that logs messages, exceptions, and objects with structured formatting.
    /// </summary>
    public class HydroGardenLogger : IHydroGardenLogger
    {
        private readonly string _logDirectory;
        private readonly object _syncLock = new();

        public HydroGardenLogger() : this(string.Empty) { }

        public HydroGardenLogger(string logDirectory)
        {
            if (string.IsNullOrWhiteSpace(logDirectory))
                logDirectory = Environment.CurrentDirectory;

            _logDirectory = Path.GetFullPath(logDirectory);
            Directory.CreateDirectory(_logDirectory);
        }

        /// <summary>
        /// Logs a simple message.
        /// </summary>
        public void Log(string message)
        {
            WriteLogRecord($"[{DateTimeOffset.UtcNow:O}] [INFO] {message}");
        }

        /// <summary>
        /// Logs an exception with a message.
        /// </summary>
        public void Log(Exception ex, string message)
        {
            WriteLogRecord(
                $"[{DateTimeOffset.UtcNow:O}] [ERROR] {message}\n" +
                $"Exception: {ex.GetType().Name}\n" +
                $"Message: {ex.Message}\n" +
                $"StackTrace:\n{ex.StackTrace}\n"
            );
        }

        /// <summary>
        /// Logs an object with a message.
        /// </summary>
        public void Log(object obj, string message)
        {
            WriteLogRecord(
                $"[{DateTimeOffset.UtcNow:O}] [DEBUG] {message}\n" +
                $"Data:\n{obj}\n"
            );
        }

        /// <summary>
        /// Writes log entries to a text file with a rolling daily format.
        /// </summary>
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
#else
    /// <summary>
    /// No-op logger for Release mode (ensures no logs are written).
    /// </summary>
    public class HydroGardenLogger : IHydroGardenLogger
    {
        public void Log(string message) { }
        public void Log(Exception ex, string message) { }
        public void Log(object obj, string message) { }
    }
#endif
}
