using HydroGarden.Foundation.Abstractions.Interfaces;
using HydroGarden.Foundation.Abstractions.Interfaces.Components;
using HydroGarden.Foundation.Abstractions.Interfaces.Events;

namespace HydroGarden.Foundation.Tests.ErrorHandling.Mocks
{
    /// <summary>
    /// A mock implementation of IComponent for testing purposes.
    /// </summary>
    public class MockComponent : IComponent
    {
        private readonly Dictionary<string, object> _properties = new();
        private readonly Dictionary<string, IPropertyMetadata> _metadata = new();
        private IPropertyChangedEventHandler? _eventHandler;

        public Guid Id { get; } = Guid.NewGuid();
        public string Name { get; }
        public string AssemblyType { get; } = typeof(MockComponent).FullName ?? "MockComponent";
        public ComponentState State { get; set; } = ComponentState.Ready;
        
        public MockComponent()
        {
            Name = "MockComponent";
        }
        
        public MockComponent(string name)
        {
            Name = name;
        }

        public void Dispose()
        {
            State = ComponentState.Disposed;
            GC.SuppressFinalize(this);
        }

        public Task<T?> GetPropertyAsync<T>(string name)
        {
            if (_properties.TryGetValue(name, out var value) && value is T typedValue)
            {
                return Task.FromResult<T?>(typedValue);
            }
            return Task.FromResult<T?>(default);
        }

        public IPropertyMetadata? GetPropertyMetadata(string name)
        {
            return _metadata.TryGetValue(name, out var metadata) ? metadata : null;
        }

        public IPropertyMetadata ConstructDefaultPropertyMetadata(string name, bool isEditable, bool isVisible)
        {
            return new MockPropertyMetadata
            {
                IsEditable = isEditable,
                IsVisible = isVisible,
                DisplayName = name,
                Description = $"Description for {name}"
            };
        }

        public IDictionary<string, object> GetProperties()
        {
            return new Dictionary<string, object>(_properties);
        }

        public IDictionary<string, IPropertyMetadata> GetAllPropertyMetadata()
        {
            return new Dictionary<string, IPropertyMetadata>(_metadata);
        }

        public Task LoadPropertiesAsync(IDictionary<string, object> properties, IDictionary<string, IPropertyMetadata>? metadata = null)
        {
            foreach (var prop in properties)
            {
                _properties[prop.Key] = prop.Value;
            }

            if (metadata != null)
            {
                foreach (var meta in metadata)
                {
                    _metadata[meta.Key] = meta.Value;
                }
            }

            return Task.CompletedTask;
        }

        public Task SetPropertyAsync(string name, object value, IPropertyMetadata metadata)
        {
            var oldValue = _properties.TryGetValue(name, out var existing) ? existing : null;
            _properties[name] = value;
            _metadata[name] = metadata;

            // Simplified event handling since we don't need to test property events here
            // _eventHandler?.HandlePropertyChangedEventAsync(this, name, oldValue, value);
            return Task.CompletedTask;
        }

        public void SetEventHandler(IPropertyChangedEventHandler handler)
        {
            _eventHandler = handler;
        }
    }

    /// <summary>
    /// A mock implementation of IPropertyMetadata for testing purposes.
    /// </summary>
    public class MockPropertyMetadata : IPropertyMetadata
    {
        public bool IsEditable { get; set; }
        public bool IsVisible { get; set; }
        public string? DisplayName { get; set; }
        public string? Description { get; set; }
    }
}
