using HydroGarden.Foundation.Abstractions.Interfaces;
using HydroGarden.Foundation.Common.Locking;
using HydroGarden.Foundation.Common.Logging;
using HydroGarden.Foundation.Core.EventHandlers;

namespace HydroGarden.Foundation.Core.Devices
{
    public abstract class IoTDeviceBase : IIoTDevice
    {
        protected readonly IHydroGardenLogger _logger;
        private volatile DeviceState _state = DeviceState.Created;
        private readonly CancellationTokenSource _executionCts = new();
        private IHydroGardenEventHandler? _eventHandler;
        private readonly Dictionary<string, object?> _localPropertyCache = new();
        private readonly AsyncReaderWriterLock _lock = new();

        public Guid Id { get; }
        public string Name { get; }
        public DeviceType Type { get; }
        public DeviceState State => _state;

        protected IoTDeviceBase(Guid id, string name, DeviceType type) : this(id, name, type, new HydroGardenLogger())
        {
            
        }

        protected IoTDeviceBase(Guid id, string name, DeviceType type, IHydroGardenLogger logger)
        {
            Id = id;
            Name = name;
            Type = type;
            _logger = logger;
        }

        public void SetEventHandler(IHydroGardenEventHandler handler)
        {
            _eventHandler = handler;
        }

        protected async Task<T?> GetPropertyValueAsync<T>(string name)
        {
            using var readLock = await _lock.ReaderLockAsync();
            return _localPropertyCache.TryGetValue(name, out var value) && value is T typedValue
                ? typedValue
                : default;
        }

        protected async Task PublishPropertyAsync<T>(string name, T value, bool isReadOnly = false)
        {
            if (_eventHandler == null)
            {
                _logger.Log($"No event handler registered for device {Id}");
                return;
            }

            using var writeLock = await _lock.WriterLockAsync();
            var oldValue = _localPropertyCache.TryGetValue(name, out var existing) ? existing : default;
            _localPropertyCache[name] = value;

            var evt = new HydroGardenPropertyChangedEvent(Id, name, typeof(T), oldValue, value, isReadOnly);
            await _eventHandler.HandleEventAsync(this, evt, CancellationToken.None);
        }

        public virtual async Task InitializeAsync(CancellationToken ct = default)
        {
            if (_state != DeviceState.Created)
                throw new InvalidOperationException($"Cannot initialize device in state {_state}");

            try
            {
                _state = DeviceState.Initializing;

                // Publish core device properties
                await PublishPropertyAsync("Id", Id, isReadOnly: true);
                await PublishPropertyAsync("Name", Name, isReadOnly: true);
                await PublishPropertyAsync("AssemblyType", GetType(), isReadOnly: true);
                await PublishPropertyAsync("DeviceType", Type, isReadOnly: true);
                await PublishPropertyAsync("State", _state, isReadOnly: true);

                await OnInitializedAsync(ct);
                _state = DeviceState.Ready;
                await PublishPropertyAsync("State", _state, isReadOnly: true);
            }
            catch (Exception ex)
            {
                _state = DeviceState.Error;
                await PublishPropertyAsync("State", _state, isReadOnly: true);
                _logger.Log(ex, $"Failed to initialize device {Id}");
                throw;
            }
        }

        public async Task ExecuteCoreAsync(CancellationToken ct = default)
        {
            if (_state != DeviceState.Ready)
                throw new InvalidOperationException($"Cannot execute device in state {_state}");

            try
            {
                _state = DeviceState.Running;
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _executionCts.Token);
                await OnExecuteCoreAsync(linkedCts.Token);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Normal cancellation
            }
            catch (Exception ex)
            {
                _state = DeviceState.Error;
                _logger.Log(ex, $"Device execution failed {Id}");
                throw;
            }
        }

        public async Task StopAsync(CancellationToken ct = default)
        {
            if (_state != DeviceState.Running)
                return;

            try
            {
                _state = DeviceState.Stopping;
                _executionCts.Cancel();
                await OnStopAsync(ct);
                _state = DeviceState.Ready;
            }
            catch (Exception ex)
            {
                _state = DeviceState.Error;
                _logger.Log(ex, $"Failed to stop device {Id}");
                throw;
            }
        }

        protected virtual Task OnInitializedAsync(CancellationToken ct) => Task.CompletedTask;
        protected abstract Task OnExecuteCoreAsync(CancellationToken ct);
        protected virtual Task OnStopAsync(CancellationToken ct) => Task.CompletedTask;

        public virtual void Dispose()
        {
            _state = DeviceState.Disposed;
            _executionCts.Dispose();
            _lock.Dispose();
        }
    }
}
