// HydroGarden.Foundation.Core.Components.Devices/IoTDeviceBase.cs
using HydroGarden.Foundation.Abstractions.Interfaces;

namespace HydroGarden.Foundation.Core.Components.Devices
{
    public abstract class IoTDeviceBase : HydroGardenComponentBase, IIoTDevice
    {
        private readonly CancellationTokenSource _executionCts = new();

        protected IoTDeviceBase(Guid id, string name, IHydroGardenLogger? logger = null)
            : base(id, name, logger)
        {
        }

        public virtual async Task InitializeAsync(CancellationToken ct = default)
        {
            if (State != ComponentState.Created)
                throw new InvalidOperationException($"Cannot initialize device in state {State}");

            try
            {
                await SetPropertyAsync(nameof(State), ComponentState.Initializing, false, true);
                await SetPropertyAsync("Id", Id, false, false, "Device ID", "Unique identifier for this device");
                await SetPropertyAsync("Name", Name, true, true, "Device Name", "User-friendly name for this device");
                await SetPropertyAsync("AssemblyType", AssemblyType, false, false, "Device Type", "Technical type of this device");

                await OnInitializeAsync(ct);

                await SetPropertyAsync(nameof(State), ComponentState.Ready, false, true);
            }
            catch (Exception ex)
            {
                await SetPropertyAsync(nameof(State), ComponentState.Error, false, true);
                throw;
            }
        }

        protected virtual Task OnInitializeAsync(CancellationToken ct) => Task.CompletedTask;

        public virtual async Task StartAsync(CancellationToken ct = default)
        {
            if (State != ComponentState.Ready)
                throw new InvalidOperationException($"Cannot start device in state {State}");

            await SetPropertyAsync(nameof(State), ComponentState.Running, false, true);

            try
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _executionCts.Token);
                await OnStartAsync(linkedCts.Token);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Normal cancellation
            }
            catch (Exception)
            {
                await SetPropertyAsync(nameof(State), ComponentState.Error, false, true);
                throw;
            }
        }

        protected virtual Task OnStartAsync(CancellationToken ct) => Task.CompletedTask;

        public virtual async Task StopAsync(CancellationToken ct = default)
        {
            if (State != ComponentState.Running)
                return;

            await SetPropertyAsync(nameof(State), ComponentState.Stopping, false, true);
            _executionCts.Cancel();

            try
            {
                await OnStopAsync(ct);
                await SetPropertyAsync(nameof(State), ComponentState.Ready, false, true);
            }
            catch (Exception)
            {
                await SetPropertyAsync(nameof(State), ComponentState.Error, false, true);
                throw;
            }
        }

        protected virtual Task OnStopAsync(CancellationToken ct) => Task.CompletedTask;

        public override void Dispose()
        {
            _executionCts.Dispose();
            base.Dispose();
        }
    }
}