# HydroGarden Development Guidelines

This document provides development guidelines and best practices for the HydroGarden system. For detailed architecture information, see [ARCHITECTURE.md](ARCHITECTURE.md), and for component documentation, see [COMPONENTS.md](COMPONENTS.md).

## Project Setup

### Repository Structure

The HydroGarden system is organized into the following main projects:

- **HydroGarden.Foundation.Abstractions**: Core interfaces and abstractions
- **HydroGarden.Foundation.Common**: Common implementations of core interfaces
- **HydroGarden.Foundation.Core**: Core component implementations
- **HydroGarden.Foundation.ErrorHandling**: Error management and recovery
- **HydroGarden.Logger**: Logging infrastructure
- **HydroGarden.Foundation.Tests.\***: Test projects

### Development Environment

Required tools:
- Visual Studio 2022 or later
- .NET 8.0 SDK
- Git

Recommended extensions:
- Roslynator (code analysis)
- Visual Studio IntelliCode
- Code Cleanup on Save

### Getting Started

1. Clone the repository
2. Open the solution file in Visual Studio
3. Restore NuGet packages
4. Build the solution
5. Run unit tests to verify the setup

## Coding Standards

### Naming Conventions

- **Interfaces**: Prefix with `I` (e.g., `IComponent`, `IEventBus`)
- **Event Interfaces**: Suffix with `Event` (e.g., `IPropertyChangedEvent`)
- **Abstract Classes**: Suffix with `Base` (e.g., `ComponentBase`, `IotDeviceBase`)
- **Implementation Classes**: Descriptive name without prefix/suffix (e.g., `EventBus`, `TopologyService`)
- **Extension Methods**: Suffix class with `Extensions` (e.g., `ErrorHandlingExtensions`)

### Code Organization

- Use namespaces that reflect the project structure
- Group related classes in appropriate folders
- Keep classes focused on a single responsibility
- Use regions sparingly and only for logical grouping

### Documentation

- Add XML documentation to all public members
- Include summary, param, returns, and exception comments where appropriate
- Document error conditions and edge cases
- Keep comments up-to-date with code changes

### Asynchronous Programming

- Use async/await throughout the codebase
- Follow Task-based Asynchronous Pattern (TAP)
- Avoid blocking calls in async methods
- Always include cancellation token support
- Use ConfigureAwait(false) when appropriate

### Error Handling

- Use the error handling framework for all errors
- Categorize errors appropriately with ErrorSource and ErrorCategory
- Include detailed context information for diagnostic purposes
- Design components to be resilient to errors
- Always provide meaningful error codes and messages

## Component Development

### Creating New Components

1. Determine whether the component should extend `ComponentBase` or `IotDeviceBase`
2. Implement the appropriate interfaces based on component functionality
3. Register property validators for input validation
4. Implement lifecycle methods (Initialize, Start, Stop)
5. Add proper error handling with the error monitoring system
6. Write unit tests for the component

### Component Lifecycle

Components follow a defined lifecycle:

1. **Created**: Initial state after construction
2. **Initializing**: During initialization
3. **Ready**: Initialized successfully, ready to start
4. **Running**: Active and operational
5. **Stopping**: During shutdown
6. **Error**: When an error occurs
7. **Disposed**: After resources are released

All state transitions should be properly handled, and events should be published for significant transitions.

### Property Management

- Use SetPropertyAsync for property changes to trigger events
- Validate property values with RegisterPropertyValidator
- Add metadata for all properties (especially user-configurable ones)
- Include property units and descriptions where appropriate
- Consider property persistence requirements

### Example Component Template

```csharp
public class MyComponent : IotDeviceBase
{
    public MyComponent(Guid id, string? name, IErrorMonitor errorMonitor, IEventBus? eventBus = null, ILogger? logger = null)
        : base(id, name, errorMonitor, eventBus, logger)
    {
        // Register property validators
        RegisterPropertyValidator("MyProperty", ValidateMyProperty);
    }
    
    protected override async Task OnInitializeAsync(CancellationToken ct)
    {
        // Set initial properties
        await SetPropertyAsync("MyProperty", defaultValue, 
            ConstructDefaultPropertyMetadata("MyProperty", true, true));
        
        // Additional initialization
        
        return await base.OnInitializeAsync(ct);
    }
    
    protected override Task OnStartAsync(CancellationToken ct)
    {
        // Start operation
        
        return base.OnStartAsync(ct);
    }
    
    protected override Task OnStopAsync(CancellationToken ct)
    {
        // Stop operation
        
        return base.OnStopAsync(ct);
    }
    
    private bool ValidateMyProperty(object? value, IPropertyMetadata? metadata)
    {
        // Property validation logic
        return true;
    }
    
    protected override async ValueTask DisposeAsync(bool disposing)
    {
        if (disposing)
        {
            // Clean up managed resources
        }
        
        await base.DisposeAsync(disposing);
    }
}
```

