using HydroGarden.Foundation.Abstractions.Interfaces;
using HydroGarden.Foundation.Common.Caching;
using HydroGarden.Foundation.Common.Logging;
using HydroGarden.Foundation.Core.Components;
using System.Threading.Channels;

namespace HydroGarden.Foundation.Core.Services
{
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

        public PersistenceService(IStore store)
            : this(store, new HydroGardenLogger(), 1000, TimeSpan.FromSeconds(5))
        {
        }

        public PersistenceService(IStore store, IHydroGardenLogger logger, int maxCachedDevices = 1000, TimeSpan? batchInterval = null)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _maxCachedDevices = maxCachedDevices;
            _batchInterval = batchInterval ?? TimeSpan.FromSeconds(5);

            _deviceProperties = new LruCache<Guid, Dictionary<string, object>>(_maxCachedDevices);
            _eventChannel = Channel.CreateUnbounded<IHydroGardenPropertyChangedEvent>(new UnboundedChannelOptions { SingleReader = true });
            _processingTask = ProcessEventsAsync(_processingCts.Token);
        }

        public async Task AddDeviceAsync(IHydroGardenComponent component, CancellationToken ct = default)
        {
            if (component == null) throw new ArgumentNullException(nameof(component));

            if (_deviceProperties.TryGetValue(component.Id, out _))
            {
                throw new InvalidOperationException($"Device {component.Id} already registered");
            }

            if (component is HydroGardenComponentBase deviceBase)
            {
                deviceBase.SetEventHandler(this);
            }

            var storedProperties = await _store.LoadAsync(component.Id, ct);
            var deviceProps = storedProperties != null ? new Dictionary<string, object>(storedProperties) : new Dictionary<string, object>();

            await _deviceProperties.AddOrUpdateAsync(component.Id, deviceProps);
        }

        public Task HandleEventAsync(object sender, IHydroGardenPropertyChangedEvent e, CancellationToken ct)
        {
            return _eventChannel.Writer.WriteAsync(e, ct).AsTask();
        }

        public Task<T?> GetPropertyAsync<T>(Guid deviceId, string propertyName, CancellationToken ct = default)
        {
            if (_deviceProperties.TryGetValue(deviceId, out var properties) && properties != null && properties.TryGetValue(propertyName, out var value))
            {
                return Task.FromResult(value is T typedValue ? typedValue : default);
            }
            return Task.FromResult<T?>(default);
        }


        private async Task ProcessEventsAsync(CancellationToken ct)
        {
            var pendingEvents = new Dictionary<Guid, Dictionary<string, IHydroGardenPropertyChangedEvent>>();
            var batchTimer = new PeriodicTimer(_batchInterval);

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    bool hasEvents = false;

                    while (await _eventChannel.Reader.WaitToReadAsync(ct) &&
                           _eventChannel.Reader.TryRead(out var evt))
                    {
                        hasEvents = true;

                        if (!pendingEvents.TryGetValue(evt.DeviceId, out var deviceEvents))
                        {
                            deviceEvents = new Dictionary<string, IHydroGardenPropertyChangedEvent>();
                            pendingEvents[evt.DeviceId] = deviceEvents;
                        }

                        deviceEvents[evt.PropertyName] = evt;

                        if (!_deviceProperties.TryGetValue(evt.DeviceId, out var properties) || properties == null)
                        {
                            properties = new Dictionary<string, object>();
                            _deviceProperties.AddOrUpdate(evt.DeviceId, properties);
                        }

                        if (!string.IsNullOrEmpty(evt.PropertyName)) // Ensure PropertyName is valid
                        {
                            properties[evt.PropertyName] = evt.NewValue ?? new object();
                        }
                        else
                        {
                            _logger.Log($"[WARNING] Null or empty property name received for device {evt.DeviceId}");
                        }

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
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
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
            if (pendingEvents.Count == 0) return;

            try
            {
                await _transactionLock.WaitAsync();

                try
                {
                    await using var transaction = await _store.BeginTransactionAsync();

                    foreach (var (deviceId, deviceEvents) in pendingEvents)
                    {
                        if (!_deviceProperties.TryGetValue(deviceId, out var properties) || properties == null)
                        {
                            properties = new Dictionary<string, object>();
                            _deviceProperties.AddOrUpdate(deviceId, properties);
                        }

                        foreach (var (propName, evt) in deviceEvents)
                        {
                            if (!string.IsNullOrEmpty(propName)) // Ensure PropertyName is valid
                            {
                                properties[propName] = evt.NewValue ?? new object();
                            }
                            else
                            {
                                _logger.Log($"[WARNING] Null or empty property name in PersistPendingEventsAsync for device {deviceId}");
                            }
                        }

                        await transaction.SaveAsync(deviceId, properties);
                    }


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

        public async ValueTask DisposeAsync()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            _processingCts.Cancel();

            try
            {
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
