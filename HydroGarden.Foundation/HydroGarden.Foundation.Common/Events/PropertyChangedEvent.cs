using HydroGarden.Foundation.Abstractions.Interfaces;
using HydroGarden.Foundation.Abstractions.Interfaces.Events;

namespace HydroGarden.Foundation.Common.Events
{
    /// <summary>
    /// Represents an event triggered when a property value changes.
    /// </summary>
    public class PropertyChangedEvent : IPropertyChangedEvent
    {
        private readonly Dictionary<string, object> _eventMetadata;

        /// <inheritdoc />
        public Guid EventId { get; }

        /// <inheritdoc />
        public Guid SourceId { get; }

        /// <inheritdoc />
        public EventType EventType { get; }

        /// <inheritdoc />
        public DateTimeOffset Timestamp { get; }

        /// <inheritdoc />
        public Guid DeviceId { get; }

        /// <inheritdoc />
        public string PropertyName { get; }

        /// <inheritdoc />
        public Type PropertyType { get; }

        /// <inheritdoc />
        public object? OldValue { get; }

        /// <inheritdoc />
        public object? NewValue { get; }
        
        /// <inheritdoc />
        public IPropertyMetadata Metadata { get; }
        
        /// <inheritdoc />
        public IEventRoutingData? RoutingData { get; }

        /// <inheritdoc />
        IDictionary<string, object> IEvent.Metadata => _eventMetadata;

        /// <summary>
        /// Initializes a new instance of the PropertyChangedEvent class.
        /// </summary>
        /// <param name="deviceId">The ID of the device whose property changed.</param>
        /// <param name="propertyName">The name of the property that changed.</param>
        /// <param name="oldValue">The previous value of the property.</param>
        /// <param name="newValue">The new value of the property.</param>
        /// <param name="metadata">Metadata associated with the property.</param>
        public PropertyChangedEvent(
            Guid deviceId,
            string propertyName,
            object? oldValue,
            object? newValue,
            IPropertyMetadata metadata)
        {
            EventId = Guid.NewGuid();
            SourceId = deviceId;
            EventType = EventType.PropertyChanged;
            Timestamp = DateTimeOffset.UtcNow;
            DeviceId = deviceId;
            PropertyName = propertyName ?? throw new ArgumentNullException(nameof(propertyName));
            PropertyType = newValue?.GetType() ?? typeof(object);
            OldValue = oldValue;
            NewValue = newValue;
            Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
            _eventMetadata = new Dictionary<string, object>();
            RoutingData = null;
        }
    }
}