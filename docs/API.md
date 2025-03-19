# HydroGarden API Reference

This document provides a comprehensive reference for the HydroGarden API, including key interfaces, events, and service contracts.

## Core Interfaces

### Component Interfaces

#### IComponent

The foundation interface for all components in the system.

```csharp
public interface IComponent : IDisposable
{
    Guid Id { get; }
    string? Name { get; }
    string? AssemblyType { get; }
    ComponentState State { get; }
    
    Task SetPropertyAsync(string name, object? value, IPropertyMetadata metadata);
    Task<T?> GetPropertyAsync<T>(string name);
    IPropertyMetadata? GetPropertyMetadata(string name);
    IPropertyMetadata ConstructDefaultPropertyMetadata(string name, bool isEditable, bool isVisible);
    Dictionary<string, object?> GetProperties();
    IDictionary<string, IPropertyMetadata> GetAllPropertyMetadata();
    Task LoadPropertiesAsync(IDictionary<string, object?> properties, IDictionary<string, IPropertyMetadata>? metadata = null);
    void SetEventHandler(IPropertyChangedEventHandler<IEvent> handler);
}
```

#### IIoTDevice

Interface for IoT devices, extending the base component functionality.

```csharp
public interface IIoTDevice : IComponent
{
    Task InitializeAsync(CancellationToken ct = default);
    Task StartAsync(CancellationToken ct = default);
    Task StopAsync(CancellationToken ct = default);
    Task ReportErrorAsync(IApplicationError error, CancellationToken ct = default);
    Task<bool> TryRecoverAsync(CancellationToken ct = default);
}
```

### Event Interfaces

#### IEvent

Base interface for all events.

```csharp
public interface IEvent
{
    Guid DeviceId { get; }
    Guid EventId { get; }
    DateTimeOffset Timestamp { get; }
    Guid SourceId { get; }
    EventType EventType { get; }
    IEventRoutingData? RoutingData { get; }
    IDictionary<string, object>? Metadata { get; }
}
```

#### IEventBus

Central interface for event publication and subscription.

```csharp
public interface IEventBus 
{
    Guid Subscribe<TEvent>(IEventHandler<IEvent> handler, IEventSubscriptionOptions? options) where TEvent : IEvent;
    Guid Subscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : IEvent;
    bool Unsubscribe(Guid subscriptionId);
    Task<IPublishResult?> PublishAsync(object? sender, IEvent evt, CancellationToken ct = default);
}
```

#### IEventHandler

Interface for generic event handlers.

```csharp
public interface IEventHandler : IAsyncDisposable
{
    Task HandleEventAsync<T>(object? sender, T evt, CancellationToken ct = default) where T : IEvent;
}

public interface IEventHandler<in TEvent> : IEventHandler where TEvent : IEvent
{
    Task HandleAsync(TEvent @event, CancellationToken ct = default);
}
```

#### Specialized Event Interfaces

```csharp
public interface IPropertyChangedEvent : IEvent
{
    string PropertyName { get; }
    Type PropertyType { get; }
    object? OldValue { get; }
    object? NewValue { get; }
    new IPropertyMetadata Metadata { get; }
}

public interface IStateChangeEvent : IEvent
{
    ComponentState OldState { get; }
    ComponentState NewState { get; }
}

public interface ICommandEvent : IEvent
{
    string CommandName { get; }
    IDictionary<string, object?>? Parameters { get; }
}

public interface ITelemetryEvent : IEvent
{
    IDictionary<string, object> Readings { get; }
    IDictionary<string, string>? Units { get; }
}

public interface IAlertEvent : IEvent
{
    AlertSeverity Severity { get; }
    string Message { get; }
    IDictionary<string, object>? AlertData { get; }
    bool IsAcknowledged { get; set; }
}

public interface ISystemEvent : IEvent
{
    string EventSubType { get; }
    IDictionary<string, object> EventData { get; }
}

public interface ILifecycleEvent : IEvent
{
    ComponentState State { get; }
    string? Details { get; }
}
```

### Error Handling Interfaces

#### IApplicationError

Interface for error representation.

```csharp
public interface IApplicationError
{
    Guid DeviceId { get; }
    string? ErrorCode { get; }
    string Message { get; }
    ErrorSeverity Severity { get; }
    Dictionary<string, object?> Context { get; }
    DateTimeOffset Timestamp { get; }
    Exception? Exception { get; }
    Guid CorrelationId { get; }
    ErrorSource Source { get; }
    ErrorCategory Category { get; }
}
```

#### IErrorMonitor

Interface for error reporting and monitoring.

