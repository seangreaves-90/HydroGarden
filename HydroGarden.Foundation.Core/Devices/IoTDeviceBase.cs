using HydroGarden.Foundation.Abstractions.Interfaces;
using HydroGarden.Foundation.Common.Locking;

namespace HydroGarden.Foundation.Core.Devices
{
    public abstract class IoTDeviceBase : IIoTDevice
    {
        private readonly IPropertyManager _properties;
        private readonly AsyncReaderWriterLock _stateLock;
        protected ILogger Logger { get; }
        private volatile DeviceState _state;
        private volatile bool _isDisposed;

        public Guid Id { get; }
        public string DisplayName { get; }
        public string DeviceType { get; }
        public DeviceState State => _state;

        protected IoTDeviceBase(
            Guid id,
            string displayName,
            string deviceType,
            IPropertyManager properties,
            ILogger logger)
        {
            Id = id;
            DisplayName = displayName;
            DeviceType = deviceType;
            _properties = properties ?? throw new ArgumentNullException(nameof(properties));
            Logger = logger;
            _stateLock = new AsyncReaderWriterLock();
            _state = DeviceState.Created;

            // Subscribe to property changes
            _properties.PropertyChanged += OnPropertyChanged;
        }

        protected async Task<bool> TryChangeStateAsync(
            DeviceState expectedState,
            DeviceState newState,
            CancellationToken ct)
        {
            using var writeLock = await _stateLock.WriterLockAsync(ct);
            if (_state != expectedState) return false;

            _state = newState;
            await SetPropertyAsync("State", newState, isReadOnly: true, ct: ct);
            return true;
        }

        public async Task InitializeAsync(CancellationToken ct)
        {
            if (!await TryChangeStateAsync(DeviceState.Created, DeviceState.Initializing, ct))
            {
                throw new InvalidOperationException($"Cannot initialize device in state {_state}");
            }

            try
            {
                await _properties.LoadAsync(ct);
                await OnInitializingAsync(ct);
                await InitializePropertiesAsync(ct);
                await OnInitializedAsync(ct);

                await TryChangeStateAsync(DeviceState.Initializing, DeviceState.Ready, ct);
            }
            catch (Exception ex)
            {
                await TryChangeStateAsync(_state, DeviceState.Error, ct);
                Logger.LogError(ex, "Device initialization failed");
                throw;
            }
        }

        private async Task InitializePropertiesAsync(CancellationToken ct)
        {
            await SetPropertyAsync("Id", Id.ToString(), isReadOnly: true, ct: ct);
            await SetPropertyAsync("DisplayName", DisplayName, isReadOnly: true, ct: ct);
            await SetPropertyAsync("DeviceType", DeviceType, isReadOnly: true, ct: ct);
        }

        protected virtual Task OnInitializingAsync(CancellationToken ct) => Task.CompletedTask;
        protected virtual Task OnInitializedAsync(CancellationToken ct) => Task.CompletedTask;

        public abstract Task ExecuteCoreAsync(CancellationToken ct);

        public virtual Task SaveAsync(CancellationToken ct)
        {
            return _properties.SaveAsync(ct);
        }

        protected Task<T?> GetPropertyAsync<T>(string name, CancellationToken ct = default)
        {
            return _properties.GetPropertyAsync<T>(name, ct);
        }

        protected Task SetPropertyAsync<T>(
            string name,
            T value,
            bool isReadOnly = false,
            IPropertyValidator<IValidationResult, T>? validator = null,
            CancellationToken ct = default)
        {
            return _properties.SetPropertyAsync(name, value, isReadOnly, validator, ct);
        }

        protected virtual void OnPropertyChanged(object? sender, IPropertyChangedEventArgs e)
        {
            Logger.LogWarning(null,
                $"Property changed on device {Id}: {e.Name} from {e.OldValue} to {e.NewValue}");
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            try
            {
                _properties.PropertyChanged -= OnPropertyChanged;
                _properties.Dispose();
                _stateLock.Dispose();
                OnDispose();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error during device disposal");
            }
        }

        protected virtual void OnDispose() { }
    }
}