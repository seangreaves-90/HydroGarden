using HydroGarden.Foundation.Abstractions.Interfaces;
using HydroGarden.Foundation.Common.Locking;
using HydroGarden.Foundation.Common.Logging;
using HydroGarden.Foundation.Core.Devices;
using System.Threading.Channels;


namespace HydroGarden.Foundation.Core.Services
{
    public class PersistenceService : IHydroGardenEventHandler
    {
        private readonly IStore _store;
        private readonly IHydroGardenLogger _logger;
        private readonly Dictionary<Guid, Dictionary<string, object>> _deviceProperties = new();
        private readonly AsyncReaderWriterLock _lock = new();
        private readonly Channel<IHydroGardenPropertyChangedEvent> _eventChannel;

        public PersistenceService(IStore store) : this(store, new HydroGardenLogger())
        {

        }
        public PersistenceService(IStore store, IHydroGardenLogger logger)
        {
            _store = store;
            _logger = logger;
            _eventChannel = Channel.CreateUnbounded<IHydroGardenPropertyChangedEvent>(
                new UnboundedChannelOptions { SingleReader = true });

            // Start processing events
            _ = ProcessEventsAsync();
        }

        public async Task AddDeviceAsync(IIoTDevice device, CancellationToken ct = default)
        {
            using var writeLock = await _lock.WriterLockAsync(ct);

            if (_deviceProperties.ContainsKey(device.Id))
                throw new InvalidOperationException($"Device {device.Id} already registered");

            _deviceProperties[device.Id] = new Dictionary<string, object>();

            if (device is IoTDeviceBase deviceBase)
            {
                deviceBase.SetEventHandler(this);
            }

            // Load any existing properties
            var storedProperties = await _store.LoadAsync(device.Id, ct);
            if (storedProperties != null)
            {
                foreach (var (key, value) in storedProperties)
                {
                    _deviceProperties[device.Id][key] = value;
                }
            }
        }

        public Task HandleEventAsync(object sender, IHydroGardenPropertyChangedEvent e, CancellationToken ct)
        {
            return _eventChannel.Writer.WriteAsync(e, ct).AsTask();
        }

        private async Task ProcessEventsAsync()
        {
            await foreach (var evt in _eventChannel.Reader.ReadAllAsync())
            {
                try
                {
                    await ProcessEventAsync(evt);
                }
                catch (Exception ex)
                {
                    _logger.Log(ex, "Failed to process device event");
                }
            }
        }

        private async Task ProcessEventAsync(IHydroGardenPropertyChangedEvent evt)
        {
            using var writeLock = await _lock.WriterLockAsync();

            if (!_deviceProperties.TryGetValue(evt.DeviceId, out var properties))
            {
                _logger.Log($"Received property event for unregistered device {evt.DeviceId}");
                return;
            }

            // Update property
            properties[evt.PropertyName] = evt.NewValue ?? new object();

            // Persist changes
            await SaveDevicePropertiesAsync(evt.DeviceId);
        }

        private async Task SaveDevicePropertiesAsync(Guid deviceId)
        {
            try
            {
                var properties = _deviceProperties[deviceId];
                await _store.SaveAsync(deviceId, properties);
            }
            catch (Exception ex)
            {
                _logger.Log(ex, $"Failed to save properties for device {deviceId}");
                throw;
            }
        }

        public async Task<T?> GetPropertyAsync<T>(Guid deviceId, string propertyName, CancellationToken ct = default)
        {
            using var readLock = await _lock.ReaderLockAsync(ct);

            if (!_deviceProperties.TryGetValue(deviceId, out var properties) ||
                !properties.TryGetValue(propertyName, out var value))
            {
                return default;
            }

            return value is T typedValue ? typedValue : default;
        }
    }
}
