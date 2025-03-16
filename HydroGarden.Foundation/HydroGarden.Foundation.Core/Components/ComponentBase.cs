using HydroGarden.Foundation.Common.PropertyMetadata;
using HydroGarden.Foundation.Common.Events;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using HydroGarden.Foundation.Abstractions.Interfaces;
using HydroGarden.Foundation.Abstractions.Interfaces.Components;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Foundation.ErrorHandling;
using HydroGarden.Foundation.ErrorHandling.Common;
using HydroGarden.Logger.Abstractions;

namespace HydroGarden.Foundation.Core.Components
{
    /// <summary>
    /// Base class for all HydroGarden components, implementing common functionality such as property management,
    /// event handling, state management, and error handling.
    /// </summary>
    public abstract class ComponentBase : IComponent
    {
        private readonly ConcurrentDictionary<string, object> _properties = new();
        private readonly ConcurrentDictionary<string, IPropertyMetadata> _propertyMetadata = new();
        protected readonly ILogger Logger;
        protected readonly IErrorMonitor ErrorMonitor;
        protected IPropertyChangedEventHandler? PropertyChangedEventHandler;
        protected readonly IEventBus? _eventBus;
        private volatile ComponentState _state = ComponentState.Created;
        private const int MaxOptimisticRetries = 3;
        private readonly SemaphoreSlim _stateTransitionLock = new(1, 1);
        private readonly ConcurrentDictionary<string, Func<object?, IPropertyMetadata, bool>> _propertyValidators = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="ComponentBase"/> class with optional event bus integration.
        /// </summary>
        /// <param name="id">The unique identifier of the component.</param>
        /// <param name="name">The name of the component.</param>
        /// <param name="errorMonitor">The error monitoring component.</param>
        /// <param name="eventBus">Optional event bus for event publishing.</param>
        /// <param name="logger">Optional logger instance.</param>
        protected ComponentBase(
            Guid id, 
            string name, 
            IErrorMonitor errorMonitor, 
            IEventBus? eventBus = null,
            ILogger? logger = null)
        {
            Id = id;
            Name = name;
            AssemblyType = GetType().FullName ?? "UnknownType";
            Logger = logger ?? new Logger.Logging.Logger();
            ErrorMonitor = errorMonitor;
            _eventBus = eventBus;
            
            // Initialize built-in properties
            _properties[nameof(Id)] = id;
            _properties[nameof(Name)] = name;
            _properties[nameof(AssemblyType)] = AssemblyType;
            _properties[nameof(State)] = _state;
            
            // Add metadata for built-in properties
            _propertyMetadata[nameof(Id)] = ConstructDefaultPropertyMetadata(nameof(Id));
            _propertyMetadata[nameof(Name)] = ConstructDefaultPropertyMetadata(nameof(Name));
            _propertyMetadata[nameof(AssemblyType)] = ConstructDefaultPropertyMetadata(nameof(AssemblyType));
            _propertyMetadata[nameof(State)] = ConstructDefaultPropertyMetadata(nameof(State));
        }

        /// <inheritdoc/>
        public Guid Id { get; }

        /// <inheritdoc/>
        public string Name { get; }

        /// <inheritdoc/>
        public string AssemblyType { get; }

        /// <inheritdoc/>
        public ComponentState State
        {
            get => _state;
            private set
            {
                var oldState = _state;
                _state = value;
                _properties[nameof(State)] = value;
                
                // Publish state change event asynchronously
                Task.Run(async () => await PublishStateChangeEventAsync(oldState, value));
            }
        }

