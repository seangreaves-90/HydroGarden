using HydroGarden.Foundation.Abstractions.Interfaces;
using HydroGarden.Foundation.Abstractions.Interfaces.Components;
using HydroGarden.Foundation.Abstractions.Interfaces.Logging;
using HydroGarden.Foundation.Common.PropertyMetadata;
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
                // Use ConstructDefaultPropertyMetadata to get correct metadata for State
                var stateMetadata = ConstructDefaultPropertyMetadata(nameof(State));
                await SetPropertyAsync(nameof(State), ComponentState.Initializing, stateMetadata);

                await SetPropertyAsync("Id", Id);
                await SetPropertyAsync("Name", Name);
                await SetPropertyAsync("AssemblyType", AssemblyType);
                await OnInitializeAsync(ct);

                // Use ConstructDefaultPropertyMetadata again
                await SetPropertyAsync(nameof(State), ComponentState.Ready, stateMetadata);
            }
            catch (Exception ex)
            {
                _logger.Log(ex, "Failed to initialize device");

                // Use ConstructDefaultPropertyMetadata here too
                await SetPropertyAsync(nameof(State), ComponentState.Error,
                    ConstructDefaultPropertyMetadata(nameof(State)));
                throw;
            }
        }

        protected virtual Task OnInitializeAsync(CancellationToken ct) => Task.CompletedTask;

        public virtual async Task StartAsync(CancellationToken ct = default)
        {
            if (State != ComponentState.Ready)
                throw new InvalidOperationException($"Cannot start device in state {State}");

            // Use ConstructDefaultPropertyMetadata
            await SetPropertyAsync(nameof(State), ComponentState.Running,
                ConstructDefaultPropertyMetadata(nameof(State)));

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
                // Use ConstructDefaultPropertyMetadata
                await SetPropertyAsync(nameof(State), ComponentState.Error,
                    ConstructDefaultPropertyMetadata(nameof(State)));
                throw;
            }
        }

        protected virtual Task OnStartAsync(CancellationToken ct) => Task.CompletedTask;

        public virtual async Task StopAsync(CancellationToken ct = default)
        {
            if (State != ComponentState.Running)
                return;

            // Use ConstructDefaultPropertyMetadata
            await SetPropertyAsync(nameof(State), ComponentState.Stopping,
                ConstructDefaultPropertyMetadata(nameof(State)));

            _executionCts.Cancel();
            try
            {
                await OnStopAsync(ct);

                // Use ConstructDefaultPropertyMetadata
                await SetPropertyAsync(nameof(State), ComponentState.Ready,
                    ConstructDefaultPropertyMetadata(nameof(State)));
            }
            catch (Exception)
            {
                // Use ConstructDefaultPropertyMetadata
                await SetPropertyAsync(nameof(State), ComponentState.Error,
                    ConstructDefaultPropertyMetadata(nameof(State)));
                throw;
            }
        }

        protected virtual Task OnStopAsync(CancellationToken ct) => Task.CompletedTask;

        public override IPropertyMetadata ConstructDefaultPropertyMetadata(string name, bool isEditable = true, bool isVisible = true)
        {
            // Add device-specific property defaults
            var devicePropertyDefaults = new Dictionary<string, (bool IsEditable, bool IsVisible, string DisplayName, string Description)>
            {
                { "State", (false, true, "Device State", "The current state of the IoT device") },
                { "Id", (false, true, "Device ID", "The unique identifier of the IoT device") },
                { "Name", (true, true, "Device Name", "The name of the IoT device") }
            };

            // Try device-specific properties first
            if (devicePropertyDefaults.TryGetValue(name, out var defaults))
            {
                return new PropertyMetadata(
                    defaults.IsEditable,
                    defaults.IsVisible,
                    defaults.DisplayName,
                    defaults.Description);
            }

            // Otherwise, fall back to base implementation
            return base.ConstructDefaultPropertyMetadata(name, isEditable, isVisible);
        }

        public override void Dispose()
        {
            // Update State property with correct metadata
            SetPropertyAsync(nameof(State), ComponentState.Disposed,
                ConstructDefaultPropertyMetadata(nameof(State))).ConfigureAwait(false).GetAwaiter().GetResult();

            _executionCts.Cancel();
            _executionCts.Dispose();
            base.Dispose();
        }
    }
}