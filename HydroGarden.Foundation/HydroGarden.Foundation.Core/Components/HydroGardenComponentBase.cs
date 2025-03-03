using HydroGarden.Foundation.Abstractions.Interfaces;
using HydroGarden.Foundation.Common.Logging;
using HydroGarden.Foundation.Common.PropertyMetadata;
using HydroGarden.Foundation.Common.Events;
using System.Collections.Concurrent;
using System.Reflection;
using HydroGarden.Foundation.Abstractions.Interfaces.Components;
using HydroGarden.Foundation.Abstractions.Interfaces.Logging;
using HydroGarden.Foundation.Abstractions.Interfaces.Events;

namespace HydroGarden.Foundation.Core.Components
{
    /// <summary>
    /// Base class for all HydroGarden components, implementing common functionality such as property management and event handling.
    /// </summary>
    public abstract class HydroGardenComponentBase : IHydroGardenComponent
    {
        private readonly ConcurrentDictionary<string, object> _properties = new();
        private readonly ConcurrentDictionary<string, PropertyMetadata> _propertyMetadata = new();
        protected readonly IHydroGardenLogger _logger;
        protected IHydroGardenPropertyChangedEventHandler? _eventHandler;
        private volatile ComponentState _state = ComponentState.Created;
        private const int MaxOptimisticRetries = 3;

        /// <summary>
        /// Initializes a new instance of the <see cref="HydroGardenComponentBase"/> class.
        /// </summary>
        /// <param name="id">The unique identifier of the component.</param>
        /// <param name="name">The name of the component.</param>
        /// <param name="logger">Optional logger instance.</param>
        protected HydroGardenComponentBase(Guid id, string name, IHydroGardenLogger? logger = null)
        {
            Id = id;
            Name = name;
            AssemblyType = GetType().FullName ?? "UnknownType";
            _logger = logger ?? new HydroGardenLogger();
        }

        /// <inheritdoc/>
        public Guid Id { get; }

        /// <inheritdoc/>
        public string Name { get; }

        /// <inheritdoc/>
        public string AssemblyType { get; }

        /// <inheritdoc/>
        public ComponentState State
        {
            get => _state;
            private set
            {
                _state = value;
                _properties[nameof(State)] = value;
            }
        }

        /// <inheritdoc/>
        public void SetEventHandler(IHydroGardenPropertyChangedEventHandler handler) => _eventHandler = handler;

        /// <inheritdoc/>
        public virtual async Task SetPropertyAsync(string name, object value, IPropertyMetadata? metadata = null)
        {
            var oldValue = _properties.TryGetValue(name, out var existing) ? existing : default;
            UpdateClassProperty(name, value);
            _properties[name] = value;

            if (metadata != null)
            {
                _propertyMetadata[name] = new PropertyMetadata(metadata.IsEditable, metadata.IsVisible, metadata.DisplayName, metadata.Description);
            }
            else if (!_propertyMetadata.ContainsKey(name))
            {
                _propertyMetadata[name] = new PropertyMetadata(true, true, name, $"Property {name}");
            }

            if (!Equals(oldValue, value))
            {
                await PublishPropertyChangeAsync(name, value, _propertyMetadata[name], oldValue);
            }
        }

        /// <inheritdoc/>
        public virtual async Task<bool> UpdatePropertyOptimisticAsync<T>(string name, Func<T?, T> updateFunc)
        {
            int attempts = 0;
            while (attempts < MaxOptimisticRetries)
            {
                attempts++;
                _properties.TryGetValue(name, out var currentValueObj);
                var currentValue = currentValueObj is T typedValue ? typedValue : default;
                var newValue = updateFunc(currentValue);

                if (currentValueObj == null)
                {
                    if (_properties.TryAdd(name, newValue!))
                    {
                        await PublishPropertyChangeAsync(name, newValue, _propertyMetadata.GetValueOrDefault(name, new PropertyMetadata(true, true, name, $"Property {name}")));
                        return true;
                    }
                }
                else
                {
                    if (_properties.TryUpdate(name, newValue!, currentValueObj))
                    {
                        await PublishPropertyChangeAsync(name, newValue, _propertyMetadata.GetValueOrDefault(name, new PropertyMetadata(true, true, name, $"Property {name}")), currentValue);
                        return true;
                    }
                }

                await Task.Delay(TimeSpan.FromMilliseconds(10 * attempts));
            }

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
        public virtual Task<T?> GetPropertyAsync<T>(string name) =>
            _properties.TryGetValue(name, out var value) && value is T typedValue
                ? Task.FromResult<T?>(typedValue)
                : Task.FromResult<T?>(default);

        /// <inheritdoc/>
        public virtual IPropertyMetadata? GetPropertyMetadata(string name) =>
            _propertyMetadata.TryGetValue(name, out var metadata) ? metadata : null;

        /// <inheritdoc/>
        public virtual IDictionary<string, object> GetProperties() => _properties.ToDictionary(x => x.Key, x => x.Value);

        /// <inheritdoc/>
        public virtual IDictionary<string, IPropertyMetadata> GetAllPropertyMetadata() => _propertyMetadata.ToDictionary(x => x.Key, x => (IPropertyMetadata)x.Value);

        /// <inheritdoc/>
        public virtual Task LoadPropertiesAsync(IDictionary<string, object> properties, IDictionary<string, IPropertyMetadata>? metadata = null)
        {
            _properties.Clear();
            foreach (var (key, value) in properties) _properties[key] = value;

            if (metadata != null)
            {
                _propertyMetadata.Clear();
                foreach (var (key, value) in metadata)
                {
                    _propertyMetadata[key] = new PropertyMetadata(
                        value.IsEditable,
                        value.IsVisible,
                        value.DisplayName,
                        value.Description
                    );
                }
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Publishes a property change event to registered event handlers
        /// </summary>
        /// <param name="name">The name of the property that changed</param>
        /// <param name="value">The new property value</param>
        /// <param name="metadata">Metadata about the property</param>
        /// <param name="oldValue">The previous property value (optional)</param>
        /// <returns>A task representing the asynchronous operation</returns>
        protected async Task PublishPropertyChangeAsync(string name, object? value, IPropertyMetadata metadata, object? oldValue = null)
        {
            if (_eventHandler == null)
            {
                _logger.Log($"No event handler registered for component {Id}");
                return;
            }

            // Create the event with the deviceId as both deviceId and sourceId (for backward compatibility)
            var evt = new HydroGardenPropertyChangedEvent(
                Id,                             // deviceId
                Id,                             // sourceId (using the same ID)
                name,                           // propertyName
                value?.GetType() ?? typeof(object), // propertyType
                oldValue,                       // oldValue
                value,                          // newValue
                metadata                        // metadata
                                                // routingData is left as null
            );

            await _eventHandler.HandleEventAsync(this, evt);
        }

        /// <inheritdoc/>
        public virtual void Dispose() => _state = ComponentState.Disposed;

        private bool MetadataEquals(IPropertyMetadata existing, IPropertyMetadata newMetadata) =>
            existing.IsEditable == newMetadata.IsEditable &&
            existing.IsVisible == newMetadata.IsVisible &&
            existing.DisplayName == newMetadata.DisplayName &&
            existing.Description == newMetadata.Description;
    }
}
