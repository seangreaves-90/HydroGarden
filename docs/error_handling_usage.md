# Error Handling and Recovery Orchestration Usage Guide

This document provides comprehensive guidance on using the HydroGarden Error Handling and Recovery Orchestration system. This system offers robust error detection, reporting, and automated recovery capabilities to ensure system resilience.

## Table of Contents

1. [Overview](#overview)
2. [Key Components](#key-components)
3. [Error Handling](#error-handling)
   - [Reporting Errors](#reporting-errors)
   - [Error Monitoring](#error-monitoring)
   - [Error Event Transformation](#error-event-transformation)
4. [Recovery Orchestration](#recovery-orchestration)
   - [Using the Recovery Orchestrator](#using-the-recovery-orchestrator)
   - [Working with ComponentError Recovery Features](#working-with-componenterror-recovery-features)
   - [Creating Custom Recovery Strategies](#creating-custom-recovery-strategies)
   - [Monitoring Recovery Attempts](#monitoring-recovery-attempts)
5. [Integration Examples](#integration-examples)
6. [Best Practices](#best-practices)
7. [Advanced Usage](#advanced-usage)
   - [Error Categorization](#error-categorization)
   - [Recovery Backoff and Retry Limiting](#recovery-backoff-and-retry-limiting)
   - [Recovery Context Management](#recovery-context-management)

## Overview

The Error Handling and Recovery Orchestration system provides a comprehensive solution for detecting, reporting, and automatically recovering from errors in the HydroGarden system. It consists of:

- **Error Monitoring**: Tracks and logs errors throughout the system
- **Error Event Transformation**: Converts errors to events for routing through the EventBus
- **Recovery Orchestration**: Coordinates recovery strategies based on error characteristics
- **ComponentError**: Enhanced error representation with recovery tracking capabilities

## Key Components

### Error Handling Components

- `IApplicationError`: Interface for representing application errors
- `ComponentError`: Enhanced error implementation with recovery tracking
- `IErrorMonitor`: Interface for reporting and monitoring errors
- `ErrorEventTransformationService`: Converts errors to events and vice versa

### Recovery Orchestration Components

- `RecoveryOrchestrator`: Coordinates error recovery using multiple strategies
- `IRecoveryStrategy`: Interface for implementing recovery strategies
- `ComponentError`: Error implementation with built-in recovery features

## Error Handling

### Reporting Errors

To report an error in your component:

```csharp
// Inject IErrorMonitor
private readonly IErrorMonitor _errorMonitor;

public MyComponent(IErrorMonitor errorMonitor)
{
    _errorMonitor = errorMonitor;
}

// Report an error
private async Task HandleFailureAsync()
{
    // Create an error with full constructor
    var error = new ComponentError(
        deviceId: _deviceId,
        errorCode: "PUMP_FAILURE",
        message: "Pump has failed to start",
        severity: ErrorSeverity.Critical,
        isRecoverable: true,
        source: ErrorSource.Device,
        isTransient: false,
        context: new Dictionary<string, object>
        {
            { "LastRunTime", DateTime.UtcNow.AddHours(-2) },
            { "PowerLevel", 85 }
        },
        exception: new Exception("Motor stalled")
    );
    
    // Or use factory methods for common scenarios
    var nonRecoverableError = ComponentError.CreateNonRecoverable(
        deviceId: _deviceId,
        errorCode: "PUMP_HARDWARE_FAILURE",
        message: "Pump hardware failure detected",
        severity: ErrorSeverity.Critical,
        source: ErrorSource.Device
    );
    
    var transientError = ComponentError.CreateTransient(
        deviceId: _deviceId,
        errorCode: "PUMP_COMMUNICATION_ERROR",
        message: "Temporary communication error with pump",
        severity: ErrorSeverity.Error,
        source: ErrorSource.Communication
    );
    
    await _errorMonitor.ReportErrorAsync(error);
}
```

### Error Monitoring

To monitor errors in the system:

```csharp
// Get active errors for a device
var activeErrors = await _errorMonitor.GetActiveErrorsForDeviceAsync(deviceId);

// Check if there are any critical errors in the system
bool hasCriticalErrors = await _errorMonitor.HasActiveErrorsAsync(ErrorSeverity.Critical);

// Get recent errors
var recentErrors = await _errorMonitor.GetRecentErrorsAsync(20);

// Get error statistics
var stats = await _errorMonitor.GetErrorStatisticsAsync(DateTimeOffset.UtcNow.AddDays(-7));
```

### Error Event Transformation

Errors are automatically converted to events by the `ErrorEventTransformationService`. This happens internally, but you can also use the service directly:

```csharp
// Inject the transformation service
private readonly IErrorEventTransformationService _transformationService;

// Convert an error to an event
var errorEvent = _transformationService.TransformErrorToEvent(error);

// Convert an event back to an error
var recoveredError = _transformationService.TransformEventToError(errorEvent);
```

## Recovery Orchestration

The Recovery Orchestration system provides streamlined error recovery capabilities through the RecoveryOrchestrator class and associated strategies.

### Using the Recovery Orchestrator

```csharp
// Create a recovery orchestrator with strategies
var recoveryOrchestrator = new RecoveryOrchestrator(
    logger,
    errorMonitor,
    new List<IRecoveryStrategy>
    {
        new RestartDeviceStrategy(logger),
        new ResetConfigurationStrategy(logger)
    });

// Attempt to recover from an error
var success = await recoveryOrchestrator.AttemptRecoveryAsync(error);

if (success)
{
    Console.WriteLine("Recovery successful");
}
else
{
    Console.WriteLine("Recovery failed");
}
```

### Working with ComponentError Recovery Features

```csharp
// Check if recovery can be attempted based on backoff interval and max attempts
if (componentError.CanAttemptRecovery())
{
    // Attempt recovery
    await recoveryOrchestrator.AttemptRecoveryAsync(componentError);
}

// Check recovery status
if (componentError.IsUnrecoverable)
{
    Console.WriteLine("Error cannot be recovered automatically");
}

// Get exponential backoff delay for next attempt
var backoffDelay = componentError.RecoveryBackoffInterval;
Console.WriteLine($"Next recovery attempt in {backoffDelay.TotalSeconds} seconds");
```

### Creating Custom Recovery Strategies

```csharp
public class CustomRecoveryStrategy : IRecoveryStrategy
{
    private readonly ILogger _logger;
    private readonly IMyService _service;
    
    public CustomRecoveryStrategy(ILogger logger, IMyService service)
    {
        _logger = logger;
        _service = service;
    }
    
    public string Name => "Custom Recovery Strategy";
    
    public bool CanRecover(IApplicationError error)
    {  
        // Determine if this strategy can handle this error type
        return error.ErrorCode == "CUSTOM_ERROR_CODE" || 
               error.ErrorCode?.StartsWith("CONFIG_") == true;
    }
    
    public async Task<bool> AttemptRecoveryAsync(IApplicationError error, CancellationToken ct = default)
    {
        try
        {
            _logger.Log($"Attempting recovery for {error.ErrorCode} using {Name}");
            
            // Implement recovery logic
            await _service.FixIssueAsync(error.DeviceId, ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.Log(ex, "Custom recovery failed");
            return false;
        }
    }
}

// Add the strategy to the orchestrator
var orchestrator = new RecoveryOrchestrator(
    logger, 
    errorMonitor,
    new List<IRecoveryStrategy>
    {
        new CustomRecoveryStrategy(logger, myService)
    });
```

### Monitoring Recovery Attempts

```csharp
// Using the error monitor to track recovery attempts
await errorMonitor.RegisterRecoveryAttemptAsync(
    deviceId,
    "DEVICE_OFFLINE",
    isSuccessful: true);

// Check device status
var activeErrors = await errorMonitor.GetActiveErrorsForDeviceAsync(deviceId);
if (activeErrors.Count > 0)
{
    Console.WriteLine($"Device has {activeErrors.Count} active errors");
    foreach (var error in activeErrors)
    {
        Console.WriteLine($"{error.ErrorCode}: {error.Message}");
        Console.WriteLine($"Recovery attempts: {(error as ComponentError)?.RecoveryAttemptCount ?? 0}");
    }
}
```

## Integration Examples

### Component with Error Handling and Recovery

```csharp
public class PumpController : HydroGardenComponentBase
{
    private readonly IErrorMonitor _errorMonitor;
    private readonly RecoveryOrchestrator _recoveryOrchestrator;
    
    public PumpController(
        IEventBus eventBus,
        IErrorMonitor errorMonitor,
        RecoveryOrchestrator recoveryOrchestrator) : base(eventBus)
    {
        _errorMonitor = errorMonitor;
        _recoveryOrchestrator = recoveryOrchestrator;
    }
    
    public async Task StartPumpAsync()
    {
        try
        {
            // Attempt to start the pump
            // If it fails, an exception is thrown
        }
        catch (Exception ex)
        {
            // Create an error using the factory method
            var error = ComponentError.CreateTransient(
                deviceId: DeviceId,
                errorCode: "PUMP_START_FAILURE",
                message: ex.Message,
                severity: ErrorSeverity.Critical,
                source: ErrorSource.Device,
                context: new Dictionary<string, object>
                {
                    { "AttemptCount", 1 },
                    { "LastSuccessful", DateTime.UtcNow.AddDays(-1) }
                },
                exception: ex
            );
            
            await _errorMonitor.ReportErrorAsync(error);
            
            // Attempt recovery if possible
            if (error.CanAttemptRecovery())
            {
                var success = await _recoveryOrchestrator.AttemptRecoveryAsync(error);
                
                if (success)
                {
                    // Retry the operation
                    await StartPumpAsync();
                }
                else
                {
                    // Propagate the exception
                    throw new DeviceOperationException("Failed to start pump after recovery attempts", ex);
                }
            }
            else
            {
                // Propagate the exception
                throw;
            }
        }
    }
}
```

### Subscribing to Error Events

```csharp
public class ErrorNotificationService
{
    private readonly IEventBus _eventBus;
    
    public ErrorNotificationService(IEventBus eventBus)
    {
        _eventBus = eventBus;
        
        // Subscribe to error events
        _eventBus.Subscribe<ErrorOccurredEvent>(HandleErrorEventAsync);
    }
    
    private async Task HandleErrorEventAsync(ErrorOccurredEvent errorEvent)
    {
        // Process the error event
        // This could send notifications, log to external systems, etc.
        
        // For critical errors, send an alert
        if (errorEvent.Severity == ErrorSeverity.Critical)
        {
            await SendAlertAsync(errorEvent);
        }
    }
    
    private async Task SendAlertAsync(ErrorOccurredEvent errorEvent)
    {
        // Send alert via email, SMS, etc.
    }
}
```

## Best Practices

1. **Use Specific Error Codes**: Define clear, specific error codes with prefixes that help categorization:
   - `DEVICE_*`: For hardware/physical device issues
   - `SERVICE_*`: For software service issues
   - `COMM_*`: For communication and networking issues
   - `EVENT_*`: For event system issues
   - `STORAGE_*`: For data persistence issues
   - `RECOVERY_*`: For issues during recovery operations

2. **Set Appropriate Severity Levels**: Use ErrorSeverity correctly to prioritize handling:
   - `Catastrophic`: System stability is at risk, immediate action required
   - `Critical`: Component needs external intervention
   - `Error`: Operation failed but component can recover
   - `Warning`: Operation can continue but attention may be needed

3. **Provide Rich Context**: Include relevant context in errors to aid in diagnosis and recovery:
   - Device state information
   - Recent values and readings
   - Connection status and history
   - Previous recovery attempts

4. **Classify Recoverability**: Correctly identify which errors can be automatically recovered:
   - Use `isRecoverable` parameter appropriately
   - Use factory methods (`CreateTransient()`, `CreateNonRecoverable()`) for common cases
   - Consider `IsTransient` flag for errors that may resolve themselves

5. **Implement Strategic Recovery**: Develop targeted recovery strategies:
   - Create specialized strategies for different error categories
   - Order strategies by priority (simplest/least disruptive first)
   - Respect backoff periods between recovery attempts

6. **Maintain Error Correlation**: Use CorrelationId to track related errors across components

7. **Utilize Exponential Backoff**: Respect the backoff mechanism to avoid overwhelming components:
   - Check `CanAttemptRecovery()` before attempting recovery
   - Call `RecordRecoveryAttempt()` to update attempt counts
   - Use `RecoveryBackoffInterval` to determine appropriate wait times

8. **Handle Unrecoverable Errors**: Have fallback plans for errors that cannot be automatically recovered:
   - Check `IsUnrecoverable` property to determine when to escalate
   - Create alerts for human intervention when needed
   - Document manual recovery procedures for operations staff

## Advanced Usage

### Error Categorization

The system provides error categorization to help with diagnosis and recovery:

```csharp
// Create an error with categorization
var error = new ComponentError(
    deviceId: deviceId,
    errorCode: "DEVICE_OFFLINE",
    message: "Device not responding",
    severity: ErrorSeverity.Error,
    isRecoverable: true,
    source: ErrorSource.Device,
    isTransient: true
);

// The error category is automatically derived from the error code prefix
// DEVICE_* → ErrorCategory.Device
// SERVICE_* → ErrorCategory.Service
// COMM_* → ErrorCategory.Communication
// EVENT_* → ErrorCategory.EventSystem
// STORAGE_* → ErrorCategory.Storage
// RECOVERY_* → ErrorCategory.Recovery

// Access the derived category
Console.WriteLine($"Error category: {error.Category}");

// Use the category for filtering or specialized handling
if (error.Category == ErrorCategory.Device)
{
    // Apply device-specific recovery processes
}
```

### Recovery Backoff and Retry Limiting

The system includes built-in support for exponential backoff and retry limiting:

```csharp
// ComponentError implements exponential backoff with a cap at 10 minutes
// The backoff formula: min(600, 2^min(attempts, 9)) seconds

// Example backoff intervals:
// Attempt 1: 2 seconds
// Attempt 2: 4 seconds
// Attempt 3: 8 seconds
// Attempt 4: 16 seconds
// And so on, capped at 600 seconds (10 minutes)

// Get the current backoff interval
var backoffInterval = componentError.RecoveryBackoffInterval;
Console.WriteLine($"Wait time before next attempt: {backoffInterval.TotalSeconds} seconds");

// Check if recovery should be attempted based on backoff and max attempts
if (componentError.CanAttemptRecovery())
{
    await recoveryOrchestrator.AttemptRecoveryAsync(componentError);
    componentError.RecordRecoveryAttempt();
}
else
{
    Console.WriteLine("Cannot attempt recovery: backoff period not elapsed or max attempts reached");
}
```

### Recovery Context Management

The system supports rich context for errors and recovery operations:

```csharp
// Create an error with context data
var contextData = new Dictionary<string, object>
{
    { "DeviceType", "TemperatureSensor" },
    { "LastReading", 24.5 },
    { "ConnectionAttempts", 3 },
    { "LastSeenTimestamp", DateTime.UtcNow.AddMinutes(-5) }
};

var error = new ComponentError(
    deviceId: deviceId,
    errorCode: "SENSOR_OFFLINE",
    message: "Temperature sensor not responding",
    severity: ErrorSeverity.Error,
    isRecoverable: true,
    source: ErrorSource.Device,
    isTransient: true,
    context: contextData
);

// The ComponentError automatically enriches the context with additional data:
// - Timestamp
// - ErrorId (CorrelationId)
// - DeviceId
// - ErrorCode
// - ErrorCategory
// - ExceptionType and InnerExceptionType if an exception is provided
// - StackTraceHash if a stack trace is available

// Access context data during recovery
public class SensorRecoveryStrategy : IRecoveryStrategy
{
    // Implementation details omitted for brevity
    
    public async Task<bool> AttemptRecoveryAsync(IApplicationError error, CancellationToken ct)
    {
        // Extract context information to guide recovery
        var deviceType = error.Context.TryGetValue("DeviceType", out var type) 
            ? type.ToString() 
            : "Unknown";
            
        var lastSeen = error.Context.TryGetValue("LastSeenTimestamp", out var timestamp)
            ? (DateTime)timestamp
            : DateTime.MinValue;
            
        // Use context to customize recovery approach
        if (deviceType == "TemperatureSensor")
        {
            // Apply temperature sensor specific recovery
        }
        
        return true;
    }
}
```

This completes the usage guide for the Error Handling and Recovery Orchestration system. For more information, refer to the XML documentation in the code or contact the development team.
