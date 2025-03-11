# HydroGarden System Components

This document provides detailed information about the key components in the HydroGarden system and how they interact.

## Core Components

### HydroGardenComponentBase

The foundation class for all components in the system.

**Features:**
- Property management with change tracking
- Event publication for property changes
- Lifecycle state management
- Integration with the event system

**Derived Components:**
- IoT devices (sensors, pumps, etc.)
- Controllers (pH, dosing, etc.)
- Services (persistence, topology, etc.)

**Example Usage:**
```csharp
public class pHSensor : IoTDeviceBase
{
    private double _currentValue;
    
    public pHSensor(Guid id, string name, IHydroGardenLogger logger = null)
        : base(id, name, logger)
    {
    }
    
    public async Task UpdateReadingAsync(double value)
    {
        _currentValue = value;
        await SetPropertyAsync("CurrentValue", value);
    }
}
```

### EventBus

The central messaging system that facilitates communication between components.

**Features:**
- Subscription management
- Event routing and delivery
- Error handling and retry mechanisms
- Event filtering and prioritization
- Integration with Event Processing Pipeline

**Main Interfaces:**
- `IEventBus`: Core functionality for subscribing and publishing
- `IEventSubscription`: Defines subscription parameters
- `IEventRoutingData`: Contains metadata for routing decisions

**Event Types:**
- `PropertyChanged`: Component property value changes
- `Lifecycle`: Component state transitions
- `Command`: Action requests
- `Telemetry`: Sensor readings
- `Alert`: System warnings

**Example Usage:**
```csharp
// Subscribe to pH changes
var options = new EventSubscriptionOptions
{
    EventTypes = new[] { EventType.PropertyChanged },
    SourceIds = new[] { pHSensorId },
    Filter = evt => evt is IHydroGardenPropertyChangedEvent propEvt && 
                     propEvt.PropertyName == "CurrentValue"
};

eventBus.Subscribe(pHController, options);

// Publish a command
var command = new CommandEvent(
    pumpId,
    "StartPump",
    new Dictionary<string, object> { { "Duration", TimeSpan.FromMinutes(5) } }
);

await eventBus.PublishAsync(this, command);
```

### Event Processing Pipeline

New component added in Phase 2 that enhances the EventBus with middleware capabilities.

**Features:**
- Middleware-based event processing
- Advanced error handling and recovery
- Circuit breaking for preventing cascading failures
- Dead letter queue for managing failed events
- Logging and monitoring

**Main Interfaces:**
- `IEventProcessingPipeline`: Core pipeline functionality
- `IEventMiddleware`: Interface for creating middleware components
- `IEventProcessingResult`: Result of processing an event

**Middleware Components:**
- `LoggingMiddleware`: Logs event processing with configurable detail levels
- `RetryMiddleware`: Implements exponential backoff retry policy with jitter
- `CircuitBreakerMiddleware`: Prevents cascading failures with circuit breaking pattern
- `DeadLetterQueueMiddleware`: Captures permanently failed events for analysis

**Example Usage:**
```csharp
// Configure pipeline with fluent builder API
var pipeline = new EventPipelineBuilder(logger)
    .AddLogging(LoggingMiddleware.LoggingLevel.Detailed)
    .AddCircuitBreaker(failureThreshold: 5, resetTimeout: TimeSpan.FromMinutes(1))
    .AddRetry(maxRetries: 3, initialDelay: TimeSpan.FromSeconds(1))
    .AddDeadLetterQueue()
    .Build();

// Attach to EventBus
eventBus.SetEventProcessingPipeline(pipeline);
```

### Error-Event Transformation Service

New component added in Phase 1 that handles bidirectional conversion between errors and events.

**Features:**
- Error to event conversion
- Event to error conversion
- Correlation tracking
- Error publishing as events

**Main Interfaces:**
- `IErrorEventTransformationService`: Core transformation functionality
- `ErrorEvent`: Model representing an error that can be converted to an event
- `RecoveryEvent`: Model representing a recovery attempt

**Event Classes:**
- `ErrorOccurredEvent`: Event published when an error occurs
- `RecoveryAttemptedEvent`: Event published when a recovery is attempted

**Example Usage:**
```csharp
// Convert an error to an event
var errorEvent = _transformationService.TransformErrorToEvent(applicationError);

// Publish an error as an event
await _transformationService.PublishErrorAsEventAsync(applicationError);

// Publish a recovery attempt
await _transformationService.PublishRecoveryAsEventAsync(
    deviceId,
    "DEVICE_OFFLINE",
    isSuccessful: true,
    message: "Device reconnected successfully",
    correlationId: errorEvent.CorrelationId);
```

### PersistenceService

Manages storage and retrieval of component state and configuration.

**Features:**
- Event subscription for persistence
- Batch processing for performance
- Entity-specific handling
- Caching for frequent access

**Storage Implementations:**
- `JsonStore`: File-based JSON storage
- (Future) Database implementations

**Key Interfaces:**
- `IPersistenceService`: Main service interface
- `IStore`: Storage abstraction
- `IStoreTransaction`: Transactional operations

**Example Usage:**
```csharp
// Register a device with persistence
await persistenceService.AddOrUpdateAsync(pHSensor);

// Query a stored property
double lastpH = await persistenceService.GetPropertyAsync<double>(pHSensorId, "CurrentValue");
```

### TopologyService

Manages connections and relationships between system components.

**Features:**
- Component connection management
- Condition evaluation for routing
- Dynamic topology modifications
- Connection persistence

