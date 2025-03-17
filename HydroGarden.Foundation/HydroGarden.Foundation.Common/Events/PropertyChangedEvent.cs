using HydroGarden.Foundation.Abstractions.Interfaces;
using HydroGarden.Foundation.Abstractions.Interfaces.Events;

namespace HydroGarden.Foundation.Common.Events
{
    /// <summary>
    /// Represents an event triggered when a property value changes.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the PropertyChangedEvent class.
    /// </remarks>
    /// <param name="deviceId">The ID of the device whose property changed.</param>
    /// <param name="propertyName">The name of the property that changed.</param>
    /// <param name="oldValue">The previous value of the property.</param>
    /// <param name="newValue">The new value of the property.</param>
    /// <param name="metadata">Metadata associated with the property.</param>
    public class PropertyChangedEvent(
        Guid deviceId,
        string propertyName,
        object? oldValue,
        object? newValue,
        IPropertyMetadata metadata) : IPropertyChangedEvent
    {
        private readonly Dictionary<string, object> _eventMetadata = [];

        /// <inheritdoc />
        public Guid EventId { get; } = Guid.NewGuid();

        /// <inheritdoc />
        public Guid SourceId { get; } = deviceId;

        /// <inheritdoc />
        public EventType EventType { get; } = EventType.PropertyChanged;

        /// <inheritdoc />
        public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;

        /// <inheritdoc />
        public Guid DeviceId { get; } = deviceId;

        /// <inheritdoc />
        public string PropertyName { get; } = propertyName ?? throw new ArgumentNullException(nameof(propertyName));

        /// <inheritdoc />
        public Type PropertyType { get; } = newValue?.GetType() ?? typeof(object);

        /// <inheritdoc />
        public object? OldValue { get; } = oldValue;

        /// <inheritdoc />
        public object? NewValue { get; } = newValue;

        /// <inheritdoc />
        public IPropertyMetadata Metadata { get; } = metadata ?? throw new ArgumentNullException(nameof(metadata));

        /// <inheritdoc />
        public IEventRoutingData? RoutingData { get; } = null;

        /// <inheritdoc />
        IDictionary<string, object> IEvent.Metadata => _eventMetadata;
    }
}