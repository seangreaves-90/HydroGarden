# HydroGarden Development Guide

This guide provides information for developers working on the HydroGarden system, including best practices, project structure, and development workflows.

## Project Structure

The HydroGarden solution consists of several projects organized by responsibility:

### Foundation Layer

- **HydroGarden.Foundation.Abstractions**
  - Interfaces and abstract types that define the system contracts
  - Event definitions
  - Service interfaces
  - Component interfaces

- **HydroGarden.Foundation.Common**
  - Common implementations shared across the system
  - Event implementations
  - Utility classes
  - Extension methods

- **HydroGarden.Foundation.Core**
  - Core component implementations
  - Base classes for devices and controllers
  - Service implementations
  - Storage implementations

### Testing Projects

- **HydroGarden.Foundation.Tests.Unit**
  - Unit tests for individual components
  - Mock implementations for testing

- **HydroGarden.Foundation.Tests.Integration**
  - Integration tests for component interactions
  - End-to-end testing of system flows

### Application Layer

- **HydroGarden.Service**
  - Main application entry point
  - Component composition
  - Configuration management

- **HydroGarden.UI**
  - Web interface for the system
  - SignalR integration
  - API controllers

### Utilities

- **TestConsole**
  - Console application for testing and demonstration
  - Manual component interaction

## Event System Overview

The event system is the backbone of HydroGarden, providing communication between all components.

### Event Types

1. **PropertyChanged**
   - Triggered when a component property changes
   - Contains property name, old value, new value, and metadata

2. **Lifecycle**
   - Represents component state transitions
   - States: Created, Initializing, Ready, Running, Stopping, Error, Disposed

3. **Command**
   - Requests for components to perform actions
   - Contains command name and parameters

4. **Telemetry**
   - Sensor readings and measurements
   - Contains named readings and optional units

5. **Alert**
   - System warnings and notifications
   - Contains severity, message, and additional data

### Creating Events

```csharp
// Property changed event
var propEvent = new HydroGardenPropertyChangedEvent(
    deviceId,
    "Temperature",
    typeof(double),
    oldTemp,
    newTemp,
    new PropertyMetadata(true, true, "Temperature", "Current temperature reading")
);

// Command event
var command = new CommandEvent(
    pumpId,
    "Start",
    new Dictionary<string, object> { { "Duration", TimeSpan.FromMinutes(5) } }
);

// Lifecycle event
var lifecycle = new LifecycleEvent(
    deviceId,
    ComponentState.Running,
    "Device started successfully"
);
```

### Publishing Events

```csharp
// Publish an event
await _eventBus.PublishAsync(this, propEvent);

// Publish with specific routing
var routingData = new EventRoutingData
{
    TargetIds = new[] { targetDeviceId },
    Priority = EventPriority.High,
    Persist = true
};

var eventWithRouting = new CommandEvent(
    sourceId,
    "EmergencyStop",
    null,
    routingData
);

await _eventBus.PublishAsync(this, eventWithRouting);
```

### Subscribing to Events

```csharp
// Basic subscription
_eventBus.Subscribe(this, new EventSubscriptionOptions
{
    EventTypes = new[] { EventType.PropertyChanged },
    SourceIds = new[] { sensorId }
});

// Advanced filtering
_eventBus.Subscribe(this, new EventSubscriptionOptions
{
    EventTypes = new[] { EventType.PropertyChanged },
    Filter = evt => 
        evt is IHydroGardenPropertyChangedEvent propEvt && 
        propEvt.PropertyName == "Temperature" && 
        propEvt.NewValue is double temp && 
        temp > 30.0
});

// Including connected sources
_eventBus.Subscribe(this, new EventSubscriptionOptions
{
    EventTypes = new[] { EventType.Telemetry },
    SourceIds = new[] { controllerId },
    IncludeConnectedSources = true
});
```

### Handling Events

```csharp
public class TemperatureController : HydroGardenComponentBase, IHydroGardenPropertyChangedEventHandler
{
    // Called for property change events
    public async Task HandleEventAsync(object sender, IHydroGardenPropertyChangedEvent evt, CancellationToken ct)
    {
        if (evt.PropertyName == "Temperature" && evt.NewValue is double temperature)
        {
            await ProcessTemperatureChangeAsync(temperature);
        }
    }
    
    // Extension method for lifecycle events
    public async Task HandleLifecycleEventAsync(object sender, IHydroGardenLifecycleEvent evt, CancellationToken ct)
    {
        if (evt.State == ComponentState.Error)
        {
            await HandleDeviceErrorAsync(evt.SourceId, evt.Details);
        }
    }
    
    // ...
}
```

