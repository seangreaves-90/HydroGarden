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
  - Event Processing Pipeline
  - Middleware components

- **HydroGarden.Foundation.Core**
  - Core component implementations
  - Base classes for devices and controllers
  - Service implementations
  - Storage implementations

- **HydroGarden.Foundation.ErrorHandling.Core**
  - Error handling and transformation implementations
  - Error-Event transformation service
  - Error monitoring implementations
  - Recovery coordination

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

## Event Processing Pipeline

New in Phase 2, the Event Processing Pipeline enhances event handling with middleware capabilities.

### Pipeline Configuration

```csharp
// Configure the pipeline using the builder
var pipeline = new EventPipelineBuilder(logger)
    .AddLogging(LoggingMiddleware.LoggingLevel.Detailed)
    .AddCircuitBreaker(failureThreshold: 5, resetTimeout: TimeSpan.FromMinutes(1))
    .AddRetry(maxRetries: 3, initialDelay: TimeSpan.FromSeconds(1))
    .AddDeadLetterQueue()
    .Build();

// Attach to the EventBus
eventBus.SetEventProcessingPipeline(pipeline);

// Or use extension method
var pipeline = eventBus.UsePipeline(logger, builder => 
{
    builder.AddLogging()
           .AddCircuitBreaker()
           .AddRetry()
           .AddDeadLetterQueue();
});
```

### Creating Custom Middleware

```csharp
public class CustomMiddleware : IEventMiddleware
{
    private readonly ILogger _logger;
    
    public CustomMiddleware(ILogger logger)
    {
        _logger = logger;
        Id = Guid.NewGuid();
        Name = "Custom Middleware";
        Order = 300; // Run after logging but before retry
    }

    public Guid Id { get; }
    public string Name { get; }
    public int Order { get; }

    public async Task<IEventProcessingResult> ProcessAsync(
        object sender,
        IEvent @event,
        Func<object, IEvent, CancellationToken, Task<IEventProcessingResult>> next,
        CancellationToken cancellationToken = default)
    {
        // Do something before the next middleware
        _logger.Log($"Custom middleware processing event {@event.EventId}");

        // Call the next middleware in the pipeline
        var result = await next(sender, @event, cancellationToken);

        // Do something after the next middleware
        if (!result.IsSuccess)
        {
            _logger.Log($"Event {@event.EventId} failed processing");
        }

        return result;
    }

    public bool ShouldApply(IEvent @event)
    {
        // Apply this middleware to all events except system events
        return @event.EventType != EventType.System;
    }
}

// Add to pipeline
pipeline.AddMiddleware(new CustomMiddleware(logger));
```

### Using the Dead Letter Queue

```csharp
// Access the dead letter queue middleware
var deadLetterQueueMiddleware = serviceProvider.GetRequiredService<DeadLetterQueueMiddleware>();

// Get all entries
var failedEvents = deadLetterQueueMiddleware.GetAllEntries();

// Process failed events
foreach (var entry in failedEvents)
{
    Console.WriteLine($"Failed event: {entry.EventId}, Error: {entry.ErrorMessage}");
    
    // Attempt to reprocess
    if (entry.ProcessingAttempts < 5)
    {
        await eventBus.PublishAsync(this, entry.Event);
        deadLetterQueueMiddleware.RemoveEntry(entry.Id);
    }
}
```

## Recovery Orchestration

New in Phase 3, the Recovery Orchestration Service provides advanced error recovery capabilities.

### Using the Recovery Orchestration Service

```csharp
// Attempt to recover from an error
var recoveryStatus = await recoveryOrchestrationService.AttemptRecoveryAsync(error);

if (recoveryStatus.IsSuccessful)
{
    Console.WriteLine($"Recovery successful using {recoveryStatus.SuccessfulStrategy}");
}
else
{
    Console.WriteLine("Recovery failed");
}

// Attempt to recover a device (handles all active errors)
var deviceRecoveryStatus = await recoveryOrchestrationService.RecoverDeviceAsync(deviceId);

// Create a recovery plan without executing it
var plan = await recoveryOrchestrationService.CreateRecoveryPlanAsync(error);

// Add custom context to the plan
plan.Context["MaxRetries"] = 5;
plan.Context["Priority"] = "High";

// Execute the plan
var executionStatus = await recoveryOrchestrationService.ExecuteRecoveryPlanAsync(plan);
```