        /// <summary>
        /// Asynchronously attempts to transition the component to a new state.
        /// State transitions are validated to ensure they follow the allowed transition paths.
        /// </summary>
        /// <param name="newState">The desired new state.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>True if the state transition was successful, false otherwise.</returns>
        public virtual async Task<bool> TransitionToStateAsync(ComponentState newState, CancellationToken ct = default)
        {
            await _stateTransitionLock.WaitAsync(ct);
            try
            {
                if (!IsValidStateTransition(_state, newState))
                {
                    Logger.Log($"Invalid state transition attempted: {_state} -> {newState}");
                    
                    // Create error object and report it
                    var error = ErrorFactory.CreateDeviceError(
                        Id,
                        "STATE_TRANSITION_INVALID",
                        $"Invalid state transition attempted: {_state} -> {newState}",
                        ErrorSeverity.Warning,
                        null,
                        new Dictionary<string, object>
                        {
                            ["CurrentState"] = _state,
                            ["AttemptedState"] = newState,
                            ["ComponentId"] = Id,
                            ["ComponentName"] = Name
                        });
                        
                    await ErrorMonitor.ReportErrorAsync(error, ct);
                    return false;
                }

                // Set the state property which will also publish a state change event
                State = newState;
                
                return true;
            }
            catch (Exception ex)
            {
                // Create error object and report it
                var error = ErrorFactory.CreateDeviceError(
                    Id,
                    "STATE_TRANSITION_ERROR",
                    $"Error during state transition {_state} -> {newState}",
                    ErrorSeverity.Error,
                    ex,
                    new Dictionary<string, object>
                    {
                        ["CurrentState"] = _state,
                        ["AttemptedState"] = newState,
                        ["ComponentId"] = Id,
                        ["ComponentName"] = Name
                    });
                    
                await ErrorMonitor.ReportErrorAsync(error, ct);
                return false;
            }
            finally
            {
                _stateTransitionLock.Release();
            }
        }

        /// <summary>
        /// Determines if a state transition is valid.
        /// </summary>
        /// <param name="currentState">The current state.</param>
        /// <param name="newState">The proposed new state.</param>
        /// <returns>True if the transition is valid, false otherwise.</returns>
        protected virtual bool IsValidStateTransition(ComponentState currentState, ComponentState newState)
        {
            // Define valid state transitions
            return (currentState, newState) switch
            {
                // Valid transitions from Created
                (ComponentState.Created, ComponentState.Initializing) => true,
                (ComponentState.Created, ComponentState.Error) => true,
                (ComponentState.Created, ComponentState.Disposed) => true,

                // Valid transitions from Initializing
                (ComponentState.Initializing, ComponentState.Ready) => true,
                (ComponentState.Initializing, ComponentState.Error) => true,
                (ComponentState.Initializing, ComponentState.Disposed) => true,

                // Valid transitions from Ready
                (ComponentState.Ready, ComponentState.Running) => true,
                (ComponentState.Ready, ComponentState.Error) => true,
                (ComponentState.Ready, ComponentState.Stopping) => true,
                (ComponentState.Ready, ComponentState.Disposed) => true,

                // Valid transitions from Running
                (ComponentState.Running, ComponentState.Stopping) => true,
                (ComponentState.Running, ComponentState.Error) => true,
                (ComponentState.Running, ComponentState.Ready) => true,

                // Valid transitions from Stopping
                (ComponentState.Stopping, ComponentState.Ready) => true,
                (ComponentState.Stopping, ComponentState.Error) => true,
                (ComponentState.Stopping, ComponentState.Disposed) => true,

                // Valid transitions from Error
                (ComponentState.Error, ComponentState.Initializing) => true,
                (ComponentState.Error, ComponentState.Ready) => true,
                (ComponentState.Error, ComponentState.Disposed) => true,

                // No valid transitions from Disposed (terminal state)
                (ComponentState.Disposed, _) => false,

                // Allow transition to same state (no-op)
                _ => currentState == newState
            };
        }

