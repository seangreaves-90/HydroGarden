using HydroGarden.Foundation.Abstractions.Interfaces;
using HydroGarden.Foundation.Abstractions.Interfaces.Components;
using HydroGarden.Foundation.Abstractions.Interfaces.Events;


namespace HydroGarden.Foundation.Common.Events
{
    /// <summary>
    /// Base class for all HydroGarden events
    /// </summary>
    /// <summary>
    /// Base class for all HydroGarden events
    /// </summary>
    /// <remarks>
    /// Creates a new event base with separate device and source IDs
    /// </remarks>
    /// <param name="deviceId">The device ID</param>
    /// <param name="sourceId">The source ID</param>
    /// <param name="routingData">Optional routing data</param>
    public abstract class HydroGardenEventBase(Guid deviceId, Guid sourceId, IEventRoutingData? routingData = null) : IEvent
    {
        /// <inheritdoc />
        public Guid EventId { get; } = Guid.NewGuid();

        /// <inheritdoc />
        public Guid SourceId { get; } = sourceId;

        /// <inheritdoc />
        public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;

        /// <inheritdoc />
        public Guid DeviceId { get; } = deviceId;

        /// <inheritdoc />
        public abstract EventType EventType { get; }

        // Private backing field for RoutingData
        private readonly IEventRoutingData? _routingData = routingData;

        /// <inheritdoc />
        // Explicit implementation to ensure we never return null from the interface
        IEventRoutingData IEvent.RoutingData => _routingData ?? new EventRoutingData();

        /// <summary>
        /// Gets the routing data for this event (maybe null)
        /// </summary>
        public IEventRoutingData? RoutingData => _routingData;

        /// <inheritdoc />
        public IDictionary<string, object>? Metadata { get; set; }

        /// <summary>
        /// Creates a new event base with default routing
        /// </summary>
        /// <param name="deviceId">The device ID</param>
        /// <param name="routingData">Optional routing data</param>
        protected HydroGardenEventBase(Guid deviceId, IEventRoutingData? routingData = null)
            : this(deviceId, deviceId, routingData) // Default sourceId to deviceId
        {
        }
    }

    /// <summary>
    /// Enhanced implementation of property changed event
    /// </summary>
    public class HydroGardenPropertyChangedEvent : HydroGardenEventBase, IPropertyChangedEvent
    {
        /// <inheritdoc />
        public string PropertyName { get; }

        /// <inheritdoc />
        public Type PropertyType { get; }

        /// <inheritdoc />
        public object? OldValue { get; }

        /// <inheritdoc />
        public object? NewValue { get; }

        /// <inheritdoc />
        public new IPropertyMetadata Metadata { get; }

        /// <inheritdoc />
        public override EventType EventType => EventType.PropertyChanged;

        /// <summary>
        /// Creates a new property changed event with the device ID as the source ID
        /// </summary>
        /// <param name="deviceId">The device ID</param>
        /// <param name="propertyName">Name of the changed property</param>
        /// <param name="propertyType">Type of the property</param>
        /// <param name="oldValue">Previous value of the property</param>
        /// <param name="newValue">New value of the property</param>
        /// <param name="metadata">Metadata for the property</param>
        /// <param name="routingData">Optional routing data</param>
        public HydroGardenPropertyChangedEvent(
            Guid deviceId,
            string propertyName,
            Type propertyType,
            object? oldValue,
            object? newValue,
            IPropertyMetadata metadata,
            IEventRoutingData? routingData = null)
            : base(deviceId, routingData)
        {
            PropertyName = propertyName ?? throw new ArgumentNullException(nameof(propertyName));
            PropertyType = propertyType ?? throw new ArgumentNullException(nameof(propertyType));
            OldValue = oldValue;
            NewValue = newValue;
            Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        }

        /// <summary>
        /// Creates a new property changed event with separate device and source IDs
        /// </summary>
        /// <param name="deviceId">The device ID</param>
        /// <param name="sourceId">The source ID</param>
        /// <param name="propertyName">Name of the changed property</param>
        /// <param name="propertyType">Type of the property</param>
        /// <param name="oldValue">Previous value of the property</param>
        /// <param name="newValue">New value of the property</param>
        /// <param name="metadata">Metadata for the property</param>
        /// <param name="routingData">Optional routing data</param>
        public HydroGardenPropertyChangedEvent(
            Guid deviceId,
            Guid sourceId,
            string propertyName,
            Type propertyType,
            object? oldValue,
            object? newValue,
            IPropertyMetadata metadata,
            IEventRoutingData? routingData = null)
            : base(deviceId, sourceId, routingData)
        {
            PropertyName = propertyName ?? throw new ArgumentNullException(nameof(propertyName));
            PropertyType = propertyType ?? throw new ArgumentNullException(nameof(propertyType));
            OldValue = oldValue;
            NewValue = newValue;
            Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        }
    }

