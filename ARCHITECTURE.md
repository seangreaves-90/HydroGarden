# HydroGarden System Architecture

## Project Objective

To implement a unified event flow architecture for the HydroGarden system where all events, including property changes, flow through the central EventBus. This creates a consistent, decoupled communication mechanism that enhances maintainability and enables flexible system extension.

## Architectural Requirements

### 1. Unified Event Flow

- **All system events flow through the EventBus**
  - Property changes
  - Lifecycle events
  - Commands
  - Telemetry
  - Alerts

- **Component Decoupling**
  - No direct event handler references in components
  - Components communicate exclusively through the event system
  - Publisher-subscriber pattern for all communications

- **Consistent Event Processing**
  - Standardized event handling across the system
  - Common patterns for event creation and consumption
  - Centralized error handling and retry mechanisms

### 2. Persistence Integration

- **Event-Based Persistence**
  - PersistenceService subscribes to the EventBus
  - Events tagged for persistence are automatically stored
  - System state can be reconstructed from event history

- **Transaction Support**
  - Atomic operations for data consistency
  - Batch processing for performance optimization
  - Rollback capabilities for error scenarios

- **Data Integrity**
  - Validation of event data before persistence
  - Consistency checks during state reconstruction
  - Error handling for storage failures

### 3. Topology-Aware Routing

- **Dynamic Component Connections**
  - TopologyService manages relationships between components
  - Connections can be created, modified, and removed at runtime
  - Connection metadata provides additional routing context

- **Conditional Routing**
  - Rules determine if events flow through connections
  - Connections can be enabled/disabled based on system state
  - Complex routing logic for advanced scenarios

- **Subscription Management**
  - Components subscribe to specific event types
  - Filtering by source, event type, and custom criteria
  - Priority-based event processing

### 4. UI Integration

- **Real-Time Updates**
  - SignalR bridge for pushing updates to UI clients
  - Event-to-UI mapping for consistent representation
  - Throttling and batching for performance

- **Configuration Management**
  - UI can modify component topology and configuration
  - Changes flow through the event system for consistency
  - Validation before applying changes

- **Service Control**
  - API for module lifecycle management
  - Starting, stopping, and configuring components
  - Authentication and authorization for security

## Component Specifications

### EventBus

The central messaging system that routes all communication between components.

**Responsibilities:**
- Event routing based on subscriptions and topology
- Event transformation and validation
- Error handling and retry mechanisms
- Event prioritization and throttling

**Interfaces:**
- `IEventBus`: Core interface for subscribing and publishing
- `IEventSubscription`: Definition of subscription parameters
- `IEventRoutingData`: Metadata for routing decisions

**Event Types:**
- `PropertyChanged`: Component property value changes
- `Lifecycle`: Component state transitions (creation, initialization, etc.)
- `Command`: Requests for components to perform actions
- `Telemetry`: Sensor readings and measurements
- `Alert`: System warnings and notifications

### PersistenceService

Manages the storage and retrieval of component state and configuration.

**Responsibilities:**
- Subscribing to events for persistence
- Batch processing for performance
- Entity-specific handling for different data types
- In-memory caching for frequent access

**Storage Implementations:**
- `JsonStore`: File-based JSON storage for development and simple deployments
- (Future) Database implementations for production deployments

**Key Features:**
- Transactional operations for data consistency
- Event-based persistence triggered by the EventBus
- Component property management

### TopologyService

Manages the connections and relationships between system components.

**Responsibilities:**
- Maintaining the component connection graph
- Evaluating routing conditions
- Providing connection information to the EventBus
- Persisting topology changes

**Key Concepts:**
- `ComponentConnection`: Defines a relationship between components
- Connection conditions: Rules that determine when events flow
- Connection metadata: Additional context for routing decisions

**Operations:**
- Creating/updating/deleting connections
- Querying connections for routing decisions
- Evaluating conditions for dynamic routing

### ModuleControllers

Domain-specific controllers that manage particular aspects of the hydroponic system.

**Types:**
- pH Controller: Manages pH sensing and adjustment
- Dosing Controller: Controls nutrient delivery
- Temperature Controller: Monitors and adjusts temperature
- Pump Controller: Manages water circulation

**Common Features:**
- Event subscription for relevant data
- Business logic implementation
- Command handling for control actions
- Telemetry generation for monitoring

### UI Bridge

Connects the core system to the user interface layer.

**Components:**
- SignalR Hub: Real-time communication with web clients
- REST API: Configuration and control endpoints
- Authentication/Authorization: Security controls

**Features:**
- Event subscription for UI updates
- Command generation from user actions
- Data formatting for presentation
- Session management and user context

## Event Flow Sequence

1. Device reads sensor and updates property value
2. Property change is published to EventBus
3. EventBus checks for relevant subscriptions and connections
4. PersistenceService receives event and persists to storage
5. Other components (like ModuleControllers) receive event based on subscriptions
6. SignalR bridge forwards updates to connected UI clients
7. ModuleController executes business logic based on event
8. Any resulting actions generate new events, continuing the cycle

## Implementation Strategy

### Phase 1: Core Event System Refactoring (Completed)

- ✅ Update HydroGardenComponentBase to use EventBus
- ✅ Modify PersistenceService to subscribe to events
- ✅ Enhance EventBus for proper routing and subscription management
- ✅ Implement basic event types and handling

### Phase 2: Persistence and Topology Integration (In Progress)

- ✅ Implement specialized entity handlers for persistence
- ✅ Ensure transaction integrity during persistence
- ✅ Integrate TopologyService with EventBus
- ✅ Implement conditional routing based on connection rules
- ⚠️ Complete event filtering and transformation mechanisms
- ⚠️ Finalize error handling and recovery processes

### Phase 3: UI Integration (Planned)

- ❌ Create SignalR bridge for real-time updates
- ❌ Implement REST API for configuration management
- ❌ Enable service control through API endpoints
- ❌ Develop web-based management interface

### Phase 4: Testing and Optimization (Planned)

- ⚠️ Update unit tests to cover new functionality
- ❌ Create comprehensive integration tests
- ❌ Perform performance optimization
- ❌ Document system architecture and APIs

## Testing Strategy

### Unit Testing

- Test individual components in isolation
- Mock dependencies for controlled testing
- Verify behavior under various conditions
- Ensure error handling works as expected

### Integration Testing

- Test component interactions through the event system
- Verify end-to-end flows for common scenarios
- Test persistence and recovery mechanisms
- Validate topology-aware routing

### Performance Testing

- Measure event throughput under load
- Test system with large numbers of components
- Verify persistence performance with large datasets
- Ensure UI responsiveness with many connected clients

## Deployment Considerations

### Development Environment

- Local file-based storage with JsonStore
- In-memory event processing
- Direct console output for monitoring

### Production Environment

- Scalable storage backend (database)
- Distributed event processing
- Logging and monitoring integration
- Authentication and authorization
- Backup and disaster recovery

## Benefits of the New Architecture

- **Improved Decoupling**: Components interact solely through events
- **Enhanced Extensibility**: New components can be added by subscribing to events
- **Consistent Architecture**: All system communication follows the same pattern
- **Real-time Updates**: UI receives immediate notification of system changes
- **Centralized Control**: EventBus provides a single point for event monitoring and management
- **Flexible Topology**: Component connections can be modified dynamically
- **Improved Maintainability**: Clearer separation of concerns throughout the system