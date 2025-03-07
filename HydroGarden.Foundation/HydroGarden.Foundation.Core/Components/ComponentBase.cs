using HydroGarden.Foundation.Common.PropertyMetadata;
using HydroGarden.Foundation.Common.Events;
using System.Collections.Concurrent;
using System.Reflection;
using HydroGarden.Foundation.Abstractions.Interfaces;
using HydroGarden.Foundation.Abstractions.Interfaces.Components;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Foundation.Common.Extensions;
using HydroGarden.Logger.Abstractions;

namespace HydroGarden.Foundation.Core.Components
{
    /// <summary>
    /// Base class for all HydroGarden components, implementing common functionality such as property management and event handling.
    /// </summary>
    public abstract class ComponentBase : IComponent
    {
        private readonly ConcurrentDictionary<string, object> _properties = new();
        private readonly ConcurrentDictionary<string, IPropertyMetadata> _propertyMetadata = new();
        protected readonly ILogger Logger;
        protected readonly IErrorMonitor ErrorMonitor;
        protected IPropertyChangedEventHandler? PropertyChangedEventHandler;
        private volatile ComponentState _state = ComponentState.Created;
        private const int MaxOptimisticRetries = 3;

        /// <summary>
        /// Initializes a new instance of the <see cref="ComponentBase"/> class.
        /// </summary>
        /// <param name="id">The unique identifier of the component.</param>
        /// <param name="name">The name of the component.</param>
        /// <param name="errorMonitor">The errorMonitor component.</param>
        /// <param name="logger">Optional logger instance.</param>
        protected ComponentBase(Guid id, string name, IErrorMonitor errorMonitor, ILogger? logger = null)
        {
            Id = id;
            Name = name;
            AssemblyType = GetType().FullName ?? "UnknownType";
            Logger = logger ?? new Logger.Logging.Logger();
            ErrorMonitor = errorMonitor;
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
        public void SetEventHandler(IPropertyChangedEventHandler handler) => PropertyChangedEventHandler = handler;

        /// <inheritdoc/>
        public virtual async Task SetPropertyAsync(string name, object value, IPropertyMetadata? metadata = null)
        {
            await this.ExecuteWithErrorHandlingAsync(
                ErrorMonitor,
                async () =>
                {
                    var oldValue = _properties.TryGetValue(name, out var existing) ? existing : default;
                    UpdateClassProperty(name, value);
                    _properties[name] = value;

                    if (metadata == null)
                    {
                        metadata = ConstructDefaultPropertyMetadata(name);
                    }

                    _propertyMetadata[name] = new PropertyMetadata(
                        metadata.IsEditable,
                        metadata.IsVisible,
                        metadata.DisplayName,
                        metadata.Description);

                    if (!Equals(oldValue, value))
                    {
                        await PublishPropertyChangeAsync(name, value, _propertyMetadata[name], oldValue);
                    }

                    return true;
                },
                "PROPERTY_UPDATE_FAILED",
                $"Failed to update property '{name}'",
                ErrorSource.Device,
                new Dictionary<string, object>
                {
                    ["PropertyName"] = name,
                    ["PropertyType"] = value?.GetType().Name ?? "null"
                });
        }

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

            Logger.Log($"Failed to update property {name} after {MaxOptimisticRetries} attempts due to concurrent modifications.");
            return false;
        }

        /// <summary>
        /// Constructs the default property metadata for general HydroGarden components.
        /// </summary>
        /// <param name="name">The property name.</param>
        /// <param name="isEditable">Indicates whether the property is editable.</param>
        /// <param name="isVisible">Indicates whether the property is visible.</param>
        /// <returns>The default <see cref="IPropertyMetadata"/> for the property.</returns>
        public virtual IPropertyMetadata ConstructDefaultPropertyMetadata(string name, bool isEditable = true, bool isVisible = true)
        {
            // Use a dictionary approach for well-known properties with predefined metadata
            var knownPropertyDefaults = new Dictionary<string, (bool IsEditable, bool IsVisible, string DisplayName, string Description)>
            {
                // Core component properties
                { "State", (false, true, "Component State", "The current state of the component") },
                { "Id", (false, true, "Component ID", "The unique identifier of the component") },
                { "Name", (true, true, "Component Name", "The name of the component") },
                { "AssemblyType", (false, true, "Component Type", "The assembly type of the component") }
            };

            // If the property is in our known list, use those values
            if (knownPropertyDefaults.TryGetValue(name, out var defaults))
            {
                return new PropertyMetadata(
                    defaults.IsEditable,
                    defaults.IsVisible,
                    defaults.DisplayName,
                    defaults.Description);
            }

            // Otherwise, use the provided default values
            return new PropertyMetadata(isEditable, isVisible, name, $"Property {name}");
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
            if (_properties.TryGetValue(name, out var value))
            {
                Logger.Log($"[DEBUG] GetPropertyAsync: Found '{name}' = {value} (Type: {value?.GetType()})");

                // Direct cast if possible
                if (value is T typedValue)
                {
                    return Task.FromResult<T?>(typedValue);
                }

                // Attempt conversion if direct cast fails
                try
                {
                    var convertedValue = (T)Convert.ChangeType(value, typeof(T))!;
                    Logger.Log($"[INFO] Converted '{name}' from {value?.GetType()} to {typeof(T)}: {convertedValue}");
                    return Task.FromResult<T?>(convertedValue);
                }
                catch (Exception ex)
                {
                    Logger.Log($"[WARNING] Failed to convert '{name}' value '{value}' from {value?.GetType()} to {typeof(T)}: {ex.Message}");
                }
            }
            else
            {
                Logger.Log($"[WARNING] Property '{name}' not found in _properties.");
            }

            return Task.FromResult<T?>(default);
        }



        /// <inheritdoc/>
        public virtual IPropertyMetadata? GetPropertyMetadata(string name) =>
            _propertyMetadata.GetValueOrDefault(name);

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
            if (PropertyChangedEventHandler == null)
            {
                Logger.Log($"No event handler registered for component {Id}");
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

            await PropertyChangedEventHandler.HandleEventAsync(this, evt);
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