## Event System Usage

### Publishing Events

- Use the EventBus for all event publication
- Select the appropriate event type for each scenario
- Include necessary metadata and context
- Consider routing requirements for the event
- Add error handling for publication failures

### Subscribing to Events

- Use typed handlers for specific event types
- Implement proper error handling in handlers
- Keep handlers focused and lightweight
- Unsubscribe when no longer needed
- Consider subscription options for filtering

### Creating New Event Types

1. Define an interface extending IEvent
2. Add specific properties for the event type
3. Create a concrete implementation
4. Add event-specific handler interface if needed
5. Implement serialization support if the event needs persistence

## Error Handling Best Practices

### Error Creation

- Always include a descriptive message
- Use proper severity level based on impact
- Set appropriate error source and category
- Include relevant context information
- Generate consistent error codes

### Error Reporting

- Report all errors through the ErrorMonitor
- Use ReportErrorAsync for known errors
- Use ReportExceptionAsync for exceptions
- Include the component source when reporting
- Consider whether recovery should be attempted

### Error Recovery

- Implement TryRecoverAsync for recoverable components
- Design recovery strategies for common errors
- Use progressive backoff for retry attempts
- Include recovery attempt tracking
- Log recovery success or failure

## Testing

### Unit Testing

- Test each component in isolation
- Mock dependencies using interfaces
- Verify property changes and events
- Test error handling and recovery
- Cover both success and failure paths

### Integration Testing

- Test component interactions
- Verify event flow between components
- Test topology-based routing
- Verify persistence and recovery
- Test complete scenarios end-to-end

### Test Coverage

- Aim for at least 80% code coverage
- Focus on business logic and error handling
- Include edge cases and error conditions
- Test asynchronous behavior properly
- Verify component lifecycle transitions

## Debugging Tips

### Common Issues

- **Event not received**: Check subscription options and routing
- **Property not updated**: Verify property name and validate method
- **Component not initializing**: Check for errors during initialization
- **Persistence failing**: Verify storage configuration and permissions
- **Topology not working**: Check connection conditions and enabled state

### Diagnostic Techniques

- Enable detailed logging during development
- Use a memory profiler for resource usage issues
- Monitor event flow with subscription debugging
- Trace property changes with property changed events
- Check error repository for reported errors

## Performance Considerations

### Memory Management

- Dispose components properly when no longer needed
- Avoid capturing large objects in event handlers
- Use weak references for non-essential caching
- Monitor memory usage during long-running operations
- Implement IAsyncDisposable for async cleanup

### Event Handling

- Keep event handlers lightweight
- Process events asynchronously when possible
- Use event filtering to reduce unnecessary processing
- Consider batching for high-frequency events
- Implement throttling for rate-limited operations

### Persistence Optimization

- Use transactions for related changes
- Batch persistence operations when possible
- Implement caching for frequently accessed data
- Use appropriate storage backend for the scenario
- Optimize query patterns for the storage technology

## Deployment

### Configuration

- Externalize configuration in appsettings.json
- Use environment-specific settings
- Implement configuration validation
- Support runtime configuration changes
- Document configuration options

### Monitoring

- Implement health checks
- Configure appropriate logging levels
- Monitor error rates and trends
- Track component state transitions
- Implement performance metrics

### Troubleshooting

- Enable diagnostic logging in production
- Capture and store error context
- Implement traceable correlation IDs
- Create detailed error reports
- Design for supportability

## Contributing

### Pull Request Process

1. Create a feature branch from develop
2. Implement changes with appropriate tests
3. Ensure all tests pass
4. Update documentation
5. Submit a pull request for review

### Code Review Guidelines

- Verify design aligns with architecture
- Check for proper error handling
- Ensure tests cover functionality
- Verify documentation is updated
- Review performance implications

## Version Control

### Branching Strategy

- **main**: Production-ready code
- **develop**: Integration branch for development
- **feature/\***: Feature development
- **bugfix/\***: Bug fixes
- **release/\***: Release preparation

### Commit Guidelines

- Use descriptive commit messages
- Reference issue numbers when applicable
- Keep commits focused on single changes
- Ensure the code builds after each commit
- Follow conventional commit format

## Release Process

### Versioning

- Follow semantic versioning (MAJOR.MINOR.PATCH)
- Update version in all relevant places
- Maintain a changelog
- Tag releases in the repository

### Distribution

- Create release packages
- Include release notes
- Provide upgrade instructions
- Test installation process
- Validate in staging environment first