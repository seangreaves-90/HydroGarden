using HydroGarden.Foundation.Abstractions.Interfaces;
using HydroGarden.Foundation.Abstractions.Interfaces.Components;
using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Foundation.Abstractions.Interfaces.Logging;
using HydroGarden.Foundation.Abstractions.Interfaces.Services;
using HydroGarden.Foundation.Common.Logging;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace HydroGarden.Foundation.Core.Services
{
    public class PersistenceService : IPersistenceService, IPropertyChangedEventHandler
    {
        private readonly IStore _store;
        private readonly IEventBus _eventBus;
        private readonly ILogger _logger;
        private readonly Dictionary<Guid, Dictionary<string, object>> _deviceProperties;
        // Add a new field to track metadata for all devices
        private readonly Dictionary<Guid, Dictionary<string, IPropertyMetadata>> _deviceMetadata;
        private readonly Channel<IPropertyChangedEvent> _eventChannel;
        private readonly CancellationTokenSource _processingCts = new();
        private readonly Task _processingTask;
        private readonly SemaphoreSlim _transactionLock = new(1, 1);
        private readonly TimeSpan _batchInterval;
        private bool _isDisposed;

        public bool IsBatchProcessingEnabled { get; set; } = true;
        public bool ForceTransactionCreation { get; set; } = false;
        public IPropertyChangedEvent? TestEvent { get; set; }

        public PersistenceService(IStore store, IEventBus eventBus)
            : this(store, eventBus, new Logger(), TimeSpan.FromSeconds(5))
        {
        }

        public PersistenceService(IStore store, IEventBus eventBus, ILogger logger, TimeSpan? batchInterval = null)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _batchInterval = batchInterval ?? TimeSpan.FromSeconds(5);
            _deviceProperties = new Dictionary<Guid, Dictionary<string, object>>();
            // Initialize the metadata dictionary
            _deviceMetadata = new Dictionary<Guid, Dictionary<string, IPropertyMetadata>>();
            _eventChannel = Channel.CreateUnbounded<IPropertyChangedEvent>(new UnboundedChannelOptions { SingleReader = true });
            _processingTask = ProcessEventsAsync(_processingCts.Token);
        }

        public async Task AddOrUpdateAsync<T>(T component, CancellationToken ct = default) where T : IIoTDevice
        {
            if (component == null)
                throw new ArgumentNullException(nameof(component));

            component.SetEventHandler(this);
            bool containsDevice = _deviceProperties.ContainsKey(component.Id);
            if (!containsDevice)
            {
                _logger.Log($"[INFO] Registering new device {component.Id}");
                _deviceProperties[component.Id] = new Dictionary<string, object>();
                _deviceMetadata[component.Id] = new Dictionary<string, IPropertyMetadata>();
            }
            else
            {
                _logger.Log($"[INFO] Device {component.Id} already exists, updating properties.");
            }

            var storedProperties = await _store.LoadAsync(component.Id, ct);
            var storedMetadata = await _store.LoadMetadataAsync(component.Id, ct);
            if (storedProperties != null)
            {
                _deviceProperties[component.Id] = new Dictionary<string, object>(storedProperties);

                // Load stored metadata into our tracking dictionary
                if (storedMetadata != null && storedMetadata.Count > 0)
                {
                    _deviceMetadata[component.Id] = new Dictionary<string, IPropertyMetadata>(storedMetadata);
                    _logger.Log($"[INFO] Metadata loaded for device {component.Id}");
                }
                else
                {
                    _logger.Log($"[WARNING] No metadata found for device {component.Id}");
                    // Ensure we have a metadata dictionary even if none was loaded
                    _deviceMetadata[component.Id] = new Dictionary<string, IPropertyMetadata>();
                }

                await component.LoadPropertiesAsync(storedProperties, storedMetadata);
                _logger.Log($"[INFO] Properties loaded for device {component.Id}");
            }
            else
            {
                var deviceProps = component.GetProperties();
                var deviceMetadata = component.GetAllPropertyMetadata();
                if (deviceProps.Count > 0)
                {
                    _deviceProperties[component.Id] = new Dictionary<string, object>(deviceProps);

                    // Store the initial metadata in our tracking dictionary
                    _deviceMetadata[component.Id] = new Dictionary<string, IPropertyMetadata>(deviceMetadata);

                    _logger.Log($"[DEBUG] Saving metadata: {deviceMetadata.Count} entries found.");
                    await _store.SaveWithMetadataAsync(component.Id, deviceProps, deviceMetadata, ct);
                    _logger.Log($"[INFO] Device {component.Id} persisted with metadata.");
                }
                else
                {
                    _logger.Log($"[WARNING] No properties found for device {component.Id}");
                }
            }

            if (!containsDevice)
            {
                await component.InitializeAsync(ct);
            }
        }

        public Task<T?> GetPropertyAsync<T>(Guid deviceId, string propertyName, CancellationToken ct = default)
        {
            if (_deviceProperties.TryGetValue(deviceId, out var properties) && properties.TryGetValue(propertyName, out var value))
            {
                return Task.FromResult(value is T typedValue ? typedValue : default);
            }
            return Task.FromResult(default(T?));
        }

        public async Task HandleEventAsync<T>(object sender, T evt, CancellationToken ct = default) where T : IEvent
        {
            if (evt is IPropertyChangedEvent propertyChangedEvent)
            {
                _logger.Log($"[DEBUG] Handling property change event for device {propertyChangedEvent.DeviceId}, property {propertyChangedEvent.PropertyName}");
                try
                {
                    if (!_deviceProperties.TryGetValue(propertyChangedEvent.DeviceId, out var properties))
                    {
                        properties = new Dictionary<string, object>();
                        _deviceProperties[propertyChangedEvent.DeviceId] = properties;
                    }

                    // Also ensure we have a metadata dictionary for this device
                    if (!_deviceMetadata.TryGetValue(propertyChangedEvent.DeviceId, out var metadata))
                    {
                        metadata = new Dictionary<string, IPropertyMetadata>();
                        _deviceMetadata[propertyChangedEvent.DeviceId] = metadata;
                    }

                    // Update the property value
                    properties[propertyChangedEvent.PropertyName] = propertyChangedEvent.NewValue ?? new object();

                    // Store the metadata from this event in our tracking dictionary
                    if (propertyChangedEvent.Metadata != null)
                    {
                        metadata[propertyChangedEvent.PropertyName] = propertyChangedEvent.Metadata;
                    }

                    await _eventChannel.Writer.WriteAsync(propertyChangedEvent, ct);
                    await _eventBus.PublishAsync(this, propertyChangedEvent, ct);
                }
                catch (Exception? ex)
                {
                    _logger.Log(ex, $"[ERROR] Failed to handle property change event for device {propertyChangedEvent.DeviceId}, property {propertyChangedEvent.PropertyName}");
                }
            }
            else
            {
                _logger.Log($"[WARNING] Received event of unsupported type: {evt.GetType().Name}");
            }
        }

        public async Task ProcessPendingEventsAsync()
        {
            var pendingEvents = new Dictionary<Guid, Dictionary<string, IPropertyChangedEvent>>();
            while (_eventChannel.Reader.TryRead(out var evt))
            {
                if (!pendingEvents.TryGetValue(evt.DeviceId, out var deviceEvents))
                {
                    deviceEvents = new Dictionary<string, IPropertyChangedEvent>();
                    pendingEvents[evt.DeviceId] = deviceEvents;
                }
                deviceEvents[evt.PropertyName] = evt;
            }

            if (pendingEvents.Count > 0)
            {
                _logger.Log($"[DEBUG] Processing {pendingEvents.Count} pending events");
                await PersistPendingEventsAsync(pendingEvents);
            }
            else
            {
                _logger.Log("[DEBUG] No pending events to process");
            }
        }

        private async Task ProcessEventsAsync(CancellationToken ct)
        {
            var pendingEvents = new Dictionary<Guid, Dictionary<string, IPropertyChangedEvent>>();
            var batchTimer = new PeriodicTimer(_batchInterval);

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    bool hasEvents = false;
                    while (_eventChannel.Reader.TryRead(out var evt))
                    {
                        hasEvents = true;
                        if (!pendingEvents.TryGetValue(evt.DeviceId, out var deviceEvents))
                        {
                            deviceEvents = new Dictionary<string, IPropertyChangedEvent>();
                            pendingEvents[evt.DeviceId] = deviceEvents;
                        }
                        deviceEvents[evt.PropertyName] = evt;
                    }

                    if (hasEvents || !await batchTimer.WaitForNextTickAsync(ct))
                    {
                        if (pendingEvents.Count > 0)
                        {
                            await PersistPendingEventsAsync(pendingEvents);
                            pendingEvents.Clear();
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.Log("[INFO] Event processing loop canceled.");
            }
            catch (Exception? ex)
            {
                _logger.Log(ex, "[ERROR] Error occurred while processing events.");
            }
        }

        private async Task PersistPendingEventsAsync(Dictionary<Guid, Dictionary<string, IPropertyChangedEvent>> pendingEvents)
        {
            if (pendingEvents.Count == 0) return;

            try
            {
                await _transactionLock.WaitAsync();
                try
                {
                    _logger.Log($"[INFO] Persisting {pendingEvents.Count} batched device events.");
                    await using var transaction = await _store.BeginTransactionAsync();

                    foreach (var (deviceId, deviceEvents) in pendingEvents)
                    {
                        if (!_deviceProperties.TryGetValue(deviceId, out var properties))
                        {
                            properties = new Dictionary<string, object>();
                            _deviceProperties[deviceId] = properties;
                        }

                        // Use our tracked metadata for the device to ensure we preserve all metadata
                        if (!_deviceMetadata.TryGetValue(deviceId, out var allDeviceMetadata))
                        {
                            allDeviceMetadata = new Dictionary<string, IPropertyMetadata>();
                            _deviceMetadata[deviceId] = allDeviceMetadata;
                        }

                        // Update properties and metadata from events
                        foreach (var (propName, evt) in deviceEvents)
                        {
                            properties[propName] = evt.NewValue ?? new object();
                            if (evt.Metadata != null)
                            {
                                allDeviceMetadata[propName] = evt.Metadata;
                            }
                        }

                        // Send ALL metadata to the transaction
                        await transaction.SaveWithMetadataAsync(deviceId, properties, allDeviceMetadata);
                    }

                    await transaction.CommitAsync();
                    _logger.Log("[INFO] Event batch successfully persisted with metadata.");
                }
                finally
                {
                    _transactionLock.Release();
                }
            }
            catch (Exception? ex)
            {
                _logger.Log(ex, "[ERROR] Failed to persist device events with metadata.");
                throw;
            }
        }

        public async Task<List<(Guid Id, string Name, IDictionary<string, object> Properties, IDictionary<string, IPropertyMetadata> Metadata)>> GetAllStoredDevices()
        {
            var storedDevices = new List<(Guid Id, string Name, IDictionary<string, object>, IDictionary<string, IPropertyMetadata>)>();

            foreach (var deviceId in _deviceProperties.Keys)
            {
                var properties = await LoadDevicePropertiesAsync(deviceId);
                var metadata = await LoadDeviceMetadataAsync(deviceId);

                if (properties != null && properties.TryGetValue("Name", out var nameObj) && nameObj is string name)
                {
                    storedDevices.Add((deviceId, name, properties, metadata ?? new Dictionary<string, IPropertyMetadata>()));
                }
            }

            return storedDevices;
        }

        /// <summary>
        /// Loads stored properties for a specific device ID.
        /// </summary>
        private async Task<IDictionary<string, object>> LoadDevicePropertiesAsync(Guid deviceId)
        {
            return await _store.LoadAsync(deviceId) ?? new Dictionary<string, object>();
        }

        /// <summary>
        /// Loads stored metadata for a specific device ID.
        /// </summary>
        private async Task<IDictionary<string, IPropertyMetadata>> LoadDeviceMetadataAsync(Guid deviceId)
        {
            return await _store.LoadMetadataAsync(deviceId) ?? new Dictionary<string, IPropertyMetadata>();
        }


        public async ValueTask DisposeAsync()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            _processingCts.Cancel();
            try
            {
                await _processingTask;
            }
            catch (OperationCanceledException)
            {
            }
            _processingCts.Dispose();
            _transactionLock.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}