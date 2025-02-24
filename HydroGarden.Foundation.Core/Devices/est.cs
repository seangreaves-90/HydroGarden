using HydroGarden.Foundation.Abstractions.Interfaces;
using HydroGarden.Foundation.Common.Logging;

namespace HydroGarden.Foundation.Core.Devices
{
    public class PumpDevice : IoTDeviceBase
    {
        private double _flowRate;
        private bool _isRunning;
        private readonly Timer _monitorTimer;

        public PumpDevice(Guid id, string name) : this(id, name, new HydroGardenLogger())
        {
            
        }

        public PumpDevice(Guid id, string name, IHydroGardenLogger logger): base(id, name, DeviceType.Pump, logger)
        {
            _monitorTimer = new Timer(OnMonitorTimer, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }

        public async Task SetFlowRateAsync(double value)
        {
            if (value < 0 || value > 100)
                throw new ArgumentOutOfRangeException(nameof(value), "Flow rate must be between 0 and 100");

            _flowRate = value;
            await PublishPropertyAsync("FlowRate", _flowRate);
        }

        protected override async Task OnExecuteCoreAsync(CancellationToken ct)
        {
            _isRunning = true;
            await PublishPropertyAsync("IsRunning", _isRunning, isReadOnly: true);
            _monitorTimer.Change(TimeSpan.Zero, TimeSpan.FromSeconds(1));

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    // Simulate pump operation
                    await PublishPropertyAsync("FlowRate", _flowRate);
                    await Task.Delay(100, ct);
                }
            }
            finally
            {
                _monitorTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                _isRunning = false;
                await PublishPropertyAsync("IsRunning", _isRunning, isReadOnly: true);
            }
        }

        private async void OnMonitorTimer(object? state)
        {
            try
            {
                await PublishPropertyAsync("CurrentFlowRate", _flowRate, isReadOnly: true);
                await PublishPropertyAsync("Timestamp", DateTime.UtcNow, isReadOnly: true);
            }
            catch (Exception ex)
            {
                _logger.Log(ex, "Failed to publish monitoring data");
            }
        }

        public override void Dispose()
        {
            _monitorTimer.Dispose();
            base.Dispose();
        }
    }
}
