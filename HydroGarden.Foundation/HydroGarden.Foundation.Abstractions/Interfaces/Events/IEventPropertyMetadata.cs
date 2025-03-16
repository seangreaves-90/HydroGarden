namespace HydroGarden.Foundation.Abstractions.Interfaces.Events
{
    /// <summary>
    /// Interface for storing additional metadata associated with an event
    /// </summary>
    public interface IEventPropertyMetadata
    {
        /// <summary>
        /// Gets a value from the metadata by key
        /// </summary>
        /// <typeparam name="T">The type of the value to return</typeparam>
        /// <param name="key">The key to look up</param>
        /// <returns>The value, or default(T) if not found</returns>
        T? GetValue<T>(string key);

        /// <summary>
        /// Sets a value in the metadata
        /// </summary>
        /// <param name="key">The key to set</param>
        /// <param name="value">The value to store</param>
        void SetValue(string key, object? value);

        /// <summary>
        /// Gets whether the metadata contains a value for the specified key
        /// </summary>
        /// <param name="key">The key to check</param>
        /// <returns>True if the metadata contains the key, false otherwise</returns>
        bool ContainsKey(string key);

        /// <summary>
        /// Gets all keys in the metadata
        /// </summary>
        IEnumerable<string> Keys { get; }
    }
}