# HydroGarden API Reference

This document provides a comprehensive reference for the APIs provided by the HydroGarden system, including core interfaces, events, and services.

## Core Interfaces

### IHydroGardenComponent

The base interface for all components in the system.

```csharp
public interface IHydroGardenComponent : IDisposable
{
    Guid Id { get; }
    string Name { get; }
    string AssemblyType { get; }
    ComponentState State { get; }

    Task SetPropertyAsync(string name, object value, IPropertyMetadata metadata);
    Task<T?> GetPropertyAsync<T>(string name);
    IPropertyMetadata? GetPropertyMetadata(string name);
    IDictionary<string, object> GetProperties();
    IDictionary<string, IPropertyMetadata> GetAllPropertyMetadata();
    Task LoadPropertiesAsync(IDictionary<string, object> properties, IDictionary<string, IPropertyMetadata>? metadata = null);
    void SetEventHandler(IHydroGardenPropertyChangedEventHandler handler);
}
```

### IIoTDevice

Represents a physical or virtual IoT device in the system.

```csharp
public interface IIoTDevice : IHydroGardenComponent
{
    Task InitializeAsync(CancellationToken ct = default);
    Task StartAsync(CancellationToken ct = default);
    Task StopAsync(CancellationToken ct = default);
}
```

### IEventBus

The central messaging system interface.

```csharp
public interface IEventBus
{
    Guid Subscribe(IHydroGardenPropertyChangedEventHandler handler, IEventSubscriptionOptions? options = null);
    bool Unsubscribe(Guid subscriptionId);
    Task<IPublishResult> PublishAsync(object sender, IHydroGardenEvent evt, CancellationToken ct = default);
}
```

### IEventSubscriptionOptions

Options for configuring event subscriptions.

```csharp
public interface IEventSubscriptionOptions
{
    EventType[] EventTypes { get; set; }
    Guid[] SourceIds { get; set; }
    Func<IHydroGardenEvent, bool>? Filter { get; set; }
    bool IncludeConnectedSources { get; set; }
    bool Synchronous { get; set; }
}
```

### IEventRoutingData

Metadata for controlling how events are routed.

```csharp
public interface IEventRoutingData
{
    Guid[] TargetIds { get; }
    bool Persist { get; }
    EventPriority Priority { get; }
    bool RequiresAcknowledgment { get; }
    TimeSpan? Timeout { get; }
}
```

### IHydroGardenPropertyChangedEventHandler

Interface for handling property change events.

```csharp
public interface IHydroGardenPropertyChangedEventHandler : IAsyncDisposable
{
    Task HandleEventAsync(object sender, IHydroGardenPropertyChangedEvent e, CancellationToken ct = default);
}
```

### IPersistenceService

Interface for the persistence service.

```csharp
public interface IPersistenceService
{
    Task AddOrUpdateAsync<T>(T component, CancellationToken ct = default) where T : IIoTDevice;
    Task ProcessPendingEventsAsync();
    Task<T?> GetPropertyAsync<T>(Guid deviceId, string propertyName, CancellationToken ct = default);
}
```

### ITopologyService

Interface for the topology service.

```csharp
public interface ITopologyService
{
    Task<IReadOnlyList<IComponentConnection>> GetConnectionsForSourceAsync(Guid sourceId, CancellationToken ct = default);
    Task<IReadOnlyList<IComponentConnection>> GetConnectionsForTargetAsync(Guid targetId, CancellationToken ct = default);
    Task<IComponentConnection> CreateConnectionAsync(IComponentConnection connection, CancellationToken ct = default);
    Task<bool> UpdateConnectionAsync(IComponentConnection connection, CancellationToken ct = default);
    Task<bool> DeleteConnectionAsync(Guid connectionId, CancellationToken ct = default);
    Task<bool> EvaluateConnectionConditionAsync(IComponentConnection connection, CancellationToken ct = default);
}
```

### IComponentConnection

Interface representing a connection between components.

```csharp
public interface IComponentConnection
{
    Guid ConnectionId { get; }
    Guid SourceId { get; }
    Guid TargetId { get; }
    string ConnectionType { get; }
    bool IsEnabled { get; }
    string? Condition { get; }
    IDictionary<string, object>? Metadata { get; }
}
```

### IStore

Interface for storage implementations.

```csharp
public interface IStore
{
    Task<IStoreTransaction> BeginTransactionAsync(CancellationToken ct = default);
    Task<IDictionary<string, object>?> LoadAsync(Guid id, CancellationToken ct = default);
    Task<IDictionary<string, IPropertyMetadata>?> LoadMetadataAsync(Guid id, CancellationToken ct = default);
    Task SaveAsync(Guid id, IDictionary<string, object> properties, CancellationToken ct = default);
    Task SaveWithMetadataAsync(Guid id, IDictionary<string, object> properties, IDictionary<string, IPropertyMetadata>? metadata, CancellationToken ct = default);
}
```

