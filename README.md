# HydroGarden Automation System

## Project Overview

HydroGarden is an advanced IoT automation system designed for hydroponic gardening setups. It provides comprehensive monitoring, control, and automation of critical parameters including pH levels, nutrient dosing, temperature, and water circulation.

The system is built on a flexible, event-driven architecture that enables:
- Real-time monitoring and control of multiple hydroponics systems
- Automated responses to environmental changes
- Data collection and analysis for optimization
- Remote monitoring and management via web interface

## System Architecture

HydroGarden uses a modular, event-driven architecture centered around an EventBus that facilitates communication between components.

![HydroGarden System Architecture](docs/images/architecture-diagram.png)

### Core Components

```
┌─────────────────────┐     ┌─────────────────────┐     ┌─────────────────────┐
│  ModuleControllers  │◄────┤      EventBus       │────►│ PersistenceService  │
│  (pH, Dosing, etc.) │     │                     │     │                     │
└─────────┬───────────┘     └──────────┬──────────┘     └──────────┬──────────┘
          │                            │                           │
          │                            │                           │
          │                            ▼                           ▼
          │                 ┌─────────────────────┐     ┌─────────────────────┐
          │                 │   TopologyService   │     │       Storage       │
          │                 │                     │     │                     │
          │                 └─────────────────────┘     └─────────────────────┘
          │
          ▼
┌─────────────────────┐
│     IoT Devices     │
│  (Sensors, Pumps)   │
└─────────────────────┘
```

#### UI Integration

```
┌─────────────────────┐     ┌─────────────────────┐     ┌─────────────────────┐
│     Core System     │     │   SignalR Bridge    │     │      Web UI        │
│      (EventBus)     │────►│                     │────►│                     │
└─────────────────────┘     └─────────────────────┘     └─────────────────────┘
          ▲                                                       │
          │                                                       │
          │                                                       ▼
          │                                             ┌─────────────────────┐
          └─────────────────────────────────────────────┤   REST API Layer   │
                                                        │                     │
                                                        └─────────────────────┘
```

### Key Architectural Principles

1. **Event-Driven**: All system communication occurs through events, creating loose coupling between components
2. **Modular Design**: Components are self-contained and can be added, removed, or replaced independently
3. **Persistence**: All events and state changes are persisted for reliability and data analysis
4. **Topology-Aware**: The system understands relationships between components for intelligent event routing
5. **Real-Time**: The system provides immediate responses and updates through the event system

## Technology Stack

- **Backend**: .NET 8 with C# for core system components
- **Storage**: Flexible storage system with JSON-based implementation for development
- **Communication**: Event-based messaging with subscription model
- **UI**: Web-based interface using ASP.NET Core and SignalR for real-time updates
- **IoT Integration**: Abstraction layer for various device protocols and interfaces

## Getting Started

### Prerequisites

- .NET 8 SDK
- Visual Studio 2022 or Visual Studio Code
- Git

### Installation

1. Clone the repository:
   ```
   git clone https://github.com/your-org/hydrogarden.git
   ```

2. Build the solution:
   ```
   dotnet build
   ```

3. Run the test suite:
   ```
   dotnet test
   ```

4. Run the test console:
   ```
   dotnet run --project TestConsole
   ```

### Basic Usage

1. Create device configurations in the `config` directory
2. Start the system with `dotnet run --project HydroGarden.Service`
3. Access the web interface at `http://localhost:5000`
4. Use the dashboard to monitor and control your hydroponics setup

## Documentation

For more detailed information, see:

- [Architecture Documentation](docs/ARCHITECTURE.md)
- [API Reference](docs/API.md)
- [Component Guide](docs/COMPONENTS.md)
- [Development Guide](docs/DEVELOPMENT.md)

## License

© 2025 HydroGarden Inc. All rights reserved.