﻿using HydroGarden.Foundation.Abstractions.Interfaces;
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
    public abstract class HydroGardenEventBase : IEvent
    {
        /// <inheritdoc />
        public Guid EventId { get; }

        /// <inheritdoc />
        public Guid SourceId { get; }

        /// <inheritdoc />
        public DateTimeOffset Timestamp { get; }

        /// <inheritdoc />
        public Guid DeviceId { get; }

        /// <inheritdoc />
        public abstract EventType EventType { get; }

        // Private backing field for RoutingData
        private readonly IEventRoutingData? _routingData;

        /// <inheritdoc />
        // Explicit implementation to ensure we never return null from the interface
        IEventRoutingData IEvent.RoutingData => _routingData ?? new EventRoutingData();

        /// <summary>
        /// Gets the routing data for this event (may be null)
        /// </summary>
        public IEventRoutingData? RoutingData => _routingData;

        /// <summary>
        /// Creates a new event base with default routing
        /// </summary>
        /// <param name="deviceId">The device ID</param>
        /// <param name="routingData">Optional routing data</param>
        protected HydroGardenEventBase(Guid deviceId, IEventRoutingData? routingData = null)
            : this(deviceId, deviceId, routingData) // Default sourceId to deviceId
        {
        }

        /// <summary>
        /// Creates a new event base with separate device and source IDs
        /// </summary>
        /// <param name="deviceId">The device ID</param>
        /// <param name="sourceId">The source ID</param>
        /// <param name="routingData">Optional routing data</param>
        protected HydroGardenEventBase(Guid deviceId, Guid sourceId, IEventRoutingData? routingData = null)
        {
            EventId = Guid.NewGuid();
            Timestamp = DateTimeOffset.UtcNow;
            DeviceId = deviceId;
            SourceId = sourceId;
            _routingData = routingData;
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
        public IPropertyMetadata Metadata { get; }

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
    public class HydroGardenLifecycleChangedEvent : ILifecycleEventHandler
    {
        private readonly List<ComponentState> _stateChanges;
        private readonly TaskCompletionSource<bool> _completionSource;

        public HydroGardenLifecycleChangedEvent()
        {
            _stateChanges = new List<ComponentState>();
            _completionSource = new TaskCompletionSource<bool>();
        }

        public HydroGardenLifecycleChangedEvent(List<ComponentState> stateChanges, TaskCompletionSource<bool> completionSource)
        {
            _stateChanges = stateChanges ?? throw new ArgumentNullException(nameof(stateChanges));
            _completionSource = completionSource ?? throw new ArgumentNullException(nameof(completionSource));
        }

        public async Task HandleEventAsync<T>(object sender, T evt, CancellationToken ct = default) where T : IEvent
        {
            // Handle property changed events that represent state changes
            if (evt is IPropertyChangedEvent propEvt &&
                propEvt.PropertyName == "State" &&
                propEvt.NewValue is ComponentState state)
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
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// Event for device commands
    /// </summary>
    public class CommandEvent : HydroGardenEventBase, ICommandEvent
    {
        /// <inheritdoc />
        public string CommandName { get; }

        /// <inheritdoc />
        public IDictionary<string, object?>? Parameters { get; }

        /// <inheritdoc />
        public override EventType EventType => EventType.Command;

        /// <summary>
        /// Creates a new command event
        /// </summary>
        /// <param name="deviceId">The source device ID</param>
        /// <param name="commandName">The name of the command to execute</param>
        /// <param name="parameters">Optional command parameters</param>
        /// <param name="routingData">Optional routing data</param>
        public CommandEvent(
            Guid deviceId,
            string commandName,
            IDictionary<string, object?>? parameters = null,
            IEventRoutingData? routingData = null)
            : base(deviceId, routingData)
        {
            CommandName = commandName ?? throw new ArgumentNullException(nameof(commandName));
            Parameters = parameters;
        }
    }

    /// <summary>
    /// Event for device telemetry/sensor readings
    /// </summary>
    public class TelemetryEvent : HydroGardenEventBase, ITelemetryEvent
    {
        /// <inheritdoc />
        public IDictionary<string, object> Readings { get; }

        /// <inheritdoc />
        public IDictionary<string, string>? Units { get; }

        /// <inheritdoc />
        public override EventType EventType => EventType.Telemetry;

        /// <summary>
        /// Creates a new telemetry event
        /// </summary>
        /// <param name="deviceId">The source device ID</param>
        /// <param name="readings">The telemetry readings</param>
        /// <param name="units">Optional units of measurement</param>
        /// <param name="routingData">Optional routing data</param>
        public TelemetryEvent(
            Guid deviceId,
            IDictionary<string, object> readings,
            IDictionary<string, string>? units = null,
            IEventRoutingData? routingData = null)
            : base(deviceId, routingData)
        {
            Readings = readings ?? throw new ArgumentNullException(nameof(readings));
            Units = units;
        }
    }

    /// <summary>
    /// Event for alerts/notifications
    /// </summary>
    public class AlertEvent : HydroGardenEventBase, IAlertEvent
    {
        /// <inheritdoc />
        public AlertSeverity Severity { get; }

        /// <inheritdoc />
        public string Message { get; }

        /// <inheritdoc />
        public IDictionary<string, object>? AlertData { get; }

        /// <inheritdoc />
        public bool IsAcknowledged { get; set; }

        /// <inheritdoc />
        public override EventType EventType => EventType.Alert;

        /// <summary>
        /// Creates a new alert event
        /// </summary>
        /// <param name="deviceId">The source device ID</param>
        /// <param name="severity">The severity of the alert</param>
        /// <param name="message">The alert message</param>
        /// <param name="alertData">Optional additional data</param>
        /// <param name="routingData">Optional routing data</param>
        public AlertEvent(
            Guid deviceId,
            AlertSeverity severity,
            string message,
            IDictionary<string, object>? alertData = null,
            IEventRoutingData? routingData = null)
            : base(deviceId, routingData)
        {
            Severity = severity;
            Message = message ?? throw new ArgumentNullException(nameof(message));
            AlertData = alertData;
            IsAcknowledged = false;
        }
    }
}