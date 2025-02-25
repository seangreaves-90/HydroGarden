// HydroGarden.Foundation.Core.Components/HydroGardenComponentBase.cs
using HydroGarden.Foundation.Abstractions.Interfaces;
using HydroGarden.Foundation.Common.Locking;
using HydroGarden.Foundation.Common.Logging;
using HydroGarden.Foundation.Common.PropertyMetadata;
using HydroGarden.Foundation.Core.EventHandlers;

namespace HydroGarden.Foundation.Core.Components
{
    public abstract class HydroGardenComponentBase : IHydroGardenComponent
    {
        private readonly Dictionary<string, object> _properties = new();
        private readonly Dictionary<string, PropertyMetadata> _propertyMetadata = new();
        private readonly IHydroGardenLogger _logger;
        private readonly AsyncReaderWriterLock _propertiesLock = new();
        protected IHydroGardenEventHandler? _eventHandler;
        private volatile ComponentState _state = ComponentState.Created;

        protected HydroGardenComponentBase(Guid id, string name, IHydroGardenLogger? logger = null)
        {
            Id = id;
            Name = name;
            AssemblyType = GetType();
            _logger = logger ?? new HydroGardenLogger();
        }

        public Guid Id { get; }
        public string Name { get; }
        public Type AssemblyType { get; }
        public ComponentState State => _state;

        public void SetEventHandler(IHydroGardenEventHandler handler)
        {
            _eventHandler = handler;
        }

        public virtual async Task SetPropertyAsync(string name, object value, bool isEditable = true, bool isVisible = true, string? displayName = null, string? description = null)
        {
            using var writeLock = await _propertiesLock.WriterLockAsync();

            var oldValue = _properties.TryGetValue(name, out var existing) ? existing : default;
            _properties[name] = value;

            var metadata = new PropertyMetadata
            {
                IsEditable = isEditable,
                IsVisible = isVisible,
                DisplayName = displayName,
                Description = description
            };
            _propertyMetadata[name] = metadata;

            await PublishPropertyChangeAsync(name, value, metadata, oldValue);
        }

        public virtual async Task<T?> GetPropertyAsync<T>(string name)
        {
            using var readLock = await _propertiesLock.ReaderLockAsync();
            return _properties.TryGetValue(name, out var value) && value is T typedValue
                ? typedValue
                : default;
        }

        public virtual IPropertyMetadata? GetPropertyMetadata(string name)
        {
            using var readLock = _propertiesLock.ReaderLockAsync().GetAwaiter().GetResult();
            return _propertyMetadata.TryGetValue(name, out var metadata) ? metadata : null;
        }

        public virtual IDictionary<string, object> GetProperties()
        {
            using var readLock = _propertiesLock.ReaderLockAsync().GetAwaiter().GetResult();
            return new Dictionary<string, object>(_properties);
        }

        public virtual IDictionary<string, IPropertyMetadata> GetAllPropertyMetadata()
        {
            using var readLock = _propertiesLock.ReaderLockAsync().GetAwaiter().GetResult();
            return _propertyMetadata.ToDictionary(x => x.Key, x => (IPropertyMetadata)x.Value);
        }

        public virtual async Task LoadPropertiesAsync(IDictionary<string, object> properties,
            IDictionary<string, IPropertyMetadata>? metadata = null)
        {
            using var writeLock = await _propertiesLock.WriterLockAsync();
            _properties.Clear();
            foreach (var (key, value) in properties)
            {
                _properties[key] = value;
            }

            if (metadata != null)
            {
                _propertyMetadata.Clear();
                foreach (var (key, value) in metadata)
                {
                    // Convert IPropertyMetadata to PropertyMetadata
                    _propertyMetadata[key] = new PropertyMetadata
                    {
                        IsEditable = value.IsEditable,
                        IsVisible = value.IsVisible,
                        DisplayName = value.DisplayName,
                        Description = value.Description
                    };
                }
            }
        }

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

        public virtual void Dispose()
        {
            _state = ComponentState.Disposed;
            _propertiesLock.Dispose();
        }
    }
}