### Creating Custom Recovery Strategies

```csharp
public class CustomRecoveryStrategy : RecoveryStrategyBase
{
    private readonly IMyService _service;
    
    public CustomRecoveryStrategy(ILogger logger, IMyService service)
        : base(logger)
    {
        _service = service;
    }
    
    public override string Name => "Custom Recovery Strategy";
    
    public override int Priority => 20; // Lower numbers run first
    
    public override ErrorTaxonomy.RecoveryComplexity ComplexityLevel =>
        ErrorTaxonomy.RecoveryComplexity.Moderate;
    
    public override ErrorTaxonomy.RootCause[] SupportedRootCauses => new[]
    {
        ErrorTaxonomy.RootCause.ConfigurationError,
        ErrorTaxonomy.RootCause.ValidationFailure
    };
    
    public override bool CanRecover(IApplicationError error)
    {
        // Use base implementation first (checks root causes)
        if (!base.CanRecover(error))
            return false;
            
        // Add custom logic
        return error.ErrorCode == "CUSTOM_ERROR_CODE";
    }
    
    protected override async Task<bool> ExecuteRecoveryAsync(IApplicationError error, CancellationToken ct)
    {
        try
        {
            // Implement recovery logic
            await _service.FixIssueAsync(error.DeviceId, ct);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Log(ex, "Custom recovery failed");
            return false;
        }
    }
}

// Register the strategy
recoveryOrchestrationService.RegisterStrategy(new CustomRecoveryStrategy(logger, myService));
```

### Analyzing Recovery Performance

```csharp
// Get overall recovery statistics
var metrics = await recoveryOrchestrationService.GetRecoveryStatisticsAsync(
    DateTimeOffset.UtcNow.AddDays(-7));
    
foreach (var (errorCode, metric) in metrics)
{
    Console.WriteLine($"Error {errorCode}:");
    Console.WriteLine($"  Success rate: {metric.SuccessRate}%");
    Console.WriteLine($"  Total attempts: {metric.TotalAttempts}");
    Console.WriteLine($"  Best strategy: {metric.MostSuccessfulStrategy}");
    Console.WriteLine($"  Avg recovery time: {metric.AverageRecoveryTimeMs}ms");
}

// Get device-specific recovery history
var history = await recoveryOrchestrationService.GetRecoveryHistoryAsync(deviceId);
foreach (var record in history)
{
    Console.WriteLine($"{record.Timestamp}: {(record.IsSuccessful ? "Success" : "Failed")} using {record.StrategyUsed}");
}
```

## Error-Event Integration

New in Phase 1, the Error-Event integration allows errors to be published as events and vice versa.

### Publishing Errors as Events

```csharp
// Using the transformation service directly
await errorEventTransformationService.PublishErrorAsEventAsync(applicationError);

// Extension method for IErrorMonitor
await errorMonitor.PublishErrorAsEventAsync(applicationError);

// Publishing a recovery attempt
await errorEventTransformationService.PublishRecoveryAsEventAsync(
    deviceId,
    "SENSOR_FAILURE",
    isSuccessful: true,
    message: "Sensor reconnected successfully",
    correlationId: errorEvent.CorrelationId);
```

### Subscribing to Error Events