## Creating New Components

### IoT Device

```csharp
public class TemperatureSensor : IoTDeviceBase
{
    private readonly Timer _readingTimer;
    private readonly Random _simulator = new();
    private double _baseTemperature = 21.0;
    
    public TemperatureSensor(Guid id, string name, IHydroGardenLogger logger = null)
        : base(id, name, logger)
    {
        _readingTimer = new Timer(OnReadingTimer, null, Timeout.Infinite, Timeout.Infinite);
    }
    
    protected override async Task OnInitializeAsync(CancellationToken ct)
    {
        await SetPropertyAsync("CurrentTemperature", _baseTemperature);
        await SetPropertyAsync("Unit", "°C");
        await base.OnInitializeAsync(ct);
    }
    
    protected override Task OnStartAsync(CancellationToken ct)
    {
        _readingTimer.Change(TimeSpan.Zero, TimeSpan.FromSeconds(5));
        return base.OnStartAsync(ct);
    }
    
    protected override Task OnStopAsync(CancellationToken ct)
    {
        _readingTimer.Change(Timeout.Infinite, Timeout.Infinite);
        return base.OnStopAsync(ct);
    }
    
    private async void OnReadingTimer(object state)
    {
        try
        {
            // Simulate a temperature reading
            var reading = _baseTemperature + (_simulator.NextDouble() * 2) - 1;
            await SetPropertyAsync("CurrentTemperature", Math.Round(reading, 1));
            await SetPropertyAsync("Timestamp", DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.Log(ex, "Error updating temperature reading");
        }
    }
    
    public override void Dispose()
    {
        _readingTimer?.Dispose();
        base.Dispose();
    }
}
```

### Controller

```csharp
public class TemperatureController : HydroGardenComponentBase, IHydroGardenPropertyChangedEventHandler
{
    private readonly IEventBus _eventBus;
    private readonly Guid _heaterId;
    private readonly Guid _chillerId;
    private double _targetTemperature = 24.0;
    private double _tolerance = 1.0;
    
    public TemperatureController(
        Guid id, 
        string name, 
        IEventBus eventBus,
        Guid heaterId,
        Guid chillerId,
        IHydroGardenLogger logger = null)
        : base(id, name, logger)
    {
        _eventBus = eventBus;
        _heaterId = heaterId;
        _chillerId = chillerId;
    }
    
    public async Task InitializeAsync()
    {
        await SetPropertyAsync("TargetTemperature", _targetTemperature);
        await SetPropertyAsync("Tolerance", _tolerance);
        
        // Subscribe to temperature sensor events
        _eventBus.Subscribe(this, new EventSubscriptionOptions
        {
            EventTypes = new[] { EventType.PropertyChanged },
            Filter = evt => evt is IHydroGardenPropertyChangedEvent propEvt && 
                           propEvt.PropertyName == "CurrentTemperature"
        });
    }
    
    public async Task HandleEventAsync(object sender, IHydroGardenPropertyChangedEvent evt, CancellationToken ct)
    {
        if (evt.PropertyName == "CurrentTemperature" && evt.NewValue is double temperature)
        {
            await ProcessTemperatureAsync(temperature);
        }
    }
    
    private async Task ProcessTemperatureAsync(double temperature)
    {
        await SetPropertyAsync("CurrentTemperature", temperature);
        
        if (temperature < _targetTemperature - _tolerance)
        {
            // Too cold, activate heater
            await ActivateHeaterAsync();
            await DeactivateChillerAsync();
        }
        else if (temperature > _targetTemperature + _tolerance)
        {
            // Too hot, activate chiller
            await DeactivateHeaterAsync();
            await ActivateChillerAsync();
        }
        else
        {
            // Within acceptable range, deactivate both
            await DeactivateHeaterAsync();
            await DeactivateChillerAsync();
        }
    }
    
    private async Task ActivateHeaterAsync()
    {
        var command = new CommandEvent(
            _heaterId,
            "Activate",
            new Dictionary<string, object> { { "Power", 100 } }
        );
        
        await _eventBus.PublishAsync(this, command);
        await SetPropertyAsync("HeaterActive", true);
    }
    
    // Additional implementation omitted for brevity
    
    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
```

