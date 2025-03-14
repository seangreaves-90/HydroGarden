# HydroGarden Foundation EventBus

1. **EventRouter Architecture**: A new dedicated EventRouter component that separates event routing logic from the EventBus, creating a cleaner separation of concerns and making the system more maintainable and testable.

2. **Error Handling and Persistence**: Completing the error handling refactoring by implementing persistence for error records.

## EventRouter Architecture

The new EventRouter architecture introduces a dedicated component for handling subscription matching and routing decisions, allowing the EventBus to focus solely on subscription management and message delivery.

### Key Components

- **IEventRouter**: Core interface for event routing services
- **BaseEventRouter**: Base implementation with common functionality
- **DirectEventRouter**: Simple router for direct matching without topology
- **TopologyEventRouter**: Advanced router that uses topology information
- **CompositeEventRouter**: Combines multiple routers for complex scenarios

### Benefits

- **Improved Separation of Concerns**: Routing logic is separate from event delivery
- **Better Testability**: Routing strategies can be tested in isolation
- **Flexible Routing**: Multiple router implementations for different scenarios
- **Cleaner Code**: EventBus implementation is simpler and more focused

## Error Persistence

The error persistence implementation provides a way to store and retrieve error records, ensuring that errors persist beyond application restarts and can be analyzed over time.

### Key Components

- **ErrorRecord**: Model for persisted error records
- **IErrorRepository**: Interface for error storage operations
- **InMemoryErrorRepository**: Simple in-memory implementation for testing
- **ErrorMonitor**: Enhanced to work with the repository

### Benefits

- **Durable Error Records**: Errors persist beyond application restarts
- **Improved Error Management**: Ability to query and analyze error history
- **Flexible Storage**: Abstraction allows for different storage implementations
- **Backward Compatibility**: Continues to work with existing error handling

## Usage

### EventRouter Registration

Add the EventRouter services to your application using the extension methods:

```csharp
// Basic setup with direct routing
services.AddDirectEventRouter();

// For topology-aware routing
services.AddTopologyEventRouter();

// For composite routing strategies
services.AddCompositeEventRouter(
    CompositeEventRouter.MatchingStrategy.Any,
    sp => new DirectEventRouter(sp.GetRequiredService<ILogger>()),
    sp => new TopologyEventRouter(
        sp.GetRequiredService<ILogger>(),
        sp.GetRequiredService<ITopologyService>(),
        sp.GetRequiredService<DirectEventRouter>())
);

// Or use the default configuration that chooses the best router based on available services
services.AddEventRoutingServices();
```

### Error Persistence Registration

Add error persistence to your application:

```csharp
// Add in-memory error repository
services.AddInMemoryErrorRepository();

// Update ErrorMonitor to use persistence
services.AddErrorPersistence();
```

## Architecture Diagram

```
┌───────────────┐     ┌───────────────┐     ┌───────────────┐
│               │     │               │     │               │
│   EventBus    │────▶│  EventRouter  │────▶│  Topology     │
│   (pub/sub)   │     │  (routing)    │     │   Service     │
│               │     │               │     │               │
└───────────────┘     └───────────────┘     └───────────────┘
```