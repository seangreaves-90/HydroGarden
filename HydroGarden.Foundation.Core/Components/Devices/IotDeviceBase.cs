using HydroGarden.Foundation.Abstractions.Interfaces;
using HydroGarden.Foundation.Common.Logging;
using System.Threading;
using System.Threading.Tasks;

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

        protected virtual Task OnInitializeAsync(CancellationToken ct) => Task.CompletedTask;

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
                // Normal cancellation
            }
            catch (Exception)
            {
                await SetPropertyAsync(nameof(State), ComponentState.Error);
                throw;
            }
        }

        protected virtual Task OnStartAsync(CancellationToken ct) => Task.CompletedTask;

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

        protected virtual Task OnStopAsync(CancellationToken ct) => Task.CompletedTask;

        public override void Dispose()
        {
            _executionCts.Cancel();
            _executionCts.Dispose();
            base.Dispose();
        }
    }
}
