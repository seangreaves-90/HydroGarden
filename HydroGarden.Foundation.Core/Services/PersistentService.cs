using HydroGarden.Foundation.Abstractions.Interfaces;
using HydroGarden.Foundation.Common.Caching;
using HydroGarden.Foundation.Common.Logging;
using HydroGarden.Foundation.Core.Components;
using System.Threading.Channels;

namespace HydroGarden.Foundation.Core.Services
{
    /// <summary>
    /// Service for persisting device properties to storage.
    /// </summary>
    public class PersistenceService : IHydroGardenEventHandler, IAsyncDisposable
    {
        private readonly IStore _store;
        private readonly IHydroGardenLogger _logger;
        private readonly LruCache<Guid, Dictionary<string, object>> _deviceProperties;
        private readonly Channel<IHydroGardenPropertyChangedEvent> _eventChannel;
        private readonly CancellationTokenSource _processingCts = new();
        private readonly Task _processingTask;
        private readonly SemaphoreSlim _transactionLock = new(1, 1);
        private readonly int _maxCachedDevices;
        private readonly TimeSpan _batchInterval;
        private bool _isDisposed;

        /// <summary>
        /// Initializes a new instance of the PersistenceService class.
        /// </summary>
        /// <param name="store">The storage mechanism for persisting properties.</param>
        public PersistenceService(IStore store)
            : this(store, new HydroGardenLogger(), 1000, TimeSpan.FromSeconds(5))
        {
        }

        /// <summary>
        /// Initializes a new instance of the PersistenceService class with explicit parameters.
        /// </summary>
        /// <param name="store">The storage mechanism for persisting properties.</param>
        /// <param name="logger">The logger for recording information and errors.</param>
        /// <param name="maxCachedDevices">The maximum number of devices to cache in memory.</param>
        /// <param name="batchInterval">The interval at which to batch property updates.</param>
        public PersistenceService(IStore store, IHydroGardenLogger logger, int maxCachedDevices = 1000, TimeSpan? batchInterval = null)
        {
            _store = store;
            _logger = logger;
            _maxCachedDevices = maxCachedDevices;
            _batchInterval = batchInterval ?? TimeSpan.FromSeconds(5);

            // Initialize LRU cache with capacity constraint
            _deviceProperties = new LruCache<Guid, Dictionary<string, object>>(_maxCachedDevices);

            // Initialize the event channel with single reader for ordered processing
            _eventChannel = Channel.CreateUnbounded<IHydroGardenPropertyChangedEvent>(
                new UnboundedChannelOptions { SingleReader = true });

            // Start the background processing task
            _processingTask = ProcessEventsAsync(_processingCts.Token);
        }

        /// <summary>
        /// Registers a device with the persistence service.
        /// </summary>
        /// <param name="component">The component to register.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public async Task AddDeviceAsync(IHydroGardenComponent component, CancellationToken ct = default)
        {
            // Check if the device is already registered
            if (_deviceProperties.TryGetValue(component.Id, out _))
            {
                throw new InvalidOperationException($"Device {component.Id} already registered");
            }

            // Register event handler for the component
            if (component is HydroGardenComponentBase deviceBase)
            {
                deviceBase.SetEventHandler(this);
            }

            // Load stored properties
            var storedProperties = await _store.LoadAsync(component.Id, ct);
            var deviceProps = new Dictionary<string, object>();

            if (storedProperties != null)
            {
                foreach (var (key, value) in storedProperties)
                {
                    deviceProps[key] = value;
                }
            }

            // Add to cache
            await _deviceProperties.AddOrUpdateAsync(component.Id, deviceProps);
        }

        /// <inheritdoc/>
        public Task HandleEventAsync(object sender, IHydroGardenPropertyChangedEvent e, CancellationToken ct)
        {
            // Queue the event for processing
            return _eventChannel.Writer.WriteAsync(e, ct).AsTask();
        }