**Key Interfaces:**
- `ITopologyService`: Main service interface
- `IComponentConnection`: Defines component relationships

**Example Usage:**
```csharp
// Create a connection from pH sensor to controller
var connection = new ComponentConnection
{
    SourceId = pHSensorId,
    TargetId = pHControllerId,
    IsEnabled = true,
    ConnectionType = "Sensor-Controller"
};

await topologyService.CreateConnectionAsync(connection);

// Get connections for a component
var sensorConnections = await topologyService.GetConnectionsForSourceAsync(pHSensorId);
```

## Recovery Orchestration Service

New component added in Phase 3 that orchestrates error recovery operations across the system.

**Features:**
- Coordinates recovery efforts using multiple strategies
- Creates recovery plans based on error characteristics
- Tracks recovery state and history
- Provides analytics on recovery success rates
- Integrates with the Error-Event transformation service

**Main Interfaces:**
- `IRecoveryOrchestrationService`: Core recovery orchestration functionality
- `RecoveryPlan`: Defines a plan for recovering from errors
- `RecoveryStatus`: Reports outcome of recovery operations
- `RecoveryMetrics`: Provides analytics about recovery effectiveness

**Recovery Models:**
- `ErrorTaxonomy`: Sophisticated error categorization system
- `RecoveryRecord`: Historical record of recovery attempts
- `ActiveRecoveryOperation`: Represents a recovery in progress

**Recovery Strategies:**
- `RestartComponentStrategy`: Recovers devices through restart cycles
- `ReinitializeConfigurationStrategy`: Resets device configuration
- Additional custom strategies can be implemented and registered

**Example Usage:**
```csharp
// Attempt to recover from an error
var recoveryStatus = await recoveryOrchestrationService.AttemptRecoveryAsync(error);

if (recoveryStatus.IsSuccessful)
{
    logger.Log($"Recovery successful using strategy: {recoveryStatus.SuccessfulStrategy}");
}
else
{
    logger.Log("All recovery strategies failed");
}

// Get recovery statistics
var metrics = await recoveryOrchestrationService.GetRecoveryStatisticsAsync(DateTime.UtcNow.AddDays(-7));
foreach (var (errorCode, metric) in metrics)
{
    logger.Log($"Error {errorCode}: {metric.SuccessRate}% success rate");
}
```

## IoT Devices

The system supports various IoT devices for monitoring and controlling hydroponics systems.

### Sensor Devices

- pH Sensors
- EC/TDS Sensors
- Temperature Sensors
- Water Level Sensors
- Dissolved Oxygen Sensors

**Common Features:**
- Periodic readings
- Calibration support
- Value range validation
- Alert generation for out-of-range values

### Actuator Devices

- Pumps (Water, Nutrient)
- Valves
- Heaters/Chillers
- Lights
- Mixers

**Common Features:**
- On/off control
- Variable speed/intensity (where applicable)
- Duty cycle management
- Runtime tracking
- Failure detection

## Controllers

Domain-specific controllers that implement the business logic for different aspects of the system.

### pH Controller

Monitors and adjusts the pH level of the nutrient solution.

**Features:**
- pH target range configuration
- Adjustment scheduling
- pH up/down pump control
- Stabilization detection

### Dosing Controller

Manages nutrient delivery to the system.

**Features:**
- Nutrient recipes
- Dosing schedules
- EC/TDS targeting
- Automatic adjustments

### Temperature Controller

Regulates the temperature of the nutrient solution and growing environment.

**Features:**
- Temperature range configuration
- Heater/chiller control
- Thermal stabilization
- Energy efficiency optimization

### Pump Controller

Manages water circulation and drainage.

**Features:**
- Pump scheduling
- Flow rate monitoring
- Flood/drain cycles
- Failure detection and handling

## UI Components

### SignalR Bridge

Provides real-time updates to connected UI clients.

**Features:**
- Event subscription
- Client connection management
- Update throttling and batching
- Connection recovery

### REST API

Enables configuration and control of the system.

**Features:**
- Component management
- Configuration endpoints
- Status reporting
- Authentication and authorization

### Web Dashboard

User interface for monitoring and controlling the hydroponic system.

**Features:**
- Real-time status display
- Historical data visualization
- Control panel for manual operations
- Configuration interface
- Alerts and notifications

## Middleware Components

New section added for Phase 2 middleware components that can be added to the Event Processing Pipeline.

### LoggingMiddleware

Logs event processing with configurable detail levels.

**Features:**
- Multiple logging levels (Basic, Detailed, Diagnostic)
- Performance timing for event processing
- Event context logging
- Error logging

**Configuration Options:**
- Logging level
- Custom logger implementation
- Context enrichment

### RetryMiddleware

Implements retry policies with exponential backoff.

**Features:**
- Configurable retry limits
- Exponential backoff with jitter
- Per-event retry state tracking
- Failure reporting

**Configuration Options:**
- Maximum retry count
- Initial delay
- Backoff multiplier
- Jitter enabled/disabled

### CircuitBreakerMiddleware

Prevents cascading failures with the circuit breaker pattern.

**Features:**
- Multiple circuit states (Closed, Open, Half-Open)
- Per-event type circuit tracking
- Automatic recovery attempts
- Manual reset capability

**Configuration Options:**
- Failure threshold
- Reset timeout
- Circuit state change notifications

### DeadLetterQueueMiddleware

Captures permanently failed events for later analysis and processing.

**Features:**
- In-memory storage of failed events
- Event metadata enrichment
- Configurable retention
- Queue capacity management

**Configuration Options:**
- Queue capacity
- Retention period
- Cleanup frequency