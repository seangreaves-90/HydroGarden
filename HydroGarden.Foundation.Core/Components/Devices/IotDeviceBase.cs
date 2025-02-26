using HydroGarden.Foundation.Abstractions.Interfaces;

namespace HydroGarden.Foundation.Core.Components.Devices
{
    /// <summary>
    /// Base class for IoT devices in the HydroGarden system.
    /// Implements the <see cref="IIoTDevice"/> interface and provides lifecycle management.
    /// </summary>
    public abstract class IoTDeviceBase : HydroGardenComponentBase, IIoTDevice
    {
        private readonly CancellationTokenSource _executionCts = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="IoTDeviceBase"/> class.
        /// </summary>
        /// <param name="id">The unique identifier of the device.</param>
        /// <param name="name">The name of the device.</param>
        /// <param name="logger">Optional logger instance.</param>
        protected IoTDeviceBase(Guid id, string name, IHydroGardenLogger? logger = null)
            : base(id, name, logger)
        {
        }

        /// <inheritdoc />
        public virtual async Task InitializeAsync(CancellationToken ct = default)
        {
            if (State != ComponentState.Created)
                throw new InvalidOperationException($"Cannot initialize device in state {State}");

            try
            {
                await SetPropertyAsync(nameof(State), ComponentState.Initializing);
                await SetPropertyAsync("Id", Id);
                await SetPropertyAsync("Name", Name);
                await SetPropertyAsync("AssemblyType", AssemblyType);
                await OnInitializeAsync(ct);
                await SetPropertyAsync(nameof(State), ComponentState.Ready);
            }
            catch (Exception ex)
            {
                _logger.Log(ex, "Failed to initialize device");
                await SetPropertyAsync(nameof(State), ComponentState.Error);
                throw;
            }
        }

        /// <summary>
        /// Performs device-specific initialization logic.
        /// Override this method to customize initialization behavior.
        /// </summary>
        protected virtual Task OnInitializeAsync(CancellationToken ct) => Task.CompletedTask;

        /// <inheritdoc />
        public virtual async Task StartAsync(CancellationToken ct = default)
        {
            if (State != ComponentState.Ready)
                throw new InvalidOperationException($"Cannot start device in state {State}");

            await SetPropertyAsync(nameof(State), ComponentState.Running);
            try
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _executionCts.Token);
                await OnStartAsync(linkedCts.Token);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
            }
            catch (Exception)
            {
                await SetPropertyAsync(nameof(State), ComponentState.Error);
                throw;
            }
        }

        /// <summary>
        /// Performs device-specific startup logic.
        /// Override this method to implement custom start behavior.
        /// </summary>
        protected virtual Task OnStartAsync(CancellationToken ct) => Task.CompletedTask;

        /// <inheritdoc />
        public virtual async Task StopAsync(CancellationToken ct = default)
        {
            if (State != ComponentState.Running)
                return;

            await SetPropertyAsync(nameof(State), ComponentState.Stopping);
            _executionCts.Cancel();

            try
            {
                await OnStopAsync(ct);
                await SetPropertyAsync(nameof(State), ComponentState.Ready);
            }
            catch (Exception)
            {
                await SetPropertyAsync(nameof(State), ComponentState.Error);
                throw;
            }
        }

        /// <summary>
        /// Performs device-specific stop logic.
        /// Override this method to implement custom stop behavior.
        /// </summary>
        protected virtual Task OnStopAsync(CancellationToken ct) => Task.CompletedTask;

        /// <inheritdoc />
        public override void Dispose()
        {
            _executionCts.Cancel();
            _executionCts.Dispose();
            base.Dispose();
        }
    }
}
