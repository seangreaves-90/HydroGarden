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