## Testing Best Practices

### Unit Testing

```csharp
[Fact]
public async Task TemperatureSensor_WhenStarted_ShouldPublishReadings()
{
    // Arrange
    var mockEventHandler = new Mock<IHydroGardenPropertyChangedEventHandler>();
    var sensorId = Guid.NewGuid();
    var sensor = new TemperatureSensor(sensorId, "Test Sensor");
    
    sensor.SetEventHandler(mockEventHandler.Object);
    
    // Act
    await sensor.InitializeAsync();
    await sensor.StartAsync();
    
    // Wait for readings
    await Task.Delay(1000);
    
    // Assert
    mockEventHandler.Verify(h => h.HandleEventAsync(
        It.IsAny<object>(),
        It.Is<IHydroGardenPropertyChangedEvent>(e => 
            e.PropertyName == "CurrentTemperature" && 
            e.NewValue is double),
        It.IsAny<CancellationToken>()),
        Times.AtLeastOnce);
}
```

### Integration Testing

```csharp
[Fact]
public async Task EventBus_ComponentIntegration_ShouldRouteCommands()
{
    // Arrange
    using var eventBus = CreateTestEventBus();
    var deviceId = Guid.NewGuid();
    var controllerId = Guid.NewGuid();
    
    // Set up mock handlers
    var deviceHandler = new Mock<IHydroGardenPropertyChangedEventHandler>();
    var controllerHandler = new Mock<IHydroGardenPropertyChangedEventHandler>();
    
    // Subscribe handlers
    var deviceOptions = new EventSubscriptionOptions
    {
        EventTypes = new[] { EventType.Command },
        SourceIds = new[] { deviceId }
    };
    
    var controllerOptions = new EventSubscriptionOptions
    {
        EventTypes = new[] { EventType.PropertyChanged },
        SourceIds = new[] { deviceId }
    };
    
    eventBus.Subscribe(deviceHandler.Object, deviceOptions);
    eventBus.Subscribe(controllerHandler.Object, controllerOptions);
    
    // Act - Send command to device
    var command = new CommandEvent(
        deviceId,
        "ChangeMode",
        new Dictionary<string, object> { { "Mode", "Eco" } }
    );
    
    await eventBus.PublishAsync(this, command);
    
    // Assert - Command was received by device
    deviceHandler.Verify(h => h.HandleEventAsync(
        It.IsAny<object>(),
        It.IsAny<IHydroGardenPropertyChangedEvent>(),
        It.IsAny<CancellationToken>()),
        Times.Once);
}
```

## Debugging Tips

1. **Enable Diagnostic Logging**
   - Set log level to Debug or Trace during development
   - Use the `_logger.Log()` method liberally for visibility

2. **Monitor Event Flow**
   - Use the EventBus diagnostic features to see event routing
   - Add event subscription to monitor all events during debugging

3. **Use TestConsole**
   - The TestConsole project is helpful for isolated testing
   - Manually trigger events and observe system behavior

4. **Common Issues**
   - Event subscriptions not matching expected events
   - Incorrect event routing due to topology setup
   - Transaction failures in persistence layer
   - Asynchronous timing issues in event handling

## Performance Considerations

1. **Event Batching**
   - Use batch operations for multiple property changes
   - PersistenceService supports batched persistence

2. **Subscription Filtering**
   - Be specific in subscription filters to reduce processing
   - Use source IDs and event types to limit event delivery

3. **Transaction Management**
   - Keep transactions short-lived
   - Use appropriate isolation levels

4. **Memory Management**
   - Be mindful of event capture and storage
   - Consider event pruning for long-running systems

## Contributing Guidelines

1. **Code Style**
   - Follow C# coding conventions
   - Use async/await consistently
   - Document public APIs with XML comments

2. **Pull Request Process**
   - Create feature branches from `develop`
   - Include unit tests for new functionality
   - Update documentation as needed
   - Request code review from team members

3. **Testing Requirements**
   - All code should have unit test coverage
   - Integration tests for new features
   - Manual verification with TestConsole

4. **Documentation**
   - Update relevant documentation files
   - Include code comments for complex logic
   - Provide examples for new features