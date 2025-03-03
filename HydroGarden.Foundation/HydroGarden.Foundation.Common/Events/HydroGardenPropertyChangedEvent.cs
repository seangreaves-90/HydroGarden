using HydroGarden.Foundation.Abstractions.Interfaces;
using HydroGarden.Foundation.Abstractions.Interfaces.Events;

namespace HydroGarden.Foundation.Common.Events
{
    /// <summary>
    /// Event fired when a property value changes on a component
    /// </summary>
    public record HydroGardenPropertyChangedEvent : IHydroGardenPropertyChangedEvent
    {
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
        public Guid EventId { get; } = Guid.NewGuid();

        /// <inheritdoc />
        public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;

        /// <inheritdoc />
        public EventType EventType => EventType.PropertyChanged;

        /// <inheritdoc />
        public IEventRoutingData? RoutingData { get; }


        /// <inheritdoc />
        IEventRoutingData IHydroGardenEvent.RoutingData => RoutingData!;


        /// <inheritdoc />
        public Guid SourceId { get; }

        /// <summary>
        /// Creates a new property changed event
        /// </summary>
        /// <param name="deviceId">The component that fired the event</param>
        /// <param name="sourceId"></param>
        /// <param name="propertyName">The name of the property that changed</param>
        /// <param name="propertyType">The type of the property</param>
        /// <param name="oldValue">The previous value</param>
        /// <param name="newValue">The new value</param>
        /// <param name="metadata">Metadata about the property</param>
        /// <param name="routingData">Optional routing information</param>
        public HydroGardenPropertyChangedEvent(
            Guid deviceId,
            Guid sourceId,
            string propertyName,
            Type propertyType,
            object? oldValue,
            object? newValue,
            IPropertyMetadata metadata,
            IEventRoutingData? routingData = null)
        {
            DeviceId = deviceId;
            SourceId = sourceId;
            PropertyName = propertyName;
            PropertyType = propertyType;
            OldValue = oldValue;
            NewValue = newValue;
            Metadata = metadata;
            RoutingData = routingData;
        }
    }
}
