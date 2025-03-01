using HydroGarden.Foundation.Abstractions.Interfaces.Logging;
using HydroGarden.Foundation.Common.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace HydroGarden.Foundation.Core.Components.Devices
{
    /// <summary>
    /// Represents a pump device in the HydroGarden system.
    /// </summary>
    public class PumpDevice : IoTDeviceBase
    {
        private double _flowRate;
        private bool _isRunning;
        private readonly Timer _monitorTimer;
        private readonly double _maxFlowRate;
        private readonly double _minFlowRate;

        /// <summary>
        /// Initializes a new instance of the <see cref="PumpDevice"/> class.
        /// </summary>
        /// <param name="id">The unique identifier of the pump.</param>
        /// <param name="name">The name of the pump.</param>
        /// <param name="maxFlowRate">The maximum flow rate of the pump.</param>
        /// <param name="minFlowRate">The minimum flow rate of the pump.</param>
        /// <param name="logger">Optional logger instance.</param>
        public PumpDevice(Guid id, string name, double maxFlowRate = 100, double minFlowRate = 0, IHydroGardenLogger? logger = null)
            : base(id, name, logger)
        {
            _maxFlowRate = maxFlowRate;
            _minFlowRate = minFlowRate;
            _monitorTimer = new Timer(OnMonitorTimer, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }

        /// <summary>
        /// Sets the flow rate of the pump asynchronously.
        /// </summary>
        /// <param name="value">The desired flow rate.</param>
        public async Task SetFlowRateAsync(double value)
        {
            if (value < _minFlowRate || value > _maxFlowRate)
                throw new ArgumentOutOfRangeException(nameof(value), $"Flow rate must be between {_minFlowRate} and {_maxFlowRate}");

            _flowRate = value;
            await SetPropertyAsync("FlowRate", _flowRate);
        }

        /// <inheritdoc/>
        protected override async Task OnInitializeAsync(CancellationToken ct)
        {
            _flowRate = 0;
            _isRunning = false;
            await SetPropertyAsync("FlowRate", _flowRate);
            await SetPropertyAsync("IsRunning", _isRunning);
            await SetPropertyAsync("MaxFlowRate", _maxFlowRate);
            await SetPropertyAsync("MinFlowRate", _minFlowRate);
            await base.OnInitializeAsync(ct);
        }

        /// <inheritdoc/>
        protected override async Task OnStartAsync(CancellationToken ct)
        {
            _isRunning = true;
            await SetPropertyAsync("IsRunning", _isRunning);
            _monitorTimer.Change(TimeSpan.Zero, TimeSpan.FromSeconds(1));
            
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    await SimulatePumpOperationAsync(ct);
                    await Task.Delay(100, ct);
                }
            }
            finally
            {
                _monitorTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                _isRunning = false;
                await SetPropertyAsync("IsRunning", _isRunning);
            }
        }

        /// <inheritdoc/>
        protected override async Task OnStopAsync(CancellationToken ct)
        {
            _monitorTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            _isRunning = false;
            await SetPropertyAsync("IsRunning", _isRunning);
            await base.OnStopAsync(ct);
        }

        private async Task SimulatePumpOperationAsync(CancellationToken ct)
        {
            double actualFlowRate = _flowRate;
            if (_isRunning && _flowRate > 0)
            {
                var random = new Random();
                actualFlowRate = _flowRate * (0.98 + 0.04 * random.NextDouble());
            }
            await SetPropertyAsync("CurrentFlowRate", actualFlowRate);
        }

        private async void OnMonitorTimer(object? state)
        {
            try
            {
                if (_isRunning)
                {
                    double actualFlowRate = _flowRate;
                    if (_flowRate > 0)
                    {
                        var random = new Random();
                        actualFlowRate = _flowRate * (0.98 + 0.04 * random.NextDouble());
                    }
                    await SetPropertyAsync("CurrentFlowRate", actualFlowRate);
                    await SetPropertyAsync("Timestamp", DateTime.UtcNow);
                }
            }
            catch (Exception ex)
            {
                _logger.Log(ex, "Error updating pump properties");
            }
        }

        /// <inheritdoc/>
        public override void Dispose()
        {
            _monitorTimer.Dispose();
            base.Dispose();
        }
    }
}