        /// <summary>
        /// Publishes a state change event for this component.
        /// </summary>
        /// <param name="oldState">The previous state.</param>
        /// <param name="newState">The new state.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        protected virtual async Task PublishStateChangeEventAsync(ComponentState oldState, ComponentState newState)
        {
        if (_eventBus == null)
        {
        Logger.Log($"Component {Id} cannot publish state change event: no event bus configured");
        return;
        }

        try
        {
        var evt = new HydroGardenStateChangedEvent(
        Id,
        Id,
        oldState,
        newState,
        DateTimeOffset.UtcNow
        );

        await _eventBus.PublishAsync(this, evt);
        }
        catch (Exception ex)
        {
        await ErrorMonitor.ReportExceptionAsync(
                this,
                    ex,
                "STATE_EVENT_PUBLISH_FAILED",
                $"Failed to publish state change event: {ex.Message}",
                ErrorSeverity.Warning,
                ErrorSource.Service,
                new Dictionary<string, object>
                {
                    ["ComponentId"] = Id,
                    ["ComponentName"] = Name,
                    ["OldState"] = oldState.ToString(),
                    ["NewState"] = newState.ToString(),
                    ["EventType"] = "StateChange"
                });
        }
    }

        /// <inheritdoc/>
        public void SetEventHandler(IPropertyChangedEventHandler handler) => PropertyChangedEventHandler = handler;

        /// <summary>
        /// Registers a validator function for a specific property.
        /// </summary>
        /// <param name="propertyName">The name of the property to validate.</param>
        /// <param name="validator">A function that validates the property value and returns true if valid, false otherwise.</param>
        public virtual void RegisterPropertyValidator(string propertyName, Func<object?, IPropertyMetadata, bool> validator)
        {
            _propertyValidators[propertyName] = validator;
        }

        /// <summary>
        /// Removes a validator for a specific property.
        /// </summary>
        /// <param name="propertyName">The name of the property.</param>
        /// <returns>True if the validator was removed, false if no validator was registered.</returns>
        public virtual bool RemovePropertyValidator(string propertyName)
        {
            return _propertyValidators.TryRemove(propertyName, out _);
        }

        /// <summary>
        /// Validates a property value against its registered validator.
        /// </summary>
        /// <param name="propertyName">The name of the property.</param>
        /// <param name="value">The value to validate.</param>
        /// <param name="metadata">The property metadata.</param>
        /// <returns>True if the value is valid or no validator is registered, false otherwise.</returns>
        protected virtual bool ValidateProperty(string propertyName, object? value, IPropertyMetadata metadata)
        {
            if (_propertyValidators.TryGetValue(propertyName, out var validator))
            {
                return validator(value, metadata);
            }
            return true; // No validator registered means always valid
        }

        /// <inheritdoc/>
        public virtual async Task SetPropertyAsync(string name, object value, IPropertyMetadata? metadata = null)
        {
            var success = await ErrorHandlingComponentExtensions.ExecuteWithErrorHandlingAsync(
                this,
                ErrorMonitor,
                async () =>
                {
                    var oldValue = _properties.TryGetValue(name, out var existing) ? existing : default;
                    
                    if (metadata == null)
                    {
                        metadata = _propertyMetadata.GetValueOrDefault(name) ?? ConstructDefaultPropertyMetadata(name);
                    }
                    
                    // Validate property value before updating
                    if (!ValidateProperty(name, value, metadata))
                    {
                        Logger.Log($"Property validation failed for '{name}'");
                        throw new ArgumentException($"Property '{name}' validation failed", nameof(value));
                    }
                    
                    // Update reflection-based class property if it exists
                    UpdateClassProperty(name, value);
                    
                    // Update property value in dictionary
                    _properties[name] = value;

                    // Store metadata
                    _propertyMetadata[name] = new PropertyMetadata(
                        metadata.IsEditable,
                        metadata.IsVisible,
                        metadata.DisplayName,
                        metadata.Description);

                    // Publish property change event if value actually changed
                    if (!Equals(oldValue, value))
                    {
                        await PublishPropertyChangeAsync(name, value, _propertyMetadata[name], oldValue);
                    }

                    return true;
                },
                "PROPERTY_UPDATE_FAILED",
                $"Failed to update property '{name}'",
                ErrorSource.Device,
                new Dictionary<string, object>
                {
                    ["PropertyName"] = name,
                    ["PropertyType"] = value?.GetType().Name ?? "null",
                    ["ComponentId"] = Id,
                    ["ComponentName"] = Name
                });
            
            if (!success)
            {
                Logger.Log($"Failed to update property '{name}'");
            }
        }

