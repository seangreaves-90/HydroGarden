using HydroGarden.Foundation.Abstractions.Interfaces.Events;

namespace HydroGarden.Foundation.Common.Events
{
    /// <summary>
    /// Default implementation of IEventPropertyMetadata using a dictionary
    /// </summary>
    public class EventPropertyMetadata : IEventPropertyMetadata
    {
        private readonly Dictionary<string, object?> _metadata = new();

        /// <summary>
        /// Creates a new, empty metadata collection
        /// </summary>
        public EventPropertyMetadata()
        {
        }

        /// <summary>
        /// Creates a new metadata collection initialized with the provided values
        /// </summary>
        /// <param name="initialValues">Initial metadata values</param>
        public EventPropertyMetadata(IDictionary<string, object?> initialValues)
        {
            if (initialValues != null)
            {
                foreach (var kvp in initialValues)
                {
                    _metadata[kvp.Key] = kvp.Value;
                }
            }
        }

        /// <inheritdoc/>
        public T? GetValue<T>(string key)
        {
            if (_metadata.TryGetValue(key, out var value))
            {
                if (value is T typedValue)
                {
                    return typedValue;
                }
                
                // Try to convert the value
                try
                {
                    return (T?)Convert.ChangeType(value, typeof(T));
                }
                catch
                {
                    // If conversion fails, return default
                    return default;
                }
            }
            
            return default;
        }

        /// <inheritdoc/>
        public void SetValue(string key, object? value)
        {
            _metadata[key] = value;
        }

        /// <inheritdoc/>
        public bool ContainsKey(string key)
        {
            return _metadata.ContainsKey(key);
        }

        /// <inheritdoc/>
        public IEnumerable<string> Keys => _metadata.Keys;
    }
}