```csharp
public interface IErrorMonitor
{
    Task ReportErrorAsync(IApplicationError error, CancellationToken ct = default);
    Task ReportExceptionAsync(object source, Exception exception, string errorCode, 
        string message, ErrorSeverity severity = ErrorSeverity.Error, 
        ErrorSource errorSource = ErrorSource.Unknown, 
        IDictionary<string, object> context = null, 
        CancellationToken ct = default);
    Task<IReadOnlyCollection<IApplicationError>> GetRecentErrorsAsync(int limit = 10, CancellationToken ct = default);
    Task<bool> HasActiveErrorsAsync(ErrorSeverity minSeverity = ErrorSeverity.Warning, CancellationToken ct = default);
    Task<IReadOnlyCollection<IApplicationError>> GetActiveErrorsForDeviceAsync(Guid deviceId, CancellationToken ct = default);
    Task ClearErrorAsync(Guid deviceId, string errorCode, CancellationToken ct = default);
}
```

#### IErrorEventTransformationService

Interface for bidirectional conversion between errors and events.

```csharp
public interface IErrorEventTransformationService
{
    IErrorEvent TransformErrorToEvent(IApplicationError error);
    IEvent TransformToPublishableEvent(IErrorEvent errorEvent);
    IErrorEvent? ExtractErrorEvent(IEvent @event);
    Task PublishErrorAsEventAsync(IApplicationError error, CancellationToken cancellationToken = default);
}
```

### Service Interfaces

#### IPersistenceService

Interface for component data persistence.

```csharp
public interface IPersistenceService : IAsyncDisposable
{
    Task AddOrUpdateAsync<T>(T? component, CancellationToken ct = default) where T : IIoTDevice;
    Task ProcessPendingEventsAsync();
    Task<T?> GetPropertyAsync<T>(Guid deviceId, string propertyName, CancellationToken ct = default);
    Task<IPersistenceTransaction> BeginTransactionAsync(CancellationToken ct = default);
    Task StoreConnectionAsync(IComponentConnection connection, CancellationToken ct = default);
    Task<IEnumerable<IComponentConnection>> GetAllConnectionsAsync(CancellationToken ct = default);
    Task<IComponentConnection?> GetConnectionAsync(Guid connectionId, CancellationToken ct = default);
    Task<bool> DeleteConnectionAsync(Guid connectionId, CancellationToken ct = default);
    Task<List<(Guid Id, string Name, IDictionary<string, object> Properties, IDictionary<string, IPropertyMetadata> Metadata)>> GetAllStoredDevicesAsync(CancellationToken ct = default);
}
```

#### ITopologyService

Interface for component relationship management.

```csharp
public interface ITopologyService : IAsyncDisposable
{
    Task<IReadOnlyList<IComponentConnection>> GetConnectionsForSourceAsync(Guid sourceId, CancellationToken ct = default);
    Task<IReadOnlyList<IComponentConnection>> GetConnectionsForTargetAsync(Guid targetId, CancellationToken ct = default);
    Task<IComponentConnection> CreateConnectionAsync(IComponentConnection connection, CancellationToken ct = default);
    Task<bool> UpdateConnectionAsync(IComponentConnection connection, CancellationToken ct = default);
    Task<bool> DeleteConnectionAsync(Guid connectionId, CancellationToken ct = default);
    Task<bool> EvaluateConnectionConditionAsync(IComponentConnection connection, CancellationToken ct = default);
}
```

#### IEventProcessingPipeline

Interface for middleware-based event processing.

```csharp
public interface IEventProcessingPipeline
{
    Task AddMiddleware(IEventMiddleware middleware);
    Task AddMiddleware(IEventMiddleware middleware, params EventType[]? eventTypes);
    Task<bool> RemoveMiddleware(Guid middlewareId);
    Task<IEventProcessingResult> ProcessEventAsync(object? sender, IEvent @event, CancellationToken cancellationToken = default);
}
```

## Enumerations

### ComponentState

States for component lifecycle.

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