## Event Interfaces

### IHydroGardenEvent

Base interface for all events.

```csharp
public interface IHydroGardenEvent
{
    Guid DeviceId { get; }
    Guid EventId { get; }
    DateTimeOffset Timestamp { get; }
    Guid SourceId { get; }
    EventType EventType { get; }
    IEventRoutingData? RoutingData { get; }
}
```

### IHydroGardenPropertyChangedEvent

Interface for property change events.

```csharp
public interface IHydroGardenPropertyChangedEvent : IHydroGardenEvent
{
    string PropertyName { get; }
    Type PropertyType { get; }
    object? OldValue { get; }
    object? NewValue { get; }
    IPropertyMetadata Metadata { get; }
}
```

### IHydroGardenLifecycleEvent

Interface for lifecycle events.

```csharp
public interface IHydroGardenLifecycleEvent : IHydroGardenEvent
{
    ComponentState State { get; }
    string? Details { get; }
}
```

### IHydroGardenCommandEvent

Interface for command events.

```csharp
public interface IHydroGardenCommandEvent : IHydroGardenEvent
{
    string CommandName { get; }
    IDictionary<string, object?>? Parameters { get; }
}
```

### IHydroGardenTelemetryEvent

Interface for telemetry events.

```csharp
public interface IHydroGardenTelemetryEvent : IHydroGardenEvent
{
    IDictionary<string, object> Readings { get; }
    IDictionary<string, string>? Units { get; }
}
```

### IHydroGardenAlertEvent

Interface for alert events.

```csharp
public interface IHydroGardenAlertEvent : IHydroGardenEvent
{
    AlertSeverity Severity { get; }
    string Message { get; }
    IDictionary<string, object>? AlertData { get; }
    bool IsAcknowledged { get; set; }
}
```

## Enumerations

### ComponentState

Represents the possible states of a component.

```csharp
public enum ComponentState
{
    Created,
    Initializing,
    Ready,
    Running,
    Stopping,
    Error,
    Disposed
}
```

### EventType

Classification of event types in the system.

```csharp
public enum EventType
{
    PropertyChanged,
    Lifecycle,
    Command,
    Telemetry,
    Alert,
    System,
    Timer,
    Custom
}
```

### EventPriority

Priority levels for event processing.

```csharp
public enum EventPriority
{
    Low = 0,
    Normal = 50,
    High = 100,
    Critical = 200
}
```

### AlertSeverity

Severity levels for alerts.

```csharp
public enum AlertSeverity
{
    Info = 0,
    Warning = 50,
    Error = 100,
    Critical = 200
}
```

## REST API (Planned)

The REST API will provide HTTP endpoints for interacting with the HydroGarden system. This section outlines the planned API endpoints.

### Authentication

```
POST /api/auth/login
POST /api/auth/logout
POST /api/auth/refresh
```

### Components

```
GET /api/components
GET /api/components/{id}
POST /api/components
PUT /api/components/{id}
DELETE /api/components/{id}
```

### Device Operations

```
POST /api/devices/{id}/initialize
POST /api/devices/{id}/start
POST /api/devices/{id}/stop
PUT /api/devices/{id}/property/{propertyName}
GET /api/devices/{id}/property/{propertyName}
```

### Topology

```
GET /api/topology
GET /api/topology/connections
POST /api/topology/connections
PUT /api/topology/connections/{id}
DELETE /api/topology/connections/{id}
```

### Events

```
POST /api/events
GET /api/events/history
GET /api/events/history/{id}
```

### System Configuration

```
GET /api/config
PUT /api/config/{section}
```

## SignalR API (Planned)

The SignalR API will provide real-time communication with clients. This section outlines the planned SignalR hubs and methods.

### EventHub

```csharp
// Server methods
Task SubscribeToEventsAsync(EventSubscriptionDto subscription);
Task UnsubscribeFromEventsAsync(Guid subscriptionId);
Task PublishCommandAsync(CommandEventDto command);

// Client methods
Task OnEventReceived(EventDto evt);
Task OnConnectionStateChanged(ConnectionStateDto state);
Task OnSystemStatusUpdated(SystemStatusDto status);
```

### Implementation Notes

- REST API endpoints will return standard HTTP status codes.
- SignalR connections will use JWT authentication.
- Event data will be serialized as JSON.
- Resource URLs are based on a RESTful design pattern.
- Pagination will be supported for collection endpoints.
- Filtering options will be available for most GET endpoints.