```csharp
// Using extension methods
eventBus.SubscribeToErrorEvents(
    async (error, ct) =>
    {
        // Handle the error
        Console.WriteLine($"Received error: {error.ErrorCode} - {error.Message}");
        
        // Attempt recovery
        if (error.ErrorCode == "DEVICE_OFFLINE")
        {
            await TryReconnectDeviceAsync(error.DeviceId);
        }
    });

// Subscribe to recovery events
eventBus.SubscribeToRecoveryEvents(
    async (deviceId, errorCode, successful, message, correlationId, ct) =>
    {
        if (successful)
        {
            Console.WriteLine($"Device {deviceId} recovered from {errorCode}: {message}");
        }
        else
        {
            Console.WriteLine($"Recovery failed for device {deviceId}, error {errorCode}: {message}");
        }
    });
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

### Unit Testing the Event Processing Pipeline

```csharp
[Fact]
public async Task RetryMiddleware_WhenEventFails_ShouldRetrySpecifiedTimes()
{
    // Arrange
    var logger = new TestLogger();
    var middleware = new RetryMiddleware(logger, maxRetries: 3);
    
    var event = new TestEvent { EventId = Guid.NewGuid() };
    var failCount = 0;
    
    Func<object, IEvent, CancellationToken, Task<IEventProcessingResult>> next = 
        (sender, evt, ct) =>
        {
            failCount++;
            if (failCount <= 2) // Fail twice
            {
                return Task.FromResult<IEventProcessingResult>(
                    EventProcessingResult.Failure(evt, new Exception("Test failure"), shouldRetry: true));
            }
            else // Succeed on third attempt
            {
                return Task.FromResult<IEventProcessingResult>(
                    EventProcessingResult.Success(evt));
            }
        };
    
    // Act
    var result = await middleware.ProcessAsync(this, event, next, CancellationToken.None);
    
    // Assert
    Assert.True(result.IsSuccess);
    Assert.Equal(3, failCount); // Should have attempted 3 times
}
```

### Unit Testing Error-Event Transformation

```csharp
[Fact]
public void TransformErrorToEvent_ShouldCreateValidErrorEvent()
{
    // Arrange
    var mockEventBus = new Mock<IEventBus>();
    var mockLogger = new Mock<ILogger>();
    var service = new ErrorEventTransformationService(mockEventBus.Object, mockLogger.Object);
    
    var error = new TestError
    {
        DeviceId = Guid.NewGuid(),
        ErrorCode = "TEST_ERROR",
        Message = "Test error message",
        Severity = ErrorSeverity.Error,
        CorrelationId = Guid.NewGuid()
    };
    
    // Act
    var errorEvent = service.TransformErrorToEvent(error);
    
    // Assert
    Assert.Equal(error.DeviceId, errorEvent.DeviceId);
    Assert.Equal(error.ErrorCode, errorEvent.ErrorCode);
    Assert.Equal(error.Message, errorEvent.Message);
    Assert.Equal(error.Severity, errorEvent.Severity);
    Assert.Equal(error.CorrelationId, errorEvent.CorrelationId);
}
```

### Integration Testing

```csharp
[Fact]
public async Task EventBus_WithPipeline_ShouldRouteCommands()
{
    // Arrange
    using var eventBus = CreateTestEventBus();
    var logger = new TestLogger();
    
    // Configure pipeline
    var pipeline = new EventPipelineBuilder(logger)
        .AddLogging()
        .AddRetry(maxRetries: 1)
        .Build();
    
    eventBus.SetEventProcessingPipeline(pipeline);
    
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
   - Use the LoggingMiddleware with Diagnostic level for detailed event processing logs

2. **Monitor Event Flow**
   - Use the EventBus diagnostic features to see event routing
   - Add event subscription to monitor all events during debugging
   - Check the Dead Letter Queue for failed events

3. **Circuit Breaker Monitoring**
   - Examine circuit breaker states for different event types
   - Check for open circuits when events aren't being delivered
   - Manually reset circuits for testing

4. **Use TestConsole**
   - The TestConsole project is helpful for isolated testing
   - Manually trigger events and observe system behavior

5. **Common Issues**
   - Event subscriptions not matching expected events
   - Incorrect event routing due to topology setup
   - Transaction failures in persistence layer
   - Asynchronous timing issues in event handling
   - Middleware ordering problems

## Performance Considerations

1. **Event Batching**
   - Use batch operations for multiple property changes
   - PersistenceService supports batched persistence

2. **Subscription Filtering**
   - Be specific in subscription filters to reduce processing
   - Use source IDs and event types to limit event delivery

3. **Middleware Efficiency**
   - Only add necessary middleware to the pipeline
   - Use ShouldApply method to skip middleware for certain events
   - Consider middleware order for optimal performance

4. **Circuit Breaking**
   - Use circuit breakers to prevent overwhelming failing components
   - Configure appropriate thresholds based on component importance

5. **Memory Management**
   - Configure appropriate Dead Letter Queue capacity
   - Enable cleanup for old entries
   - Be mindful of event size when publishing

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
   - Create handoff documents when transitioning work to another developer