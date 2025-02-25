// HydroGarden.Foundation.Core.Components.Devices/PumpDevice.cs
using HydroGarden.Foundation.Abstractions.Interfaces;
using HydroGarden.Foundation.Common.Logging;

namespace HydroGarden.Foundation.Core.Components.Devices
{
    public class PumpDevice : IoTDeviceBase
    {
        private double _flowRate;
        private bool _isRunning;
        private readonly Timer _monitorTimer;
        private readonly double _maxFlowRate;
        private readonly double _minFlowRate;

        public PumpDevice(Guid id, string name, double maxFlowRate = 100, double minFlowRate = 0, IHydroGardenLogger? logger = null)
            : base(id, name, logger)
        {
            _maxFlowRate = maxFlowRate;
            _minFlowRate = minFlowRate;
            _monitorTimer = new Timer(OnMonitorTimer, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }

        public async Task SetFlowRateAsync(double value)
        {
            if (value < _minFlowRate || value > _maxFlowRate)
                throw new ArgumentOutOfRangeException(nameof(value), $"Flow rate must be between {_minFlowRate} and {_maxFlowRate}");

            _flowRate = value;
            await SetPropertyAsync("FlowRate", _flowRate, true, true, "Flow Rate", "Current flow rate setting (0-100%)");
        }

        protected override async Task OnInitializeAsync(CancellationToken ct)
        {
            // Initialize with default values
            _flowRate = 0;
            _isRunning = false;

            // Set the properties with metadata
            await SetPropertyAsync("FlowRate", _flowRate, true, true, "Flow Rate", "Current flow rate setting (0-100%)");
            await SetPropertyAsync("IsRunning", _isRunning, false, true, "Running Status", "Indicates if the pump is currently running");
            await SetPropertyAsync("MaxFlowRate", _maxFlowRate, false, true, "Maximum Flow Rate", "Maximum possible flow rate for this pump");
            await SetPropertyAsync("MinFlowRate", _minFlowRate, false, true, "Minimum Flow Rate", "Minimum possible flow rate for this pump");

            await base.OnInitializeAsync(ct);
        }

        protected override async Task OnStartAsync(CancellationToken ct)
        {
            _isRunning = true;
            await SetPropertyAsync("IsRunning", _isRunning, false, true);

            // Start the monitoring timer
            _monitorTimer.Change(TimeSpan.Zero, TimeSpan.FromSeconds(1));

            // Main pump operation loop
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    // Simulate pump operation - in a real device this would control hardware
                    await SimulatePumpOperationAsync(ct);
                    await Task.Delay(100, ct);
                }
            }
            finally
            {
                _monitorTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                _isRunning = false;
                await SetPropertyAsync("IsRunning", _isRunning, false, true);
            }
        }

        protected override async Task OnStopAsync(CancellationToken ct)
        {
            _monitorTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            _isRunning = false;
            await SetPropertyAsync("IsRunning", _isRunning, false, true);

            await base.OnStopAsync(ct);
        }

        private async Task SimulatePumpOperationAsync(CancellationToken ct)
        {
            // In a real implementation, this would control actual pump hardware
            // For simulation, we just periodically update the properties

            // Simulate some minor fluctuation in the actual flow rate
            double actualFlowRate = _flowRate;
            if (_isRunning && _flowRate > 0)
            {
                // Add small random variation to simulate real-world conditions
                var random = new Random();
                actualFlowRate = _flowRate * (0.98 + 0.04 * random.NextDouble());
            }

            await SetPropertyAsync("CurrentFlowRate", actualFlowRate, false, true,
                "Current Flow Rate", "Actual measured flow rate from sensors");
        }

        private async void OnMonitorTimer(object? state)
        {
            try
            {
                if (_isRunning)
                {
                    // Simulate sensor readings
                    double actualFlowRate = _flowRate;
                    if (_flowRate > 0)
                    {
                        // Add small random variation to simulate real-world conditions
                        var random = new Random();
                        actualFlowRate = _flowRate * (0.98 + 0.04 * random.NextDouble());
                    }

                    await SetPropertyAsync("CurrentFlowRate", actualFlowRate, false, true,
                        "Current Flow Rate", "Actual measured flow rate from sensors");
                    await SetPropertyAsync("Timestamp", DateTime.UtcNow, false, true,
                        "Last Update", "Timestamp of the most recent sensor reading");

                    // We could add more properties here like power consumption, temperature, etc.
                }
            }
            catch (Exception ex)
            {
                // Log error but don't crash the timer
                // In a real implementation, you might want to report this error via a dedicated property
                // await SetPropertyAsync("LastError", ex.Message, false, true, "Last Error", "Most recent error message");
            }
        }

        public override void Dispose()
        {
            _monitorTimer.Dispose();
            base.Dispose();
        }
    }
}