        /// <summary>
        /// Updates a property using an optimistic concurrency approach.
        /// </summary>
        /// <typeparam name="T">The type of the property.</typeparam>
        /// <param name="name">The property name.</param>
        /// <param name="updateFunc">Function that takes the current value and returns the updated value.</param>
        /// <param name="validateBeforeUpdate">Optional: Validate the value before updating.</param>
        /// <returns>True if the update was successful, false if it failed due to concurrent modifications.</returns>
        public virtual async Task<bool> UpdatePropertyOptimisticAsync<T>(
            string name, 
            Func<T?, T> updateFunc,
            bool validateBeforeUpdate = true)
        {
            int attempts = 0;
            while (attempts < MaxOptimisticRetries)
            {
                attempts++;
                
                // Get current value
                _properties.TryGetValue(name, out var currentValueObj);
                var currentValue = currentValueObj is T typedValue ? typedValue : default;
                
                // Calculate new value
                var newValue = updateFunc(currentValue);
                
                // Get or create metadata
                var metadata = _propertyMetadata.GetValueOrDefault(name, ConstructDefaultPropertyMetadata(name));
                
                // Validate if requested
                if (validateBeforeUpdate && !ValidateProperty(name, newValue, metadata))
                {
                    Logger.Log($"Property validation failed for '{name}'");
                    return false;
                }
                
                // If property doesn't exist, try to add it
                if (currentValueObj == null)
                {
                    if (_properties.TryAdd(name, newValue!))
                    {
                        // Update class property via reflection
                        UpdateClassProperty(name, newValue);
                        
                        // Publish property change event
                        await PublishPropertyChangeAsync(name, newValue, metadata);
                        return true;
                    }
                }
                // Otherwise try to update the existing property
                else
                {
                    if (_properties.TryUpdate(name, newValue!, currentValueObj))
                    {
                        // Update class property via reflection
                        UpdateClassProperty(name, newValue);
                        
                        // Publish property change event
                        await PublishPropertyChangeAsync(name, newValue, metadata, currentValue);
                        return true;
                    }
                }

                // Wait before retrying with exponential backoff
                await Task.Delay(TimeSpan.FromMilliseconds(Math.Min(100 * Math.Pow(2, attempts - 1), 1000)));
            }

            Logger.Log($"Failed to update property {name} after {MaxOptimisticRetries} attempts due to concurrent modifications.");
            
            // Create error object and report it
            var error = ErrorFactory.CreateDeviceError(
                Id,
                "OPTIMISTIC_UPDATE_FAILED",
                $"Failed to update property {name} after {MaxOptimisticRetries} attempts due to concurrent modifications",
                ErrorSeverity.Warning,
                null,
                new Dictionary<string, object>
                {
                    ["PropertyName"] = name,
                    ["AttemptCount"] = attempts,
                    ["ComponentId"] = Id,
                    ["ComponentName"] = Name
                });
                
            await ErrorMonitor.ReportErrorAsync(error);
                
            return false;
        }

