using HydroGarden.Foundation.Abstractions.Interfaces;
using HydroGarden.Foundation.Common.Logging;
using HydroGarden.Foundation.Common.PropertyMetadata;
using HydroGarden.Foundation.Core.EventHandlers;
using System.Collections.Concurrent;
using System.Reflection;

namespace HydroGarden.Foundation.Core.Components
{
    /// <summary>
    /// Base implementation of IHydroGardenComponent providing common functionality.
    /// </summary>
    public abstract class HydroGardenComponentBase : IHydroGardenComponent
    {
        private readonly ConcurrentDictionary<string, object> _properties = new();
        private readonly ConcurrentDictionary<string, PropertyMetadata> _propertyMetadata = new();
        private readonly IHydroGardenLogger _logger;
        protected IHydroGardenEventHandler? _eventHandler;
        private volatile ComponentState _state = ComponentState.Created;
        private const int MaxOptimisticRetries = 3;

        /// <summary>
        /// Initializes a new instance of the HydroGardenComponentBase class.
        /// </summary>
        /// <param name="id">The unique identifier for this component.</param>
        /// <param name="name">The display name for this component.</param>
        /// <param name="logger">Logger for recording information and errors (optional).</param>
        protected HydroGardenComponentBase(Guid id, string name, IHydroGardenLogger? logger = null)
        {
            Id = id;
            Name = name;
            AssemblyType = GetType();
            _logger = logger ?? new HydroGardenLogger();
        }

        /// <inheritdoc/>
        public Guid Id { get; }

        /// <inheritdoc/>
        public string Name { get; }

        /// <inheritdoc/>
        public Type AssemblyType { get; }

        /// <inheritdoc/>
        public ComponentState State
        {
            get => _state;
            private set => _state = value;
        }

        /// <inheritdoc/>
        public void SetEventHandler(IHydroGardenEventHandler handler)
        {
            _eventHandler = handler;
        }

        /// <inheritdoc/>
        public virtual async Task SetPropertyAsync(string name, object value, bool isEditable = true, bool isVisible = true, string? displayName = null, string? description = null)
        {
            // Get the old value (if any) for event notification
            var oldValue = _properties.TryGetValue(name, out var existing) ? existing : default;

            // Update the property value
            _properties[name] = value;

            // Update the class property if it exists
            UpdateClassProperty(name, value);

            // Create and set metadata
            var metadata = new PropertyMetadata
            {
                IsEditable = isEditable,
                IsVisible = isVisible,
                DisplayName = displayName,
                Description = description
            };

            _propertyMetadata[name] = metadata;

            // Notify subscribers of the property change
            await PublishPropertyChangeAsync(name, value, metadata, oldValue);
        }

        /// <summary>
        /// Updates a component property with optimistic concurrency.
        /// </summary>
        /// <typeparam name="T">The type of the property value.</typeparam>
        /// <param name="name">The name of the property.</param>
        /// <param name="updateFunc">A function that takes the current value and returns the new value.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public virtual async Task<bool> UpdatePropertyOptimisticAsync<T>(string name, Func<T?, T> updateFunc)
        {
            int attempts = 0;

            while (attempts < MaxOptimisticRetries)
            {
                attempts++;

                // Get current value
                _properties.TryGetValue(name, out var currentValueObj);
                var currentValue = currentValueObj is T typedValue ? typedValue : default;

                // Calculate new value
                var newValue = updateFunc(currentValue);

                // Try to update with optimistic concurrency
                if (currentValueObj == null)
                {
                    if (_properties.TryAdd(name, newValue!))
                    {
                        // Update was successful
                        await PublishPropertyChangeAsync(name, newValue,
                            _propertyMetadata.TryGetValue(name, out var meta) ? meta : new PropertyMetadata());
                        return true;
                    }
                }
                else
                {
                    if (_properties.TryUpdate(name, newValue!, currentValueObj))
                    {
                        // Update was successful
                        await PublishPropertyChangeAsync(name, newValue,
                            _propertyMetadata.TryGetValue(name, out var meta) ? meta : new PropertyMetadata(), currentValue);
                        return true;
                    }
                }

                // If we get here, the update failed due to concurrent modification
                // Wait a bit before retrying to reduce contention
                await Task.Delay(TimeSpan.FromMilliseconds(10 * attempts));
            }

            // If we've exhausted all retry attempts, log the failure and return false
            _logger.Log($"Failed to update property {name} after {MaxOptimisticRetries} attempts due to concurrent modifications.");
            return false;
        }

        private void UpdateClassProperty(string propertyName, object value)
        {
            var type = GetType();
            while (type != null)
            {
                var property = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (property != null)
                {
                    var setter = property.GetSetMethod(true);
                    if (setter != null)
                    {
                        setter.Invoke(this, new[] { value });
                        return;
                    }
                }
                type = type.BaseType;
            }
        }

        /// <inheritdoc/>
        public virtual Task<T?> GetPropertyAsync<T>(string name)
        {
            if (_properties.TryGetValue(name, out var value) && value is T typedValue)
            {
                return Task.FromResult<T?>(typedValue);
            }
            return Task.FromResult<T?>(default);
        }

        /// <inheritdoc/>
        public virtual IPropertyMetadata? GetPropertyMetadata(string name)
        {
            return _propertyMetadata.TryGetValue(name, out var metadata) ? metadata : null;
        }

        /// <inheritdoc/>
        public virtual IDictionary<string, object> GetProperties()
        {
            return _properties.ToDictionary(x => x.Key, x => x.Value);
        }

        /// <inheritdoc/>
        public virtual IDictionary<string, IPropertyMetadata> GetAllPropertyMetadata()
        {
            return _propertyMetadata.ToDictionary(x => x.Key, x => (IPropertyMetadata)x.Value);
        }

        /// <inheritdoc/>
        public virtual Task LoadPropertiesAsync(IDictionary<string, object> properties,
            IDictionary<string, IPropertyMetadata>? metadata = null)
        {
            // Clear and reload properties
            _properties.Clear();
            foreach (var (key, value) in properties)
            {
                _properties[key] = value;
            }

            // Clear and reload metadata if provided
            if (metadata != null)
            {
                _propertyMetadata.Clear();
                foreach (var (key, value) in metadata)
                {
                    _propertyMetadata[key] = new PropertyMetadata
                    {
                        IsEditable = value.IsEditable,
                        IsVisible = value.IsVisible,
                        DisplayName = value.DisplayName,
                        Description = value.Description
                    };
                }
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Publishes a property change event to subscribers.
        /// </summary>
        /// <param name="name">The property name.</param>
        /// <param name="value">The new property value.</param>
        /// <param name="metadata">The property metadata.</param>
        /// <param name="oldValue">The old property value (optional).</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        protected async Task PublishPropertyChangeAsync(string name, object? value, IPropertyMetadata metadata, object? oldValue = null)
        {
            if (_eventHandler == null)
            {
                _logger.Log($"No event handler registered for component {Id}");
                return;
            }

            var evt = new HydroGardenPropertyChangedEvent(
                Id, name, value?.GetType() ?? typeof(object), oldValue, value, metadata);
            await _eventHandler.HandleEventAsync(this, evt);
        }

        /// <inheritdoc/>
        public virtual void Dispose()
        {
            _state = ComponentState.Disposed;
        }
    }
}