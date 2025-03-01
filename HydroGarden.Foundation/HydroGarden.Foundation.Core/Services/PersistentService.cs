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
    /// <summary>
    /// The PersistenceService is responsible for managing device properties, persisting them to storage,
    /// and propagating changes via the event bus.
    /// </summary>
    public class PersistenceService : IPersistenceService, IHydroGardenPropertyChangedEventHandler
    {
        private readonly IStore _store;
        private readonly IEventBus _eventBus;
        private readonly IHydroGardenLogger _logger;
        private readonly Dictionary<Guid, Dictionary<string, object>> _deviceProperties;
        private readonly Channel<IHydroGardenPropertyChangedEvent> _eventChannel;
        private readonly CancellationTokenSource _processingCts = new();
        private readonly Task _processingTask;
        private readonly SemaphoreSlim _transactionLock = new(1, 1);
        private readonly int _maxCachedDevices;
        private readonly TimeSpan _batchInterval;
        private bool _isDisposed;

        /// <summary>
        /// Enables or disables batch processing of events.
        /// </summary>
        public bool IsBatchProcessingEnabled { get; set; } = true;

        /// <summary>
        /// Forces transaction creation even when there are no pending events (for testing).
        /// </summary>
        public bool ForceTransactionCreation { get; set; } = false;

        /// <summary>
        /// Stores a test event for validation in testing environments.
        /// </summary>
        public IHydroGardenPropertyChangedEvent? TestEvent { get; set; }

        /// <summary>
        /// Initializes the PersistenceService.
        /// </summary>
        public PersistenceService(IStore store, IEventBus eventBus)
            : this(store, eventBus, new HydroGardenLogger(), 1000, TimeSpan.FromSeconds(5))
        {
        }

        /// <summary>
        /// Initializes the PersistenceService with configurable options.
        /// </summary>
        public PersistenceService(IStore store, IEventBus eventBus, IHydroGardenLogger logger, int maxCachedDevices = 1000, TimeSpan? batchInterval = null)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _maxCachedDevices = maxCachedDevices;
            _batchInterval = batchInterval ?? TimeSpan.FromSeconds(5);
            _deviceProperties = new Dictionary<Guid, Dictionary<string, object>>();
            _eventChannel = Channel.CreateUnbounded<IHydroGardenPropertyChangedEvent>(new UnboundedChannelOptions { SingleReader = true });
            _processingTask = ProcessEventsAsync(_processingCts.Token);
        }

        /// <summary>
        /// Registers a new device or updates an existing device's properties.
        /// Ensures component properties are loaded and stored efficiently.
        /// </summary>
        public async Task AddOrUpdateAsync<T>(T component, CancellationToken ct = default) where T : IHydroGardenComponent
        {
            if (component == null)
                throw new ArgumentNullException(nameof(component));

            component.SetEventHandler(this);

            if (!_deviceProperties.ContainsKey(component.Id))
            {
                _logger.Log($"[INFO] Registering new device {component.Id}");
                _deviceProperties[component.Id] = new Dictionary<string, object>();
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
                await component.LoadPropertiesAsync(storedProperties, storedMetadata);
                _logger.Log($"[INFO] Properties loaded for device {component.Id}");
            }
            else
            {
                var deviceProps = component.GetProperties();
                if (deviceProps.Count > 0)
                {
                    _deviceProperties[component.Id] = new Dictionary<string, object>(deviceProps);
                    await _store.SaveWithMetadataAsync(component.Id, deviceProps, component.GetAllPropertyMetadata(), ct);
                    _logger.Log($"[INFO] Device {component.Id} persisted.");
                }
                else
                {
                    _logger.Log($"[WARNING] No properties found for device {component.Id}");
                }
            }
        }

        /// <summary>
        /// Retrieves a stored property value for a given device.
        /// </summary>
        public Task<T?> GetPropertyAsync<T>(Guid deviceId, string propertyName, CancellationToken ct = default)
        {
            if (_deviceProperties.TryGetValue(deviceId, out var properties) && properties.TryGetValue(propertyName, out var value))
            {
                return Task.FromResult(value is T typedValue ? typedValue : default);
            }
            return Task.FromResult(default(T?));
        }

        /// <summary>
        /// Handles property change events from IoT devices.
        /// </summary>
        public async Task HandleEventAsync(object sender, IHydroGardenPropertyChangedEvent e, CancellationToken ct)
        {
            try
            {
                _logger.Log($"[DEBUG] Handling event for device {e.DeviceId}, property {e.PropertyName}");

                if (!_deviceProperties.TryGetValue(e.DeviceId, out var properties))
                {
                    properties = new Dictionary<string, object>();
                    _deviceProperties[e.DeviceId] = properties;
                }

                properties[e.PropertyName] = e.NewValue ?? new object();

                await _eventChannel.Writer.WriteAsync(e, ct);
                await _eventBus.PublishAsync(this, e, ct);
            }
            catch (Exception ex)
            {
                _logger.Log(ex, $"Failed to handle property change event for {e.DeviceId}, property {e.PropertyName}");
            }
        }

        /// <summary>
        /// Manually triggers batch processing of pending events.
        /// </summary>
        public async Task ProcessPendingEventsAsync()
        {
            var pendingEvents = new Dictionary<Guid, Dictionary<string, IHydroGardenPropertyChangedEvent>>();

            while (_eventChannel.Reader.TryRead(out var evt))
            {
                if (!pendingEvents.TryGetValue(evt.DeviceId, out var deviceEvents))
                {
                    deviceEvents = new Dictionary<string, IHydroGardenPropertyChangedEvent>();
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

        /// <summary>
        /// Asynchronously processes queued events in batches.
        /// This method continuously reads from the event channel and persists updates.
        /// </summary>
        private async Task ProcessEventsAsync(CancellationToken ct)
        {
            var pendingEvents = new Dictionary<Guid, Dictionary<string, IHydroGardenPropertyChangedEvent>>();
            var batchTimer = new PeriodicTimer(_batchInterval);

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    bool hasEvents = false;

                    // Process all queued events
                    while (_eventChannel.Reader.TryRead(out var evt))
                    {
                        hasEvents = true;
                        if (!pendingEvents.TryGetValue(evt.DeviceId, out var deviceEvents))
                        {
                            deviceEvents = new Dictionary<string, IHydroGardenPropertyChangedEvent>();
                            pendingEvents[evt.DeviceId] = deviceEvents;
                        }
                        deviceEvents[evt.PropertyName] = evt;
                    }

                    // Either process events immediately or wait for the batch timer to expire
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
            catch (Exception ex)
            {
                _logger.Log(ex, "[ERROR] Error occurred while processing events.");
            }
        }

        /// <summary>
        /// Persists batched property change events to the database.
        /// </summary>
        /// <param name="pendingEvents">A dictionary of device property updates.</param>
        private async Task PersistPendingEventsAsync(Dictionary<Guid, Dictionary<string, IHydroGardenPropertyChangedEvent>> pendingEvents)
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

                        foreach (var (propName, evt) in deviceEvents)
                        {
                            properties[propName] = evt.NewValue ?? new object();
                        }

                        await transaction.SaveAsync(deviceId, properties);
                    }

                    await transaction.CommitAsync();
                    _logger.Log("[INFO] Event batch successfully persisted.");
                }
                finally
                {
                    _transactionLock.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.Log(ex, "[ERROR] Failed to persist device events.");
                throw; 
            }
        }

        /// <summary>
        /// Disposes the persistence service asynchronously.
        /// </summary>
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
                // Ignore the cancellation exception
            }

            _processingCts.Dispose();
            _transactionLock.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