        /// <summary>
        /// Constructs the default property metadata for general HydroGarden components.
        /// </summary>
        /// <param name="name">The property name.</param>
        /// <param name="isEditable">Indicates whether the property is editable.</param>
        /// <param name="isVisible">Indicates whether the property is visible.</param>
        /// <returns>The default <see cref="IPropertyMetadata"/> for the property.</returns>
        public virtual IPropertyMetadata ConstructDefaultPropertyMetadata(string name, bool isEditable = true, bool isVisible = true)
        {
            // Use a dictionary approach for well-known properties with predefined metadata
            var knownPropertyDefaults = new Dictionary<string, (bool IsEditable, bool IsVisible, string DisplayName, string Description)>
            {
                // Core component properties
                { "State", (false, true, "Component State", "The current state of the component") },
                { "Id", (false, true, "Component ID", "The unique identifier of the component") },
                { "Name", (true, true, "Component Name", "The name of the component") },
                { "AssemblyType", (false, true, "Component Type", "The assembly type of the component") }
            };

            // If the property is in our known list, use those values
            if (knownPropertyDefaults.TryGetValue(name, out var defaults))
            {
                return new PropertyMetadata(
                    defaults.IsEditable,
                    defaults.IsVisible,
                    defaults.DisplayName,
                    defaults.Description);
            }

            // Otherwise, use the provided default values
            return new PropertyMetadata(isEditable, isVisible, name, $"Property {name}");
        }

        /// <summary>
        /// Uses reflection to update a class property value.
        /// </summary>
        /// <param name="propertyName">The name of the property to update.</param>
        /// <param name="value">The new value.</param>
        private void UpdateClassProperty(string propertyName, object value)
        {
            var type = GetType();
            while (type != null)
            {
                var property = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (property != null)
                {
                    var setter = property.GetSetMethod(true);
                    if (setter != null)
                    {
                        try
                        {
                            setter.Invoke(this, new[] { value });
                        }
                        catch (Exception ex)
                        {
                            Logger.Log($"Error updating property {propertyName} via reflection: {ex.Message}");
                        }
                        return;
                    }
                }
                type = type.BaseType;
            }
        }

        /// <inheritdoc/>
        public virtual Task<T?> GetPropertyAsync<T>(string name)
        {
            if (_properties.TryGetValue(name, out var value))
            {
                Logger.Log($"[DEBUG] GetPropertyAsync: Found '{name}' = {value} (Type: {value?.GetType()})");

                // Direct cast if possible
                if (value is T typedValue)
                {
                    return Task.FromResult<T?>(typedValue);
                }

                // Attempt conversion if direct cast fails
                try
                {
                    var convertedValue = (T)Convert.ChangeType(value, typeof(T))!;
                    Logger.Log($"[INFO] Converted '{name}' from {value?.GetType()} to {typeof(T)}: {convertedValue}");
                    return Task.FromResult<T?>(convertedValue);
                }
                catch (Exception ex)
                {
                    Logger.Log($"[WARNING] Failed to convert '{name}' value '{value}' from {value?.GetType()} to {typeof(T)}: {ex.Message}");
                }
            }
            else
            {
                Logger.Log($"[DEBUG] Property '{name}' not found");
            }

            return Task.FromResult<T?>(default);
        }

        /// <inheritdoc/>
        public virtual IPropertyMetadata? GetPropertyMetadata(string name) =>
            _propertyMetadata.GetValueOrDefault(name);

        /// <inheritdoc/>
        public virtual IDictionary<string, object> GetProperties() => _properties.ToDictionary(x => x.Key, x => x.Value);

        /// <inheritdoc/>
        public virtual IDictionary<string, IPropertyMetadata> GetAllPropertyMetadata() => _propertyMetadata.ToDictionary(x => x.Key, x => (IPropertyMetadata)x.Value);