Types of events in the system.

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
    Error,
    Custom,
    StateChange
}
```

### ErrorSeverity

Severity levels for errors.

```csharp
public enum ErrorSeverity
{
    Warning,        // Operation can continue
    Error,          // Operation failed but component can recover
    Critical,       // Component needs external intervention
    Catastrophic    // System stability is at risk
}
```

### ErrorSource

Sources of errors.

```csharp
public enum ErrorSource
{
    Device,        // Hardware/IoT device errors
    Service,       // Service/application logic errors
    Communication, // Network/communication errors
    UI,            // User interface errors
    Database,      // Data persistence errors,
    System,        // System-level errors
    Unknown        // Uncategorized errors
}
```

### ErrorCategory

Categorization of errors.

```csharp
public enum ErrorCategory
{
    Unknown = 0,
    Device = 10,
    Service = 20,
    Communication = 30,
    EventSystem = 40,
    Storage = 50,
    Security = 60,
    System = 70
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

## Connection and Routing

### IComponentConnection

Interface for connections between components.

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

### IEventRoutingData

Interface for event routing metadata.

```csharp
public interface IEventRoutingData
{
    List<Guid> TargetIds { get; }
    bool Persist { get; }
    EventPriority Priority { get; }
    bool RequiresAcknowledgment { get; }
    TimeSpan? Timeout { get; }
}
```

### IEventRouter

Interface for event routing decisions.

```csharp
public interface IEventRouter
{
    Task<IReadOnlyList<IEventSubscription>> GetMatchingSubscriptionsAsync(
        IEvent @event, 
        IEnumerable<IEventSubscription> availableSubscriptions,
        CancellationToken ct = default);
    
    Task<bool> MatchesSubscriptionAsync(
        IEvent @event,
        IEventSubscription subscription,
        CancellationToken ct = default);
}
```

### IEventSubscription

Interface for event subscriptions.

```csharp
public interface IEventSubscription
{
    Guid Id { get; }
    IEventHandler<IEvent> Handler { get; }
    IEventSubscriptionOptions Options { get; }
}
```

### IEventSubscriptionOptions

Interface for event subscription configuration.

```csharp
public interface IEventSubscriptionOptions
{
    EventType[] EventTypes { get; set; }
    Guid[] SourceIds { get; set; }
    Func<IEvent, bool>? Filter { get; set; }
    bool IncludeConnectedSources { get; set; }
    bool Synchronous { get; set; }
}
```

## Middleware

### IEventMiddleware

Interface for processing pipeline middleware.

```csharp
public interface IEventMiddleware
{
    Guid Id { get; }
    int Priority { get; }
    Task<IMiddlewareProcessingResult> ProcessEventAsync(object? sender, IEvent evt, CancellationToken cancellationToken = default);
}
```

### IMiddlewareProcessingResult

Interface for middleware processing results.

```csharp
public interface IMiddlewareProcessingResult
{
    IEvent Event { get; }
    bool Success { get; }
    bool ShouldStopProcessing { get; }
    Exception? Exception { get; }
}
```

## Property Metadata

### IPropertyMetadata

Interface for property metadata.

```csharp
public interface IPropertyMetadata
{
    bool IsEditable { get; set; }
    bool IsVisible { get; set; }
    string? DisplayName { get; set; }
    string? Description { get; set; }
}
```

## Storage

### IStore

Interface for low-level storage.

```csharp
public interface IStore
{
    Task<IStoreTransaction> BeginTransactionAsync(CancellationToken ct = default);
    Task<IDictionary<string, object?>?> LoadAsync(Guid id, CancellationToken ct = default);
    Task<IDictionary<string, IPropertyMetadata>?> LoadMetadataAsync(Guid id, CancellationToken ct = default);
    Task SaveAsync(Guid id, IDictionary<string, object> properties, CancellationToken ct = default);
    Task SaveWithMetadataAsync(Guid id, IDictionary<string, object> properties,
        IDictionary<string, IPropertyMetadata>? metadata, CancellationToken ct = default);
}
```

### IStoreTransaction

Interface for storage transactions.

```csharp
public interface IStoreTransaction : IAsyncDisposable
{
    Task SaveAsync(Guid id, IDictionary<string, object> properties);
    Task SaveWithMetadataAsync(Guid id, IDictionary<string, object> properties,
        IDictionary<string, IPropertyMetadata>? metadata);
    Task CommitAsync(CancellationToken ct = default);
    Task RollbackAsync(CancellationToken ct = default);
}
```

## Result Objects

### IPublishResult

Interface for event publication results.

```csharp
public interface IPublishResult
{
    Guid EventId { get; set; }
    int HandlerCount { get; set; }
    int SuccessCount { get; set; }
    bool IsComplete { get; }
    bool TimedOut { get; set; }
    IReadOnlyList<Exception?> Errors { get; }
    bool HasErrors { get; }
    List<Task> HandlerTasks { get; }
}
```

### IEventProcessingResult

Interface for event processing results.

```csharp
public interface IEventProcessingResult
{
    IEvent Event { get; }
    IEvent ProcessedEvent { get; }
    bool IsSuccess { get; }
    bool ShouldRetry { get; }  // [Deprecated]
    Exception? Exception { get; }
    int RetryCount { get; }    // [Deprecated]
    TimeSpan RetryDelay { get; } // [Deprecated]
}
```