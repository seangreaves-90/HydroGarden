# HydroGarden Usage Examples

This document provides comprehensive usage examples for all major components of the HydroGarden system. These examples are designed to help developers understand how to properly integrate and utilize the various components.

## Table of Contents

1. [Event System](#event-system)
    - [EventBus](#eventbus)
    - [Event Processing Pipeline](#event-processing-pipeline)
    - [Event Handlers](#event-handlers)
    - [Event Routing](#event-routing)
2. [Components](#components)
    - [Base Components](#base-components)
    - [IoT Devices](#iot-devices)
    - [Pump Device](#pump-device)
3. [Error Handling](#error-handling)
    - [Error Monitoring](#error-monitoring)
    - [Error-Event Transformation](#error-event-transformation)
    - [Error Recovery](#error-recovery)
4. [Persistence](#persistence)
    - [PersistenceService](#persistenceservice)
    - [JsonStore](#jsonstore)
5. [Topology](#topology)
    - [Topology Service](#topology-service)
    - [Component Connections](#component-connections)

## Event System

### EventBus

The EventBus is the central messaging component of the HydroGarden system.

#### Publishing Events

```csharp
// Create an event
var propertyEvent = new HydroGardenPropertyChangedEvent(
    deviceId: sensorId,
    propertyName: "Temperature",
    propertyType: typeof(double),
    oldValue: 22.5,
    newValue: 23.1,
    metadata: new PropertyMetadata { 
        IsEditable = false, 
        IsVisible = true, 
        DisplayName = "Temperature", 
        Description = "Current temperature reading" 
    }
);

// Publish the event
await eventBus.PublishAsync(this, propertyEvent);

// Using routing data for targeted delivery
var routingData = new EventRoutingData()
{
    TargetIds = new List<Guid> { controllerId },
    Priority = EventPriority.High
};

var alertEvent = new AlertEvent(
    deviceId: sensorId,
    severity: AlertSeverity.Warning,
    message: "Temperature exceeding normal range",
    routingData: routingData
);

await eventBus.PublishAsync(this, alertEvent);
```

#### Subscribing to Events

```csharp
// Basic subscription
var subscriptionId = eventBus.Subscribe<IEvent>(eventHandler, new EventSubscriptionOptions 
{
    EventTypes = new[] { EventType.PropertyChanged }
});

// Type-specific subscription
var typedSubscriptionId = eventBus.Subscribe<IPropertyChangedEvent>(propertyChangedHandler);

// Advanced filtering
var filteredSubscriptionId = eventBus.Subscribe<IEvent>(eventHandler, new EventSubscriptionOptions 
{
    EventTypes = new[] { EventType.PropertyChanged, EventType.Telemetry },
    SourceIds = new[] { sensorId, pumpId },
    Filter = evt => 
        (evt is IPropertyChangedEvent propEvt && propEvt.PropertyName == "Status") ||
        (evt is ITelemetryEvent telEvt && telEvt.Readings.ContainsKey("FlowRate"))
});

// Unsubscribing
eventBus.Unsubscribe(subscriptionId);
```

### Event Processing Pipeline

The Event Processing Pipeline enables middleware-based event processing.

#### Configuring the Pipeline

```csharp
// Basic pipeline with defaults
var pipeline = new DefaultEventProcessingPipeline();
eventBus.SetEventProcessingPipeline(pipeline);

// Add middleware
var loggingMiddleware = new LoggingMiddleware(logger);
pipeline.AddMiddleware(loggingMiddleware);

// Add transformation middleware
var transformerMiddleware = new DefaultTransformerMiddleware();
pipeline.AddMiddleware(transformerMiddleware);

// Add state change middleware
var stateChangeMiddleware = new StateChangeMiddleware();
pipeline.AddMiddleware(stateChangeMiddleware);

// Add validation middleware
var validationMiddleware = new EventValidationMiddleware();
pipeline.AddMiddleware(validationMiddleware);
```

### Event Handlers

Creating and implementing event handlers for various event types.

```csharp
// Property changed event handler
public class SensorReadingHandler : IEventHandler<IPropertyChangedEvent>
{
    public async Task HandleAsync(IPropertyChangedEvent @event, CancellationToken ct = default)
    {
        if (@event.PropertyName == "Temperature" && @event.NewValue is double temperature)
        {
            Console.WriteLine($"Temperature changed to {temperature}°C");
            await ProcessTemperatureAsync(temperature);
        }
    }
    
    private Task ProcessTemperatureAsync(double temperature)
    {
        // Processing logic
        return Task.CompletedTask;
    }
}

// Using adapter to handle multiple event types
public class MultiEventHandler : IEventHandler
{
    public async Task HandleEventAsync<T>(object? sender, T evt, CancellationToken ct = default) where T : IEvent
    {
        switch (evt)
        {
            case IPropertyChangedEvent propEvent:
                await HandlePropertyChangedAsync(propEvent, ct);
                break;
            case IAlertEvent alertEvent:
                await HandleAlertAsync(alertEvent, ct);
                break;
            case ICommandEvent cmdEvent:
                await HandleCommandAsync(cmdEvent, ct);
                break;
        }
    }
    
    private Task HandlePropertyChangedAsync(IPropertyChangedEvent evt, CancellationToken ct)
    {
        // Handle property changed
        return Task.CompletedTask;
    }
    
    private Task HandleAlertAsync(IAlertEvent evt, CancellationToken ct)
    {
        // Handle alert
        return Task.CompletedTask;
    }
    
    private Task HandleCommandAsync(ICommandEvent evt, CancellationToken ct)
    {
        // Handle command
        return Task.CompletedTask;
    }
    
    public ValueTask DisposeAsync()
    {
        // Cleanup resources
        return ValueTask.CompletedTask;
    }
}

// Using the generic handler adapter
var adapter = new GenericEventHandlerAdapter<IPropertyChangedEvent>(new SensorReadingHandler());
eventBus.Subscribe<IEvent>(adapter, null);
```

### Event Routing

Configuring advanced event routing behavior.

```csharp
// Create routers
var directRouter = new DirectEventRouter();
var topologyRouter = new TopologyEventRouter(topologyService);

// Combine routers with composite router
var compositeRouter = new CompositeEventRouter(
    new List<IEventRouter> { directRouter, topologyRouter }
);

// Set up event bus with custom router
var eventBus = new EventBus(logger, compositeRouter);

// Get matching subscriptions for an event manually
var event = new PropertyChangedEvent(...);
var subscriptions = await eventBus.GetMatchingSubscriptionsAsync(event);
```

## Components

### Base Components

Working with the foundation of the component system.

```csharp
// Create a basic component
public class BasicComponent : ComponentBase
{
    public BasicComponent(Guid id, string? name = null) 
        : base(id, name)
    {
    }
    
    public async Task InitializeWithPropertiesAsync()
    {
        // Set initial properties
        await SetPropertyAsync("SerialNumber", "SN12345");
        await SetPropertyAsync("Firmware", "v1.2.3");
        await SetPropertyAsync("Location", "Zone A", 
            ConstructDefaultPropertyMetadata("Location", true, true));
        
        // Transition state
        await TransitionToStateAsync(ComponentState.Ready);
    }
    
    // Override property validation
    protected override bool ValidatePropertyValue(string name, object? value, IPropertyMetadata? metadata)
    {
        if (name == "Location" && value is string location)
        {
            return !string.IsNullOrEmpty(location) && location.StartsWith("Zone");
        }
        
        return base.ValidatePropertyValue(name, value, metadata);
    }
}

// Using the component
var component = new BasicComponent(Guid.NewGuid(), "Component1");
await component.InitializeWithPropertiesAsync();

// Get property values
var serialNumber = await component.GetPropertyAsync<string>("SerialNumber");
var metadata = component.GetPropertyMetadata("Location");
var allProperties = component.GetProperties();
```

### IoT Devices

Working with the IoT device foundation classes.

```csharp
// Creating a temperature sensor device
public class TemperatureSensor : IotDeviceBase
{
    private readonly Random _random = new Random();
    private Timer? _readingTimer;
    
    public TemperatureSensor(Guid id, string? name, IErrorMonitor errorMonitor) 
        : base(id, name, errorMonitor)
    {
    }
    
    protected override async Task OnInitializeAsync(CancellationToken ct)
    {
        // Set up properties
        await SetPropertyAsync("CurrentTemperature", 21.0);
        await SetPropertyAsync("Unit", "°C");
        await SetPropertyAsync("UpdateInterval", TimeSpan.FromSeconds(5));
        
        await base.OnInitializeAsync(ct);
    }
    
    protected override Task OnStartAsync(CancellationToken ct)
    {
        // Start reading timer
        _readingTimer = new Timer(ReadSensor, null, TimeSpan.Zero, 
            TimeSpan.FromSeconds(5));
        
        return base.OnStartAsync(ct);
    }
    
    protected override Task OnStopAsync(CancellationToken ct)
    {
        // Stop reading timer
        _readingTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        
        return base.OnStopAsync(ct);
    }
    
    private async void ReadSensor(object? state)
    {
        try
        {
            // Simulate reading
            var temperature = 20.0 + (_random.NextDouble() * 5.0);
            await SetPropertyAsync("CurrentTemperature", Math.Round(temperature, 1));
            await SetPropertyAsync("LastUpdated", DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            // Report error
            await ReportErrorAsync(
                new ComponentError(
                    Id,
                    "SENSOR_READ_ERROR",
                    $"Failed to read temperature: {ex.Message}",
                    ErrorSeverity.Error,
                    ErrorSource.Device,
                    ex
                )
            );
        }
    }
    
    public override void Dispose()
    {
        _readingTimer?.Dispose();
        base.Dispose();
    }
}

// Using the device
var sensor = new TemperatureSensor(
    Guid.NewGuid(), 
    "Zone A Sensor", 
    errorMonitor
);

await sensor.InitializeAsync();
await sensor.StartAsync();

// Reading properties
var currentTemp = await sensor.GetPropertyAsync<double>("CurrentTemperature");
var unit = await sensor.GetPropertyAsync<string>("Unit");

// Stopping
await sensor.StopAsync();
```

### Pump Device

Using the specialized pump device component.

```csharp
// Creating a pump device instance
var pump = new PumpDevice(
    Guid.NewGuid(),
    "Nutrient Pump",
    errorMonitor
);

// Initialize with configuration
await pump.InitializeAsync();

// Set custom properties 
await pump.SetPropertyAsync("FlowRate", 2.5);
await pump.SetPropertyAsync("MaxFlowRate", 5.0);
await pump.SetPropertyAsync("MinFlowRate", 0.5);

// Start the pump
await pump.StartAsync();

// Control the pump
await pump.SetPropertyAsync("IsRunning", true);
await pump.SetPropertyAsync("FlowRate", 3.0);

// Check status
var isRunning = await pump.GetPropertyAsync<bool>("IsRunning");
var flowRate = await pump.GetPropertyAsync<double>("FlowRate");

// Stop the pump
await pump.SetPropertyAsync("IsRunning", false);
await pump.StopAsync();
```

## Error Handling

### Error Monitoring

Using the error monitoring system to report and track errors.

```csharp
// Creating errors
var error = new ComponentError(
    deviceId: deviceId,
    errorCode: "DEVICE_OFFLINE",
    message: "Device not responding to communication attempts",
    severity: ErrorSeverity.Error,
    source: ErrorSource.Device,
    context: new Dictionary<string, object>
    {
        { "LastSeen", DateTime.UtcNow.AddMinutes(-10) },
        { "ConnectionAttempts", 3 }
    }
);

// Reporting errors
await errorMonitor.ReportErrorAsync(error);

// Reporting exceptions
await errorMonitor.ReportExceptionAsync(
    source: this,
    exception: exception,
    errorCode: "CONFIG_PARSE_ERROR",
    message: "Failed to parse configuration file",
    severity: ErrorSeverity.Critical,
    errorSource: ErrorSource.Service,
    context: new Dictionary<string, object>
    {
        { "ConfigFile", "system.json" },
        { "ConfigVersion", "1.2.3" }
    }
);

// Querying errors
var hasErrors = await errorMonitor.HasActiveErrorsAsync(ErrorSeverity.Warning);
var deviceErrors = await errorMonitor.GetActiveErrorsForDeviceAsync(deviceId);
var recentErrors = await errorMonitor.GetRecentErrorsAsync(10);

// Clearing errors
await errorMonitor.ClearErrorAsync(deviceId, "DEVICE_OFFLINE");
```

### Error-Event Transformation

Using the error-event transformation service to convert between errors and events.

```csharp
// Creating the service
var errorTransformationService = new ErrorEventTransformationService(eventBus);

// Convert an error to an event
var errorEvent = errorTransformationService.TransformErrorToEvent(error);

// Convert the event to a publishable event
var publishableEvent = errorTransformationService.TransformToPublishableEvent(errorEvent);

// Extract an error event from a general event
var extractedErrorEvent = errorTransformationService.ExtractErrorEvent(receivedEvent);

// Publish an error as an event
await errorTransformationService.PublishErrorAsEventAsync(error);
```

### Error Recovery

Implementing and using recovery strategies.

```csharp
// Error recovery in a device
public class RecoverableDevice : IotDeviceBase
{
    public RecoverableDevice(Guid id, string? name, IErrorMonitor errorMonitor) 
        : base(id, name, errorMonitor)
    {
    }
    
    // Implement recovery logic
    public override async Task<bool> TryRecoverAsync(CancellationToken ct = default)
    {
        try
        {
            // Perform recovery steps
            await ResetConnectionAsync();
            await RestoreDefaultConfigAsync();
            
            // Update state
            await TransitionToStateAsync(ComponentState.Ready);
            return true;
        }
        catch (Exception ex)
        {
            await ErrorMonitor.ReportExceptionAsync(
                this,
                ex,
                "RECOVERY_FAILED",
                "Failed to recover device",
                ErrorSeverity.Critical,
                ErrorSource.Device
            );
            return false;
        }
    }
    
    private Task ResetConnectionAsync()
    {
        // Implementation
        return Task.CompletedTask;
    }
    
    private Task RestoreDefaultConfigAsync()
    {
        // Implementation
        return Task.CompletedTask;
    }
}

// Using recovery from a component
public async Task HandleErrorAsync(IApplicationError error)
{
    try
    {
        // Report error to device
        await device.ReportErrorAsync(error);
        
        // Attempt recovery
        var recovered = await device.TryRecoverAsync();
        if (recovered)
        {
            Console.WriteLine("Device recovered successfully");
        }
        else
        {
            Console.WriteLine("Recovery failed, manual intervention required");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error during recovery: {ex.Message}");
    }
}
```

## Persistence

### PersistenceService

Using the persistence service to store and retrieve component data.

```csharp
// Create persistence service
var store = new JsonStore("data");
var persistenceService = new PersistenceService(store, logger);

// Register a component for persistence
await persistenceService.AddOrUpdateAsync(device);

// Get a stored property
var storedTemperature = await persistenceService.GetPropertyAsync<double>(
    deviceId, 
    "Temperature"
);

// Using transactions
using var transaction = await persistenceService.BeginTransactionAsync();

try
{
    // Store multiple components in a transaction
    await transaction.StoreComponentAsync(sensor1);
    await transaction.StoreComponentAsync(sensor2);
    
    // Store connection
    await transaction.StoreConnectionAsync(new ComponentConnection
    {
        SourceId = sensor1.Id,
        TargetId = controller.Id,
        ConnectionType = "Sensor-Controller"
    });
    
    // Commit the transaction
    await transaction.CommitAsync();
}
catch
{
    // Rollback on error
    await transaction.RollbackAsync();
    throw;
}

// Get all stored devices
var devices = await persistenceService.GetAllStoredDevicesAsync();
foreach (var (id, name, properties, metadata) in devices)
{
    Console.WriteLine($"Device: {name} ({id})");
    foreach (var prop in properties)
    {
        Console.WriteLine($"  {prop.Key}: {prop.Value}");
    }
}
```

### JsonStore

Direct usage of the JSON store for persistence.

```csharp
// Create a JSON store
var store = new JsonStore("data");

// Save properties
var properties = new Dictionary<string, object>
{
    { "Temperature", 23.5 },
    { "Humidity", 45.2 },
    { "LastUpdated", DateTimeOffset.UtcNow }
};

await store.SaveAsync(deviceId, properties);

// Save with metadata
var metadata = new Dictionary<string, IPropertyMetadata>
{
    { 
        "Temperature", 
        new PropertyMetadata 
        { 
            IsEditable = false, 
            IsVisible = true, 
            DisplayName = "Temperature", 
            Description = "Current temperature" 
        } 
    }
};

await store.SaveWithMetadataAsync(deviceId, properties, metadata);

// Load properties
var loadedProperties = await store.LoadAsync(deviceId);
var loadedMetadata = await store.LoadMetadataAsync(deviceId);

// Using a transaction
using var transaction = await store.BeginTransactionAsync();

try
{
    await transaction.SaveAsync(device1Id, device1Properties);
    await transaction.SaveAsync(device2Id, device2Properties);
    await transaction.CommitAsync();
}
catch
{
    await transaction.RollbackAsync();
    throw;
}
```

## Topology

### Topology Service

Using the topology service to manage component relationships.

```csharp
// Create topology service
var topologyService = new TopologyService(persistenceService);

// Create a connection
var connection = new ComponentConnection
{
    SourceId = sensorId,
    TargetId = controllerId,
    ConnectionType = "Sensor-Controller",
    IsEnabled = true,
    Condition = "Source.Temperature > 25.0"
};

var createdConnection = await topologyService.CreateConnectionAsync(connection);

// Get connections
var sourceConnections = await topologyService.GetConnectionsForSourceAsync(sensorId);
var targetConnections = await topologyService.GetConnectionsForTargetAsync(controllerId);

// Update a connection
connection.IsEnabled = false;
await topologyService.UpdateConnectionAsync(connection);

// Evaluate a condition
var isActive = await topologyService.EvaluateConnectionConditionAsync(connection);

// Delete a connection
await topologyService.DeleteConnectionAsync(connection.ConnectionId);

// Check if two devices are connected (using extension method)
var areConnected = await topologyService.AreDevicesConnectedAsync(sensorId, controllerId);
```

### Component Connections

Creating and using component connections.

```csharp
// Create a connection
var connection = new ComponentConnection
{
    ConnectionId = Guid.NewGuid(),
    SourceId = sensorId,
    TargetId = controllerId,
    ConnectionType = "Sensor-Controller",
    IsEnabled = true,
    Condition = "Source.Status == 'Ready' && Target.IsReady == true",
    Metadata = new Dictionary<string, object>
    {
        { "Priority", 1 },
        { "Category", "Environmental" }
    }
};

// Create a simple connection
var simpleConnection = new ComponentConnection
{
    SourceId = sensorId,
    TargetId = controllerId,
    ConnectionType = "Default",
    IsEnabled = true
};

// Create connection for event routing
var routingConnection = new ComponentConnection
{
    SourceId = controllerId,
    TargetId = actuatorId,
    ConnectionType = "Controller-Actuator",
    IsEnabled = true,
    Condition = "Event.EventType == 'Command'"
};
```
