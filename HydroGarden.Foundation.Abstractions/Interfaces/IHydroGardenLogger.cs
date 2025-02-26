namespace HydroGarden.Foundation.Abstractions.Interfaces
{
    /// <summary>
    /// Provides logging functionality for HydroGarden components.
    /// </summary>
    public interface IHydroGardenLogger
    {
        /// <summary>
        /// Logs a message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        void Log(string message);

        /// <summary>
        /// Logs an exception with an accompanying message.
        /// </summary>
        /// <param name="ex">The exception to log.</param>
        /// <param name="message">The message describing the exception.</param>
        void Log(Exception ex, string message);

        /// <summary>
        /// Logs an object with a description message.
        /// </summary>
        /// <param name="obj">The object to log.</param>
        /// <param name="message">The message describing the object.</param>
        void Log(object obj, string message);
    }
}