        /// <summary>
        /// Gets a property value for a specific device.
        /// </summary>
        /// <typeparam name="T">The type of the property value.</typeparam>
        /// <param name="deviceId">The device identifier.</param>
        /// <param name="propertyName">The property name.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The property value, or default if not found.</returns>
        public async Task<T?> GetPropertyAsync<T>(Guid deviceId, string propertyName, CancellationToken ct = default)
        {
            if (!_deviceProperties.TryGetValue(deviceId, out var properties) ||
                !properties.TryGetValue(propertyName, out var value))
            {
                return default;
            }

            return value is T typedValue ? typedValue : default;
        }

        /// <summary>
        /// Processes property change events and persists them to storage.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        private async Task ProcessEventsAsync(CancellationToken ct)
        {
            var pendingEvents = new Dictionary<Guid, Dictionary<string, IHydroGardenPropertyChangedEvent>>();
            var batchTimer = new PeriodicTimer(_batchInterval);

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    // Process events until the batch interval elapses or cancellation is requested
                    var hasEvents = false;

                    // Read events from the channel with a timeout
                    while (await _eventChannel.Reader.WaitToReadAsync(ct) &&
                           _eventChannel.Reader.TryRead(out var evt))
                    {
                        hasEvents = true;

                        // Add to pending events, keeping only the latest event for each property
                        if (!pendingEvents.TryGetValue(evt.DeviceId, out var deviceEvents))
                        {
                            deviceEvents = new Dictionary<string, IHydroGardenPropertyChangedEvent>();
                            pendingEvents[evt.DeviceId] = deviceEvents;
                        }

                        deviceEvents[evt.PropertyName] = evt;

                        // Update in-memory cache
                        if (_deviceProperties.TryGetValue(evt.DeviceId, out var properties))
                        {
                            properties[evt.PropertyName] = evt.NewValue ?? new object();
                        }
                        else
                        {
                            var newProperties = new Dictionary<string, object>
                            {
                                { evt.PropertyName, evt.NewValue ?? new object() }
                            };
                            await _deviceProperties.AddOrUpdateAsync(evt.DeviceId, newProperties);
                        }
                    }

                    // If we have pending events and either the batch interval has elapsed or 
                    // the channel is empty, persist them
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
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Normal cancellation, persist any remaining events
                if (pendingEvents.Count > 0)
                {
                    await PersistPendingEventsAsync(pendingEvents);
                }
            }
            catch (Exception ex)
            {
                _logger.Log(ex, "Error in event processing loop");
            }
        }

        private async Task PersistPendingEventsAsync(Dictionary<Guid, Dictionary<string, IHydroGardenPropertyChangedEvent>> pendingEvents)
        {
            if (pendingEvents.Count == 0)
                return;

            try
            {
                // Acquire transaction lock to ensure only one batch is processed at a time
                await _transactionLock.WaitAsync();

                try
                {
                    // Start a transaction
                    await using var transaction = await _store.BeginTransactionAsync();

                    // Process each device's events
                    foreach (var (deviceId, deviceEvents) in pendingEvents)
                    {
                        // Get the current properties from cache
                        if (!_deviceProperties.TryGetValue(deviceId, out var properties))
                        {
                            properties = new Dictionary<string, object>();
                            foreach (var (propName, evt) in deviceEvents)
                            {
                                properties[propName] = evt.NewValue ?? new object();
                            }
                        }

                        // Save the properties
                        await transaction.SaveAsync(deviceId, properties);
                    }

                    // Commit the transaction
                    await transaction.CommitAsync();
                }
                finally
                {
                    _transactionLock.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.Log(ex, $"Failed to persist {pendingEvents.Count} device events");
            }
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            // Cancel the processing task
            _processingCts.Cancel();

            try
            {
                // Wait for the processing task to complete with timeout
                await Task.WhenAny(_processingTask, Task.Delay(5000));
            }
            catch (Exception ex)
            {
                _logger.Log(ex, "Error while disposing persistence service");
            }

            _processingCts.Dispose();
            _transactionLock.Dispose();
        }
    }
}