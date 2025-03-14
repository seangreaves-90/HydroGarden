# HydroGarden Event System

## Overview

The HydroGarden Event System provides a robust and efficient mechanism for event-based communication between system components. The event system has been refactored to focus on core pub/sub functionality, with a clean separation from error handling concerns.

## Architecture

### Components

1. **RefactoredEventBus**
   - Core pub/sub event distribution system
   - Manages subscriptions and event delivery
   - Integrates with topology for event routing
   - Optional pipeline support for advanced processing

2. **EventSubscription**
   - Represents a handler's subscription to events
   - Includes filtering options for event types and sources

3. **EventProcessingPipeline**
   - Optional middleware-based processing pipeline
   - Allows for cross-cutting concerns like logging, validation, etc.
   - Can be configured with custom middleware

### Key Interfaces

- `IEventBus`: Core event bus functionality
- `IEventHandler`: Event handling contract
- `IEventSubscription`: Subscription information
- `IEventSubscriptionOptions`: Filtering options
- `IEventProcessingPipeline`: Pipeline processing
- `IEventMiddleware`: Middleware components for the pipeline

## Topology Integration

The EventBus uses the topology service to determine routing paths for events. This is a fundamental feature for IoT systems where physical or logical connections between devices determine where events should flow. The topology service is used to:

1. **Determine Event Routing**: When a device publishes an event, the bus needs to know which handlers should receive it based on their topology connection
2. **Apply Connection Conditions**: Connections may have conditions that determine if events flow through them
3. **Support Device Discovery**: Handlers can subscribe to events from connected devices

While the EventBus implements `ITopologyAware`, it's recommended to inject the topology service via the constructor rather than using the `SetTopologyService` method. This ensures the topology service is available from initialization and allows for proper dependency injection.

```csharp
// Preferred approach - inject in constructor
var eventBus = new EventBus(logger, topologyService);

// Alternative approach (not recommended)
var eventBus = new EventBus(logger);
eventBus.SetTopologyService(topologyService);
```

If no topology service is provided, the EventBus will still function but will be limited to direct routing (where the event source or target must be explicitly specified).

## Using the Event System

### Basic Usage

```csharp
// Create the event bus
var eventBus = new RefactoredEventBus(logger);

// Set topology service (optional)
eventBus.SetTopologyService(topologyService);

// Subscribe to events
var subscriptionId = eventBus.Subscribe(myHandler, new EventSubscriptionOptions 
{
    EventTypes = new[] { EventType.Telemetry },
    SourceIds = new[] { deviceId }
});

// Publish an event
var result = await eventBus.PublishAsync(this, myEvent);

// Unsubscribe when done
eventBus.Unsubscribe(subscriptionId);
```

### Using the Pipeline

```csharp
// Create a pipeline
var pipeline = new EventProcessingPipeline(logger);

// Add middleware
pipeline.AddMiddleware(new ErrorHandlingMiddleware(logger, errorMonitor));

// Set the pipeline in the event bus
eventBus.SetEventProcessingPipeline(pipeline);

// Events will now go through the pipeline first
```

### Creating Custom Middleware

```csharp
public class MyCustomMiddleware : IEventMiddleware
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Name => "My Custom Middleware";
    public int Order => 200;

    public bool ShouldApply(IEvent @event)
    {
        // Determine if this middleware should be applied
        return true;
    }

    public async Task<IEventProcessingResult> ProcessAsync(
        object? sender,
        IEvent @event,
        Func<object?, IEvent, CancellationToken, Task<IEventProcessingResult>> next,
        CancellationToken cancellationToken = default)
    {
        // Do something before the next middleware
        
        // Call the next middleware
        var result = await next(sender, @event, cancellationToken);
        
        // Do something after the next middleware
        
        return result;
    }
}
```

## Best Practices

1. **Subscription Management**
   - Use specific event types and source IDs for efficient filtering
   - Unsubscribe handlers when they're no longer needed

2. **Event Design**
   - Keep events small and focused
   - Include all necessary context in the event
   - Use routing data for explicit routing

3. **Error Handling**
   - Use middleware for cross-cutting error handling
   - Check publish results for errors

4. **Performance**
   - Use asynchronous event handlers for long-running operations
   - Set appropriate timeouts for event handling

## Refactoring Notes

The event system has been refactored to:

1. **Focus on Pub/Sub**: Removed recovery mechanisms and other complex functionality
2. **Decouple Topology Integration**: Cleaner integration with topology service via ITopologyAware
3. **Remove Error Handling Logic**: Error handling is now handled through separate middleware
4. **Improve Testability**: Easier to test with less complex implementation

The legacy `EventBus` class is marked as deprecated and should be replaced with `RefactoredEventBus` in all code.