    /// <summary>
    /// Event for device lifecycle changes
    /// </summary>
    public class HydroGardenLifecycleChangedEvent : ILifecycleEventHandler<IEvent>
    {
        private readonly List<ComponentState> _stateChanges;
        private readonly TaskCompletionSource<bool> _completionSource;

        public HydroGardenLifecycleChangedEvent()
        {
            _stateChanges = [];
            _completionSource = new TaskCompletionSource<bool>();
        }

        public HydroGardenLifecycleChangedEvent(List<ComponentState> stateChanges, TaskCompletionSource<bool> completionSource)
        {
            _stateChanges = stateChanges ?? throw new ArgumentNullException(nameof(stateChanges));
            _completionSource = completionSource ?? throw new ArgumentNullException(nameof(completionSource));
        }

        public Task HandleAsync(IEvent @event, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }

        public async Task HandleEventAsync<T>(object? sender, T evt, CancellationToken ct = default) where T : IEvent
        {
            // Handle property changed events that represent state changes
            if (evt is IPropertyChangedEvent { PropertyName: "State", NewValue: ComponentState state })
            {
                _stateChanges.Add(state);

                // Signal completion if we've received enough state changes
                if (_stateChanges.Count >= 5)
                {
                    _completionSource.TrySetResult(true);
                }
            }
            // Handle lifecycle events directly
            else if (evt is ILifecycleEvent lifecycleEvt)
            {
                _stateChanges.Add(lifecycleEvt.State);

                // Signal completion if we've received enough state changes
                if (_stateChanges.Count >= 5)
                {
                    _completionSource.TrySetResult(true);
                }
            }
            await Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            GC.SuppressFinalize(this);
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// Event for device commands
    /// </summary>
    /// <remarks>
    /// Creates a new command event
    /// </remarks>
    /// <param name="deviceId">The source device ID</param>
    /// <param name="commandName">The name of the command to execute</param>
    /// <param name="parameters">Optional command parameters</param>
    /// <param name="routingData">Optional routing data</param>
    public class CommandEvent(
        Guid deviceId,
        string commandName,
        IDictionary<string, object?>? parameters = null,
        IEventRoutingData? routingData = null) : HydroGardenEventBase(deviceId, routingData), ICommandEvent
    {
        /// <inheritdoc />
        public string CommandName { get; } = commandName ?? throw new ArgumentNullException(nameof(commandName));

        /// <inheritdoc />
        public IDictionary<string, object?>? Parameters { get; } = parameters;

        /// <inheritdoc />
        public override EventType EventType => EventType.Command;
    }

    /// <summary>
    /// Event for device telemetry/sensor readings
    /// </summary>
    /// <remarks>
    /// Creates a new telemetry event
    /// </remarks>
    /// <param name="deviceId">The source device ID</param>
    /// <param name="readings">The telemetry readings</param>
    /// <param name="units">Optional units of measurement</param>
    /// <param name="routingData">Optional routing data</param>
    public class TelemetryEvent(
        Guid deviceId,
        IDictionary<string, object> readings,
        IDictionary<string, string>? units = null,
        IEventRoutingData? routingData = null) : HydroGardenEventBase(deviceId, routingData), ITelemetryEvent
    {
        /// <inheritdoc />
        public IDictionary<string, object> Readings { get; } = readings ?? throw new ArgumentNullException(nameof(readings));

        /// <inheritdoc />
        public IDictionary<string, string>? Units { get; } = units;

        /// <inheritdoc />
        public override EventType EventType => EventType.Telemetry;
    }

    /// <summary>
    /// Event for alerts/notifications
    /// </summary>
    /// <remarks>
    /// Creates a new alert event
    /// </remarks>
    /// <param name="deviceId">The source device ID</param>
    /// <param name="severity">The severity of the alert</param>
    /// <param name="message">The alert message</param>
    /// <param name="alertData">Optional additional data</param>
    /// <param name="routingData">Optional routing data</param>
    public class AlertEvent(
        Guid deviceId,
        AlertSeverity severity,
        string message,
        IDictionary<string, object>? alertData = null,
        IEventRoutingData? routingData = null) : HydroGardenEventBase(deviceId, routingData), IAlertEvent
    {
        /// <inheritdoc />
        public AlertSeverity Severity { get; } = severity;

        /// <inheritdoc />
        public string Message { get; } = message ?? throw new ArgumentNullException(nameof(message));

        /// <inheritdoc />
        public IDictionary<string, object>? AlertData { get; } = alertData;

        /// <inheritdoc />
        public bool IsAcknowledged { get; set; } = false;

        /// <inheritdoc />
        public override EventType EventType => EventType.Alert;
    }
}