        /// <inheritdoc/>
        public virtual async Task LoadPropertiesAsync(IDictionary<string, object> properties, IDictionary<string, IPropertyMetadata>? metadata = null)
        {
            await ErrorHandlingComponentExtensions.ExecuteWithErrorHandlingAsync(
                this,
                ErrorMonitor,
                async () =>
                {
                    _properties.Clear();
                    foreach (var (key, value) in properties) 
                    {
                        _properties[key] = value;
                    }

                    if (metadata != null)
                    {
                        _propertyMetadata.Clear();
                        foreach (var (key, value) in metadata)
                        {
                            _propertyMetadata[key] = new PropertyMetadata(
                                value.IsEditable,
                                value.IsVisible,
                                value.DisplayName,
                                value.Description
                            );
                        }
                    }
                    
                    // Ensure core properties are present
                    if (!_properties.ContainsKey(nameof(Id)))
                        _properties[nameof(Id)] = Id;
                    
                    if (!_properties.ContainsKey(nameof(Name)))
                        _properties[nameof(Name)] = Name;
                    
                    if (!_properties.ContainsKey(nameof(AssemblyType)))
                        _properties[nameof(AssemblyType)] = AssemblyType;
                    
                    if (!_properties.ContainsKey(nameof(State)))
                        _properties[nameof(State)] = _state;
                },
                "PROPERTY_LOAD_FAILED",
                "Failed to load properties",
                ErrorSource.Device,
                new Dictionary<string, object>
                {
                    ["ComponentId"] = Id,
                    ["ComponentName"] = Name,
                    ["PropertyCount"] = properties.Count,
                    ["MetadataCount"] = metadata?.Count ?? 0
                });
        }

        /// <summary>
        /// Publishes a property change event to registered event handlers
        /// </summary>
        /// <param name="name">The name of the property that changed</param>
        /// <param name="value">The new property value</param>
        /// <param name="metadata">Metadata about the property</param>
        /// <param name="oldValue">The previous property value (optional)</param>
        /// <returns>A task representing the asynchronous operation</returns>
        protected async Task PublishPropertyChangeAsync(string name, object? value, IPropertyMetadata metadata, object? oldValue = null)
        {
        var evt = new HydroGardenPropertyChangedEvent(
            Id,                               // deviceId
            Id,                               // sourceId
        name,                             // propertyName
        value?.GetType() ?? typeof(object), // propertyType
        oldValue,                         // oldValue
        value,                            // newValue
        metadata                          // metadata
        );

        // Try to publish using EventBus first if available
        if (_eventBus != null)
        {
        try
            {
        await _eventBus.PublishAsync(this, evt);
        return;
        }
        catch (Exception ex)
        {
        await ErrorMonitor.ReportExceptionAsync(
            this,
                ex,
                    "EVENT_PUBLISH_FAILED",
                    $"Failed to publish property change event through EventBus: {ex.Message}",
                    ErrorSeverity.Warning,
                    ErrorSource.Service,
                    new Dictionary<string, object>
                {
                    ["ComponentId"] = Id,
                        ["ComponentName"] = Name,
                        ["PropertyName"] = name,
                        ["EventType"] = "PropertyChanged"
                    });
            // Fall back to direct handler if available
        }
        }

        // Fall back to direct event handler if no event bus or event bus publish failed
        if (PropertyChangedEventHandler == null)
        {
            Logger.Log($"No event handler registered for component {Id}");
            return;
        }

        try
        {
            await PropertyChangedEventHandler.HandleEventAsync(this, evt);
        }
        catch (Exception ex)
        {
            await ErrorMonitor.ReportExceptionAsync(
                this,
                ex,
                "EVENT_HANDLER_FAILED",
                $"Failed to handle property change event: {ex.Message}",
                ErrorSeverity.Warning,
                ErrorSource.Service,
                new Dictionary<string, object>
                {
                    ["ComponentId"] = Id,
                    ["ComponentName"] = Name,
                    ["PropertyName"] = name,
                    ["EventType"] = "PropertyChanged"
                });
        }
    }

        /// <summary>
        /// Initializes the component asynchronously.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>True if initialization was successful, false otherwise.</returns>
        public virtual async Task<bool> InitializeAsync(CancellationToken ct = default)
        {
            return await TransitionToStateAsync(ComponentState.Initializing, ct) &&
                   await OnInitializeAsync(ct) &&
                   await TransitionToStateAsync(ComponentState.Ready, ct);
        }

        /// <summary>
        /// Override this method to implement component-specific initialization logic.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>True if initialization was successful, false otherwise.</returns>
        protected virtual Task<bool> OnInitializeAsync(CancellationToken ct = default)
        {
            return Task.FromResult(true);
        }

