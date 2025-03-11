using System.Reflection;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling.RecoveryStrategy;
using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Foundation.ErrorHandling.Common;
using HydroGarden.Foundation.ErrorHandling.Exceptions;
using HydroGarden.Foundation.ErrorHandling.Interfaces;
using HydroGarden.Logger.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using CircuitBreakerMiddlewareNS = HydroGarden.Foundation.Common.Events.Pipeline.Middleware;
using IResiliencePolicy = HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling.RecoveryStrategy.IResiliencePolicy;

namespace HydroGarden.Foundation.ErrorHandling.RecoveryStrategy;

/// <summary>
/// A recovery strategy that attempts to reset open circuit breakers.
/// This strategy is designed to recover from failures caused by the circuit breaker pattern
/// when the underlying system issues have been resolved.
/// </summary>
public class CircuitBreakerRecoveryStrategy(ILogger logger, IServiceProvider serviceProvider) 
    : RecoveryStrategyBase(logger)
{
    private readonly IServiceProvider _serviceProvider = serviceProvider 
        ?? throw new ArgumentNullException(nameof(serviceProvider));

    /// <summary>
    /// Gets the name of this recovery strategy.
    /// </summary>
    public override string Name => "Circuit Breaker Reset";

    /// <summary>
    /// Lower priority than basic strategies.
    /// </summary>
    public override int Priority => 20;
    
    /// <summary>
    /// This strategy handles simple recovery scenarios.
    /// </summary>
    public override ErrorTaxonomy.RecoveryComplexity ComplexityLevel =>
        ErrorTaxonomy.RecoveryComplexity.Simple;
        
    /// <summary>
    /// Root causes this strategy can address.
    /// </summary>
    public override ErrorTaxonomy.RootCause[] SupportedRootCauses => 
    [
        ErrorTaxonomy.RootCause.CircuitBreakerOpen,
        ErrorTaxonomy.RootCause.RetryExhaustion,
        ErrorTaxonomy.RootCause.RecoveryFailure
    ];

    /// <summary>
    /// Determines if this strategy can recover from the specified error.
    /// </summary>
    public override bool CanRecover(IApplicationError? error)
    {
        if (!base.CanRecover(error) || error == null)
            return false;
            
        // Handle specific circuit breaker error codes
        return error.ErrorCode == ErrorCodes.Recovery.CIRCUIT_OPEN ||
               error.Exception is CircuitBreakerOpenException;
    }
    
    /// <summary>
    /// Attempts to reset circuit breakers to recover from the error.
    /// </summary>
    protected override async Task<bool> ExecuteRecoveryAsync(IApplicationError? error, CancellationToken ct)
    {
        if (error == null)
            return false;
            
        try
        {
            Logger.Log($"Attempting to reset circuit breakers for device {error.DeviceId}");
            
            // Get circuit breaker details from error context
            string? circuitBreakerType = null;
            string? eventType = null;
            string? serviceKey = null;
            
            if (error.Context != null)
            {
                if (error.Context.TryGetValue("CircuitBreakerType", out var cbTypeObj) && cbTypeObj is string cbType)
                {
                    circuitBreakerType = cbType;
                }
                
                if (error.Context.TryGetValue("EventType", out var evtTypeObj) && evtTypeObj is string evtType)
                {
                    eventType = evtType;
                }
                
                if (error.Context.TryGetValue("ServiceKey", out var serviceKeyObj) && serviceKeyObj is string svcKey)
                {
                    serviceKey = svcKey;
                }
                else
                {
                    Logger.Log("Service key not found in error context");
                }
            }
            
            // Check if we're dealing with an event pipeline circuit breaker
            if (circuitBreakerType == "EventPipeline" || error.Exception is CircuitBreakerOpenException)
            {
                Logger.Log("Attempting to reset event pipeline circuit breaker");
                bool eventPipelineReset = await ResetEventPipelineCircuitBreakerAsync(eventType);
                
                if (eventPipelineReset)
                {
                    Logger.Log("Successfully reset event pipeline circuit breaker");
                    return true;
                }
            }
            
            // Check for resilience policy circuit breakers
            if (circuitBreakerType == "ResiliencePolicy" || circuitBreakerType == null)
            {
                Logger.Log("Attempting to reset resilience policy circuit breakers");
                bool resiliencePolicyReset = await ResetResiliencePolicyCircuitBreakersAsync(error.DeviceId);
                
                if (resiliencePolicyReset)
                {
                    Logger.Log("Successfully reset resilience policy circuit breakers");
                    return true;
                }
            }
            
            // Try a general circuit breaker reset if specific resets didn't work
            if (circuitBreakerType == null)
            {
                Logger.Log("Attempting general circuit breaker reset");
                bool generalReset = await ResetAllCircuitBreakersAsync();
                
                if (generalReset)
                {
                    Logger.Log("Successfully reset at least one circuit breaker");
                    return true;
                }
            }
            
            Logger.Log("Failed to reset any circuit breakers");
            return false;
        }
        catch (Exception ex)
        {
            Logger.Log(ex, $"Error during circuit breaker recovery for device {error.DeviceId}");
            return false;
        }
    }
    
    /// <summary>
    /// Resets circuit breakers in the event processing pipeline.
    /// </summary>
    private async Task<bool> ResetEventPipelineCircuitBreakerAsync(string? eventType)
    {
        try
        {
            // Try to get the circuit breaker middleware from the service provider
            var circuitBreaker = _serviceProvider.GetService<ICircuitBreakerMiddleware>();
            if (circuitBreaker == null)
            {
                // Try the real implementation
                circuitBreaker = _serviceProvider.GetService<ICircuitBreakerMiddleware?>();
            }
            
            if (circuitBreaker == null)
            {
                Logger.Log("CircuitBreakerMiddleware not found in service provider");
                
                // Try to find it by searching through all registered services
                var fieldInfo = _serviceProvider.GetType().GetField("_factory", BindingFlags.Instance | BindingFlags.NonPublic);
                if (fieldInfo != null)
                {
                    var factory = fieldInfo.GetValue(_serviceProvider);
                    var middlewareField = factory?.GetType().GetField("_middleware", BindingFlags.Instance | BindingFlags.NonPublic);
                    
                    if (middlewareField != null)
                    {
                        var allMiddleware = middlewareField.GetValue(factory) as IEnumerable<object>;
                        circuitBreaker = (ICircuitBreakerMiddleware?)(allMiddleware?.FirstOrDefault(m => 
                            m is CircuitBreakerMiddlewareNS.CircuitBreakerMiddleware) 
                                as CircuitBreakerMiddlewareNS.CircuitBreakerMiddleware);
                    }
                }
                
                if (circuitBreaker == null)
                {
                    Logger.Log("Failed to find CircuitBreakerMiddleware instance");
                    return false;
                }
            }
            
            // Reset specific event type circuit if provided
            if (!string.IsNullOrEmpty(eventType) && Enum.TryParse<EventType>(eventType, true, out var parsedEventType))
            {
                var stateVal = circuitBreaker.GetCircuitState(parsedEventType.ToString());
                var currentState = stateVal.ToString();
                
                // In test cases, state is returned as an enum, in real implementation as a string
                if (currentState.Contains("Open"))
                {
                    Logger.Log($"Resetting circuit for event type {eventType} (was {currentState})");
                    circuitBreaker.ResetCircuit(parsedEventType.ToString());
                    
                    // Check if state changed
                    var newState = circuitBreaker.GetCircuitState(parsedEventType.ToString()).ToString();
                    return !newState.Contains("Open") || newState.Contains("HalfOpen");
                }
                
                Logger.Log($"Circuit for event type {eventType} is already in {currentState} state");
                return false;
            }
            
            // If no specific event type, try to reset common event types
            bool anyReset = false;
            foreach (EventType commonType in Enum.GetValues(typeof(EventType)))
            {
                var stateVal = circuitBreaker.GetCircuitState(commonType.ToString());
                var state = stateVal.ToString();
                
                if (state.Contains("Open"))
                {
                    Logger.Log($"Resetting circuit for event type {commonType} (was {state})");
                    circuitBreaker.ResetCircuit(commonType.ToString());
                    
                    // Check if state changed
                    var newState = circuitBreaker.GetCircuitState(commonType.ToString()).ToString();
                    anyReset = !newState.Contains("Open") || newState.Contains("HalfOpen");
                }
            }
            
            return anyReset;
        }
        catch (Exception ex)
        {
            Logger.Log(ex, "Error resetting event pipeline circuit breaker");
            return false;
        }
        
        // Task is complete synchronously, but method signature requires Task<bool>
    }
    
    /// <summary>
    /// Resets circuit breakers in resilience policies for a device.
    /// </summary>
    private async Task<bool> ResetResiliencePolicyCircuitBreakersAsync(Guid deviceId)
    {
        try
        {
            // Try to get resilience policy manager
            var policyManager = _serviceProvider.GetService<IResiliencePolicy>();
            if (policyManager == null)
            {
                Logger.Log("IResiliencePolicy not found in service provider");
                return false;
            }
            
            // Reset circuit breakers for the device
            await policyManager.ResetCircuitBreakersAsync(deviceId.ToString(), CancellationToken.None);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Log(ex, "Error resetting resilience policy circuit breakers");
            return false;
        }
    }
    
    /// <summary>
    /// Attempts to reset all circuit breakers in the system.
    /// </summary>
    private async Task<bool> ResetAllCircuitBreakersAsync()
    {
        bool eventPipelineReset = await ResetEventPipelineCircuitBreakerAsync(null);
        
        // Try to find and reset other circuit breaker implementations
        var allServices = _serviceProvider.GetServices<object>();
        bool otherReset = false;
        
        foreach (var service in allServices)
        {
            try
            {
                // Look for any methods that might reset circuit breakers
                var resetMethods = service.GetType()
                    .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => (m.Name.Contains("Reset") && m.Name.Contains("Circuit")) || 
                                m.Name == "ResetCircuit" || 
                                m.Name == "ResetBreaker");
                                
                foreach (var method in resetMethods)
                {
                    if (method.GetParameters().Length == 0)
                    {
                        // No parameters needed
                        method.Invoke(service, null);
                        otherReset = true;
                        Logger.Log($"Reset circuit breaker via {service.GetType().Name}.{method.Name}()");
                    }
                    else if (method.GetParameters().Length == 1 && 
                            method.GetParameters()[0].ParameterType == typeof(string))
                    {
                        // Try with some common names
                        foreach (var name in (List<string>)["default", "main", "global", "system"])
                        {
                            method.Invoke(service, new object[] { name });
                        }
                        otherReset = true;
                        Logger.Log($"Reset circuit breaker via {service.GetType().Name}.{method.Name}(string)");
                    }
                }
            }
            catch
            {
                // Ignore errors in reflection
            }
        }
        
        return eventPipelineReset || otherReset;
    }
}