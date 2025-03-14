# HydroGarden Error Handling System

## Overview

The HydroGarden Error Handling System provides a comprehensive framework for managing, reporting, and processing errors throughout the application. It's designed to be lightweight, consistent, and extensible.

## Key Features

- **Simplified Architecture**: Focused on core error handling functionality without recovery strategies or complex mechanisms
- **Consistent Error Representation**: Standard format for representing errors with rich context
- **Domain-Specific Exceptions**: Pre-defined exception types for common error scenarios
- **Event Integration**: Seamless conversion of errors to events and vice versa
- **Error Monitoring**: Centralized tracking of active errors with query capabilities
- **Extensibility**: Easy to extend for domain-specific error types

## Core Components

### Error Representation

- `IApplicationError`: Core interface for representing errors
- `ComponentError`: Default implementation of `IApplicationError`
- `ErrorFactory`: Factory for creating different types of errors
- `ErrorContextBuilder`: Builder for creating rich error context

### Error Monitoring

- `IErrorMonitor`: Interface for reporting and querying errors
- `ErrorMonitor`: Default implementation of `IErrorMonitor`

### Error-to-Event Transformation

- `IErrorEvent`: Interface for error events
- `ErrorEvent`: Default implementation of `IErrorEvent`
- `IErrorEventTransformationService`: Interface for bidirectional error/event conversion
- `ErrorEventTransformationService`: Default implementation of transformation service

### Exception Hierarchy

- `ApplicationException`: Base class for all application-specific exceptions
- Domain-specific exceptions:
  - Device exceptions (initialization, communication, hardware, etc.)
  - Service exceptions (initialization, timeout, configuration, etc.)
  - Communication exceptions (connection, protocol, timeout, etc.)
  - Storage exceptions (read, write, transaction, corruption, etc.)
  - Event exceptions (publication, subscription, handling, routing, etc.)

## Usage Examples

### Reporting an Error

```csharp
// Using the error monitor directly
var error = ErrorFactory.CreateDeviceError(
    deviceId: Guid.Parse("00000000-0000-0000-0000-000000000001"),
    errorCode: ErrorCodes.Device.SENSOR_MALFUNCTION,
    message: "Temperature sensor reading out of expected range",
    severity: ErrorSeverity.Error);
    
await errorMonitor.ReportErrorAsync(error);

// Using extension methods
await errorMonitor.ReportDeviceErrorAsync(
    deviceId: Guid.Parse("00000000-0000-0000-0000-000000000001"),
    errorCode: ErrorCodes.Device.SENSOR_MALFUNCTION,
    message: "Temperature sensor reading out of expected range",
    severity: ErrorSeverity.Error);
```

### Throwing Domain-Specific Exceptions

```csharp
// Device initialization exception
throw new DeviceInitializationException(
    message: "Failed to initialize temperature sensor",
    deviceId: deviceId,
    innerException: innerException);

// Service configuration exception
throw new ServiceConfigurationException(
    message: "Invalid configuration: Missing sensor type",
    serviceName: "TemperatureSensorService",
    configParameter: "SensorType");
```

### Converting Exceptions to Errors

```csharp
try
{
    // Some operation that might throw
    await InitializeDeviceAsync(deviceId);
}
catch (Exception ex)
{
    // Convert exception to error and report it
    var error = ErrorFactory.FromException(ex, deviceId);
    await errorMonitor.ReportErrorAsync(error);
}
```

### Handling Error Events

```csharp
// Subscribe to error events
var subscriptionId = eventBus.SubscribeToErrors(errorEventHandler);

// Subscribe to critical error events only
var criticalSubscriptionId = eventBus.SubscribeToErrorsBySeverity(
    errorEventHandler, 
    ErrorSeverity.Critical);
    
// Create an error event handler
public class MyErrorEventHandler : IEventHandler
{
    public async Task HandleEventAsync<T>(object? sender, T evt, CancellationToken ct = default) 
        where T : IEvent
    {
        var errorData = ((IEvent)evt).ExtractErrorData();
        if (errorData != null)
        {
            // Process error event
            Console.WriteLine($"Error: {errorData.ErrorCode} - {errorData.Message}");
        }
    }
    
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
```

## Best Practices

1. **Use Domain-Specific Exceptions**: Throw the most specific exception type for your error scenario.
2. **Include Diagnostic Context**: Always include relevant context information when reporting errors.
3. **Consistent Error Codes**: Use the predefined error codes from `ErrorCodes` or follow the same pattern for custom codes.
4. **Appropriate Severity Levels**: Choose the right severity level based on the impact of the error.
5. **Error Event Handling**: Subscribe to error events for cross-component error monitoring.

## Error Severity Guidelines

- **Warning**: Operation can continue but might need attention
- **Error**: Operation failed but component can recover
- **Critical**: Component needs external intervention
- **Catastrophic**: System stability is at risk

## Adding New Error Types

To add a new domain-specific error type:

1. Add error codes to `ErrorCodes` class
2. Create specific exception classes inheriting from `ApplicationException`
3. Add factory methods to `ErrorFactory` if needed
4. Add extension methods to `ErrorHandlingExtensions` for convenience

## Integration with Other Components

The error handling system integrates with:

- **Event System**: Errors can be published as events using the `IErrorEventTransformationService`
- **Logging**: Errors are automatically logged when reported
- **Persistence**: Error events can be persisted using the event system's persistence capabilities
