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
  - State changes
  - Error events

- **Component Decoupling**
  - No direct event handler references in components
  - Components communicate exclusively through the event system
  - Publisher-subscriber pattern for all communications

- **Consistent Event Processing**
  - Standardized event handling across the system
  - Common patterns for event creation and consumption
  - Centralized error handling and recovery mechanisms

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

### 4. Error Handling Infrastructure

- **Comprehensive Error Management**
  - Standardized error representation
  - Error categorization and classification
  - Error-to-event transformation
  - Recovery orchestration

- **Error Reporting Pipeline**
  - Error aggregation and correlation
  - Error rate monitoring
  - Automatic recovery attempts
  - Error persistence for analysis

### 5. UI Integration (Planned)

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
- `StateChange`: Component state transitions
- `System`: General system events
- `Error`: Error-related events

### Event Processing Pipeline

The Event Processing Pipeline enhances the EventBus with middleware capabilities.

**Responsibilities:**
- Applying middleware to event processing
- Event transformation
- Event validation
- State change management
- Error handling

**Interfaces:**
- `IEventProcessingPipeline`: Core pipeline interface
- `IEventMiddleware`: Interface for middleware components
- `IEventProcessingResult`: Result of processing an event

**Middleware Components:**
- `EventValidationMiddleware`: Validates events before processing
- `DefaultTransformerMiddleware`: Transforms events during processing
- `StateChangeMiddleware`: Manages state change events

### Error-Event Transformation

Provides bidirectional conversion between errors and events.

**Responsibilities:**
- Converting application errors to events
- Converting events back to error objects
- Maintaining correlation context
- Publishing errors as events
- Tracking error occurrences

**Interfaces:**
- `IErrorEventTransformationService`: Core transformation interface
- `IApplicationError`: Interface for error representation
- `IErrorMonitor`: Interface for error reporting and monitoring
- `IErrorEvent`: Interface for error events

**Key Components:**
- `ComponentError`: Concrete implementation of IApplicationError
- `ErrorContextBuilder`: Utility for building rich error context information
- `ErrorEventTransformationService`: Default implementation for error transformation

### PersistenceService

Manages the storage and retrieval of component state and configuration.

**Responsibilities:**
- Subscribing to events for persistence
- Batch processing for performance
- Entity-specific handling for different data types
- In-memory caching for frequent access
- Transaction support for data consistency

**Storage Implementations:**
- `JsonStore`: File-based JSON storage for development and simple deployments
- `JsonStoreTransaction`: Transaction support for JSON store

**Key Features:**
- Transactional operations for data consistency
- Event-based persistence triggered by the EventBus
- Component property management
- Component connection storage

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

### ErrorMonitor

Central service for error tracking and management.

**Responsibilities:**
- Error reporting and collection
- Error categorization
- Error persistence
- Recovery attempt tracking
- Error rate monitoring

**Implementations:**
- Base error monitor with extensible design
- In-memory error repository for development
- (Future) Database-backed repositories for production

**Features:**
- Error correlation tracking
- Error categorization by source and severity
- Active/resolved error states
- Error rate monitoring

### IoT Device Base

Foundation for all IoT devices in the system.

**Responsibilities:**
- Lifecycle management (initialization, start, stop)
- Property management
- State tracking
- Error reporting and recovery
- Event publishing

**Key Components:**
- `IotDeviceBase`: Base class for all IoT devices
- `ComponentBase`: Foundation for all components
- `PumpDevice`: Specialized device for pump control

## Event Flow Sequence

1. Device reads sensor and updates property value
2. Property change is published to EventBus as PropertyChangedEvent
3. EventBus processes the event through the Event Processing Pipeline
4. Pipeline applies middleware (validation, transformation, etc.)
5. PersistenceService receives event and persists to storage
6. Other components receive event based on subscriptions and topology
7. Components execute business logic based on received events
8. Any resulting actions generate new events, continuing the cycle

## Event Routing Architecture

The event routing system uses a sophisticated approach to deliver events to the right components:

### Direct Routing

Events with explicit target IDs are routed directly to those targets:

1. Event contains target IDs in routing data
2. DirectEventRouter identifies matching subscriptions
3. EventBus delivers events to matched handlers