        /// <summary>
        /// Starts the component asynchronously.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>True if the component was started successfully, false otherwise.</returns>
        public virtual async Task<bool> StartAsync(CancellationToken ct = default)
        {
            if (State != ComponentState.Ready)
            {
                Logger.Log($"Cannot start component {Id} because it is not in Ready state. Current state: {State}");
                return false;
            }

            return await TransitionToStateAsync(ComponentState.Running, ct) &&
                   await OnStartAsync(ct);
        }

        /// <summary>
        /// Override this method to implement component-specific start logic.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>True if the component was started successfully, false otherwise.</returns>
        protected virtual Task<bool> OnStartAsync(CancellationToken ct = default)
        {
            return Task.FromResult(true);
        }

        /// <summary>
        /// Stops the component asynchronously.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>True if the component was stopped successfully, false otherwise.</returns>
        public virtual async Task<bool> StopAsync(CancellationToken ct = default)
        {
            if (State != ComponentState.Running)
            {
                Logger.Log($"Cannot stop component {Id} because it is not in Running state. Current state: {State}");
                return false;
            }

            return await TransitionToStateAsync(ComponentState.Stopping, ct) &&
                   await OnStopAsync(ct) &&
                   await TransitionToStateAsync(ComponentState.Ready, ct);
        }

        /// <summary>
        /// Override this method to implement component-specific stop logic.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>True if the component was stopped successfully, false otherwise.</returns>
        protected virtual Task<bool> OnStopAsync(CancellationToken ct = default)
        {
            return Task.FromResult(true);
        }

        /// <summary>
        /// Handles an error in the component by transitioning to the Error state
        /// and performing any component-specific error handling.
        /// </summary>
        /// <param name="error">The error to handle.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>True if the error was handled successfully, false otherwise.</returns>
        public virtual async Task<bool> HandleErrorAsync(IApplicationError error, CancellationToken ct = default)
        {
            if (State == ComponentState.Disposed)
            {
                Logger.Log($"Cannot handle error in component {Id} because it is already disposed");
                return false;
            }

            return await TransitionToStateAsync(ComponentState.Error, ct) &&
                   await OnHandleErrorAsync(error, ct);
        }

        /// <summary>
        /// Override this method to implement component-specific error handling logic.
        /// </summary>
        /// <param name="error">The error to handle.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>True if the error was handled successfully, false otherwise.</returns>
        protected virtual Task<bool> OnHandleErrorAsync(IApplicationError error, CancellationToken ct = default)
        {
            return Task.FromResult(true);
        }

        /// <summary>
        /// Attempts to recover from the Error state.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>True if recovery was successful, false otherwise.</returns>
        public virtual async Task<bool> RecoverFromErrorAsync(CancellationToken ct = default)
        {
            if (State != ComponentState.Error)
            {
                Logger.Log($"Cannot recover component {Id} because it is not in Error state. Current state: {State}");
                return false;
            }

            // Perform recovery by re-initializing
            return await OnRecoverFromErrorAsync(ct) &&
                   await InitializeAsync(ct);
        }

        /// <summary>
        /// Override this method to implement component-specific error recovery logic.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>True if recovery preparation was successful, false otherwise.</returns>
        protected virtual Task<bool> OnRecoverFromErrorAsync(CancellationToken ct = default)
        {
            return Task.FromResult(true);
        }

        /// <inheritdoc/>
        public virtual async void Dispose()
        {
            // Try to transition to disposed state
            await TransitionToStateAsync(ComponentState.Disposed);
            
            // Clean up resources
            _stateTransitionLock.Dispose();
            PropertyChangedEventHandler = null;
            
            // Call component-specific disposal logic
            OnDispose();
            
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Override this method to implement component-specific disposal logic.
        /// </summary>
        protected virtual void OnDispose()
        {
            // No default implementation
        }
    }
}
