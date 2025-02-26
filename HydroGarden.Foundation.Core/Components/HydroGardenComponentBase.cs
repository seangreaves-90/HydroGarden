using HydroGarden.Foundation.Abstractions.Interfaces;
using HydroGarden.Foundation.Common.Logging;
using HydroGarden.Foundation.Common.PropertyMetadata;
using HydroGarden.Foundation.Core.EventHandlers;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading.Tasks;

namespace HydroGarden.Foundation.Core.Components
{
    public abstract class HydroGardenComponentBase : IHydroGardenComponent
    {
        private readonly ConcurrentDictionary<string, object> _properties = new();
        private readonly ConcurrentDictionary<string, PropertyMetadata> _propertyMetadata = new();
        protected readonly IHydroGardenLogger _logger;
        protected IHydroGardenEventHandler? _eventHandler;
        private volatile ComponentState _state = ComponentState.Created;
        private const int MaxOptimisticRetries = 3;

        protected HydroGardenComponentBase(Guid id, string name, IHydroGardenLogger? logger = null)
        {
            Id = id;
            Name = name;
            AssemblyType = GetType().FullName ?? "UnknownType";
            _logger = logger ?? new HydroGardenLogger();
        }

        public Guid Id { get; }
        public string Name { get; }
        public string AssemblyType { get; }

        public ComponentState State
        {
            get => _state;
            private set
            {
                _state = value;
                _properties[nameof(State)] = value;
            }
        }

        public void SetEventHandler(IHydroGardenEventHandler handler) => _eventHandler = handler;

        public virtual async Task SetPropertyAsync(string name, object value, IPropertyMetadata? metadata = null)
        {
            var oldValue = _properties.TryGetValue(name, out var existing) ? existing : default;

            UpdateClassProperty(name, value);

            _properties[name] = value;

            if (metadata != null)
            {
                if (!_propertyMetadata.TryGetValue(name, out var existingMetadata) || !MetadataEquals(existingMetadata, metadata))
                {
                    _propertyMetadata[name] = new PropertyMetadata(metadata.IsEditable, metadata.IsVisible, metadata.DisplayName, metadata.Description);
                }
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

        public virtual Task<T?> GetPropertyAsync<T>(string name) =>
            _properties.TryGetValue(name, out var value) && value is T typedValue ?
                Task.FromResult<T?>(typedValue) : Task.FromResult<T?>(default);

        public virtual IPropertyMetadata? GetPropertyMetadata(string name) =>
            _propertyMetadata.TryGetValue(name, out var metadata) ? metadata : null;

        public virtual IDictionary<string, object> GetProperties() => _properties.ToDictionary(x => x.Key, x => x.Value);
        public virtual IDictionary<string, IPropertyMetadata> GetAllPropertyMetadata() => _propertyMetadata.ToDictionary(x => x.Key, x => (IPropertyMetadata)x.Value);

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

        protected async Task PublishPropertyChangeAsync(string name, object? value, IPropertyMetadata metadata, object? oldValue = null)
        {
            if (_eventHandler == null)
            {
                _logger.Log($"No event handler registered for component {Id}");
                return;
            }

            var evt = new HydroGardenPropertyChangedEvent(Id, name, value?.GetType() ?? typeof(object), oldValue, value, metadata);
            await _eventHandler.HandleEventAsync(this, evt);
        }

        public virtual void Dispose() => _state = ComponentState.Disposed;

        private bool MetadataEquals(IPropertyMetadata existing, IPropertyMetadata newMetadata) =>
            existing.IsEditable == newMetadata.IsEditable &&
            existing.IsVisible == newMetadata.IsVisible &&
            existing.DisplayName == newMetadata.DisplayName &&
            existing.Description == newMetadata.Description;
    }
}