### Topology-Based Routing

Events without explicit targets use the component topology:

1. Event contains source ID but no targets
2. TopologyEventRouter queries topology connections
3. Connections define paths from source to targets
4. EventBus delivers events to connected components

### Composite Routing

Combined approach that uses both strategies:

1. CompositeEventRouter processes event
2. Event first passes through DirectEventRouter
3. Then passes through TopologyEventRouter
4. Results are combined and duplicates removed
5. EventBus delivers to final subscription list

### Conditional Routing

Topology connections can include conditions:

1. Connection defines condition expression
2. ConditionEvaluator processes condition at routing time
3. Event only flows if condition evaluates to true
4. Enables dynamic, context-sensitive routing

## Implementation Status

### Phase 1: Core Event System and Error Integration (Completed)

- ✅ Update ComponentBase to use EventBus
- ✅ Modify PersistenceService to subscribe to events
- ✅ Enhance EventBus for proper routing and subscription management
- ✅ Implement basic event types and handling
- ✅ Create Error-Event Transformation Service
- ✅ Implement bidirectional conversion between errors and events
- ✅ Add error correlation and tracking

### Phase 2: Event Processing Pipeline (Completed)

- ✅ Design and implement the Event Processing Pipeline
- ✅ Create middleware interfaces and core pipeline classes
- ✅ Implement validation middleware
- ✅ Implement transformer middleware
- ✅ Implement state change middleware
- ✅ Integrate pipeline with EventBus

### Phase 3: Advanced Routing and Topology (Completed)

- ✅ Implement topology-aware routing
- ✅ Create condition evaluation system
- ✅ Develop composite router
- ✅ Implement connection management
- ✅ Add condition-based routing

### Phase 4: IoT Device Framework (Completed)

- ✅ Create IotDeviceBase foundation
- ✅ Implement lifecycle management
- ✅ Add specialized device types (Pump)
- ✅ Integrate error handling
- ✅ Add recovery mechanisms

### Phase 5: UI Integration (Planned)

- ❌ Create SignalR bridge for real-time updates
- ❌ Implement REST API for configuration management
- ❌ Enable service control through API endpoints
- ❌ Develop web-based management interface

### Phase 6: Testing and Optimization (In Progress)

- ⚠️ Update unit tests to cover new functionality
- ⚠️ Create comprehensive integration tests
- ❌ Perform performance optimization
- ✅ Document system architecture and APIs

## Key Abstractions

### Event Interfaces

- `IEvent`: Base interface for all events
- `IPropertyChangedEvent`: Events for property changes
- `IStateChangeEvent`: Events for state transitions
- `ICommandEvent`: Events for component commands
- `ITelemetryEvent`: Events for sensor readings
- `IAlertEvent`: Events for alerts and notifications
- `ISystemEvent`: Events for system operations
- `ILifecycleEvent`: Events for component lifecycle changes
- `IErrorEvent`: Events for error reporting

### Error Interfaces

- `IApplicationError`: Base interface for errors
- `IErrorMonitor`: Interface for error reporting
- `IErrorEventTransformationService`: Interface for error-event conversion
- `IErrorRepository`: Interface for error storage

### Component Interfaces

- `IComponent`: Base interface for all components
- `IIoTDevice`: Interface for IoT devices
- `IComponentConnection`: Interface for topology connections

### Service Interfaces

- `IEventBus`: Interface for event publication and subscription
- `IEventProcessingPipeline`: Interface for event processing
- `ITopologyService`: Interface for topology management
- `IPersistenceService`: Interface for persistence operations
- `IPropertyAccessService`: Interface for property access

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

## Benefits of the Architecture

- **Improved Decoupling**: Components interact solely through events
- **Enhanced Extensibility**: New components can be added by subscribing to events
- **Consistent Architecture**: All system communication follows the same pattern
- **Centralized Control**: EventBus provides a single point for event monitoring and management
- **Flexible Topology**: Component connections can be modified dynamically
- **Improved Maintainability**: Clearer separation of concerns throughout the system
- **Enhanced Resilience**: Comprehensive error handling and recovery mechanisms