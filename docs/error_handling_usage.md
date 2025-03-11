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
   - [Creating Recovery Plans](#creating-recovery-plans)
   - [Executing Recovery](#executing-recovery)
   - [Managing Recovery Strategies](#managing-recovery-strategies)
   - [Recovery Analytics](#recovery-analytics)
5. [Integration Examples](#integration-examples)
6. [Best Practices](#best-practices)
7. [Advanced Usage](#advanced-usage)

## Overview

The Error Handling and Recovery Orchestration system provides a comprehensive solution for detecting, reporting, and automatically recovering from errors in the HydroGarden system. It consists of:

- **Error Monitoring**: Tracks and logs errors throughout the system
- **Error Event Transformation**: Converts errors to events for routing through the EventBus
- **Recovery Orchestration**: Plans and executes recovery strategies based on error characteristics
- **Error Taxonomy**: Sophisticated categorization system for precise recovery planning

## Key Components

### Error Handling Components

- `IErrorMonitor`: Interface for reporting and monitoring errors
- `ComponentErrorMonitorService`: Implementation that tracks component-specific errors
- `ErrorEventTransformationService`: Converts errors to events and vice versa
- `ComponentError`: Class representing a component-specific error

### Recovery Orchestration Components

- `IRecoveryOrchestrationService`: Main interface for recovery orchestration
- `RecoveryOrchestrationService`: Implementation that plans and executes recovery operations
- `IRecoveryStrategy`: Interface for recovery strategy implementations
- `RecoveryStrategyBase`: Base class for implementing recovery strategies
- `RecoveryPlan`: Represents a plan for recovering from errors
- `RecoveryStatus`: Contains information about recovery attempts

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
    var error = new ComponentError
    {
        ErrorCode = "PUMP_FAILURE",
        Message = "Pump has failed to start",
        DeviceId = _deviceId,
        Severity = ErrorSeverity.Critical,
        CorrelationId = Guid.NewGuid(),
        Source = "PumpController"
    };
    
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

### Creating Recovery Plans

Recovery plans can be created manually or automatically by the service:

```csharp
// Inject the recovery orchestration service
private readonly IRecoveryOrchestrationService _recoveryService;

// Create a recovery plan for an error
var plan = await _recoveryService.CreateRecoveryPlanAsync(error);

// Customize the plan if needed
plan.MaxAttemptsPerStrategy = 5;
plan.Timeout = TimeSpan.FromMinutes(10);
```

### Executing Recovery

To execute recovery operations:

```csharp
// Attempt recovery for a specific error
var status = await _recoveryService.AttemptRecoveryAsync(error);

// Execute a custom recovery plan
var status = await _recoveryService.ExecuteRecoveryPlanAsync(plan);

// Recover an entire device (all its errors)
var status = await _recoveryService.RecoverDeviceAsync(deviceId);

// Check recovery status
if (status.IsSuccessful)
{
    Console.WriteLine($"Recovery successful using {status.SuccessfulStrategy}");
}
else
{
    Console.WriteLine($"Recovery failed after {status.AttemptCount} attempts");
}
```

### Managing Recovery Strategies

To register custom recovery strategies:

```csharp
// Create a custom recovery strategy
public class MyCustomRecoveryStrategy : RecoveryStrategyBase
{
    public MyCustomRecoveryStrategy(ILogger logger) : base(logger) { }
    
    public override string Name => "CustomStrategy";
    
    public override int Priority => 50; // Lower numbers run first
    
    public override ErrorTaxonomy.RecoveryComplexity ComplexityLevel 
        => ErrorTaxonomy.RecoveryComplexity.Moderate;
    
    public override ErrorTaxonomy.RootCause[] SupportedRootCauses 
        => new[] { ErrorTaxonomy.RootCause.ConfigurationIssue };
    
    public override bool CanRecover(IApplicationError error)
    {
        // Custom logic to determine if this strategy can recover the error
        return base.CanRecover(error) && error.ErrorCode.StartsWith("CONFIG_");
    }
    
    protected override async Task<bool> ExecuteRecoveryAsync(IApplicationError error, CancellationToken ct)
    {
        // Custom recovery logic
        Logger.Log($"Executing custom recovery for {error.ErrorCode}");
        
        // Perform recovery steps
        await Task.Delay(1000, ct); // Simulated recovery operation
        
        return true; // Indicate if recovery was successful
    }
}

// Register the strategy with the service
_recoveryService.RegisterStrategy(new MyCustomRecoveryStrategy(_logger));
```

### Recovery Analytics

To track recovery operations and analytics:

```csharp
// Get recovery history for a device
var history = await _recoveryService.GetRecoveryHistoryAsync(deviceId, 20);

// Get recovery statistics 
var metrics = await _recoveryService.GetRecoveryStatisticsAsync(DateTimeOffset.UtcNow.AddDays(-7));

foreach (var entry in metrics)
{
    Console.WriteLine($"Error code: {entry.Key}");
    Console.WriteLine($"  Success rate: {entry.Value.SuccessRate}%");
    Console.WriteLine($"  Attempts: {entry.Value.TotalAttempts}");
    Console.WriteLine($"  Most successful strategy: {entry.Value.MostSuccessfulStrategy}");
    Console.WriteLine($"  Average recovery time: {entry.Value.AverageRecoveryTimeMs}ms");
}

// Check active recoveries
var activeRecoveries = await _recoveryService.GetActiveRecoveriesAsync();
bool isRecovering = _recoveryService.IsDeviceRecovering(deviceId);
```

## Integration Examples

### Component with Error Handling and Recovery

```csharp
public class PumpController : HydroGardenComponentBase
{
    private readonly IErrorMonitor _errorMonitor;
    private readonly IRecoveryOrchestrationService _recoveryService;
    
    public PumpController(
        IEventBus eventBus,
        IErrorMonitor errorMonitor,
        IRecoveryOrchestrationService recoveryService) : base(eventBus)
    {
        _errorMonitor = errorMonitor;
        _recoveryService = recoveryService;
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
            // Create and report the error
            var error = new ComponentError
            {
                ErrorCode = "PUMP_START_FAILURE",
                Message = ex.Message,
                DeviceId = DeviceId,
                Severity = ErrorSeverity.Critical,
                Exception = ex,
                Source = "PumpController"
            };
            
            await _errorMonitor.ReportErrorAsync(error);
            
            // Attempt recovery
            var status = await _recoveryService.AttemptRecoveryAsync(error);
            
            if (status.IsSuccessful)
            {
                // Retry the operation
                await StartPumpAsync();
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

1. **Use Specific Error Codes**: Define clear, specific error codes for better classification and recovery.

2. **Set Appropriate Severity Levels**: Use ErrorSeverity correctly to prioritize handling:
   - `Critical`: System cannot function, immediate attention required
   - `Error`: Significant issue affecting functionality but not system-wide
   - `Warning`: Potential issue that may need attention
   - `Information`: Non-problematic information

3. **Provide Context in Errors**: Include relevant context in errors (device info, component state, etc.).

4. **Implement Custom Recovery Strategies**: Create domain-specific recovery strategies for specialized components.

5. **Maintain Error Correlation**: Use CorrelationId to track related errors across components.

6. **Monitor Recovery Statistics**: Regularly check recovery metrics to identify recurring issues.

7. **Handle Unrecoverable Errors**: Have fallback plans for errors that cannot be automatically recovered.

8. **Set Reasonable Timeouts**: Configure appropriate timeouts for recovery operations based on complexity.

## Advanced Usage

### Custom Error Taxonomy

The system includes a sophisticated error taxonomy system. To use it effectively:

```csharp
// Create an error profile for better recovery planning
var errorProfile = ErrorTaxonomy.CreateErrorProfile(error);

// Manually set taxonomy attributes
errorProfile[ErrorTaxonomy.ROOT_CAUSE] = ErrorTaxonomy.RootCause.HardwareFailure;
errorProfile[ErrorTaxonomy.SYSTEM_IMPACT] = ErrorTaxonomy.SystemImpact.ComponentLevel;
errorProfile[ErrorTaxonomy.RECOVERY_COMPLEXITY] = ErrorTaxonomy.RecoveryComplexity.Complex;
errorProfile[ErrorTaxonomy.TIME_SENSITIVITY] = ErrorTaxonomy.TimeSensitivity.Urgent;

// Analyze a specific aspect of an error
var rootCause = ErrorTaxonomy.AnalyzeRootCause(error.ErrorCode);
```

### Circuit Breaker Integration

Recovery strategies incorporate circuit breaker patterns:

```csharp
// The circuit breaker is managed internally by the RecoveryOrchestrationService
// It will automatically track failures and open the circuit after multiple failures

// Check if a strategy is being blocked by the circuit breaker
var activeRecoveries = await _recoveryService.GetActiveRecoveriesAsync();
var blockedByCircuit = activeRecoveries.Any(r => 
    r.DeviceId == deviceId && 
    r.Error.ErrorCode == errorCode && 
    string.IsNullOrEmpty(r.CurrentStrategy));
```

### Manual Recovery Plan Execution

For more control over recovery:

```csharp
// Create a recovery plan
var plan = await _recoveryService.CreateRecoveryPlanAsync(error);

// Customize the plan
plan.Strategies.Clear();
plan.Strategies.Add(new RestartComponentStrategy(_logger));
plan.Strategies.Add(new ReinitializeConfigurationStrategy(_logger));
plan.MaxAttemptsPerStrategy = 2;
plan.Timeout = TimeSpan.FromMinutes(1);
plan.ContinueAfterSuccess = true; // Try all strategies even after one succeeds

// Execute the customized plan
var status = await _recoveryService.ExecuteRecoveryPlanAsync(plan);
```

This completes the usage guide for the Error Handling and Recovery Orchestration system. For more information, refer to the XML documentation in the code or contact the development team.
