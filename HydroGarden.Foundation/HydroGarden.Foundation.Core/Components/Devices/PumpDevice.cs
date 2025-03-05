using HydroGarden.Foundation.Abstractions.Interfaces;
using HydroGarden.Foundation.Abstractions.Interfaces.Logging;
using HydroGarden.Foundation.Common.PropertyMetadata;

namespace HydroGarden.Foundation.Core.Components.Devices
{
    /// <summary>
    /// Represents a pump device in the HydroGarden system.
    /// </summary>
    public class PumpDevice : IoTDeviceBase
    {
        private readonly Timer _monitorTimer;

        /// <summary>
        /// Gets or sets the flow rate of the pump.
        /// </summary>
        public double FlowRate { get; private set; }

        /// <summary>
        /// Gets or sets whether the pump is running.
        /// </summary>
        public bool IsRunning { get; private set; }

        /// <summary>
        /// Gets the maximum flow rate of the pump.
        /// </summary>
        public double MaxFlowRate { get; }

        /// <summary>
        /// Gets the minimum flow rate of the pump.
        /// </summary>
        public double MinFlowRate { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PumpDevice"/> class.
        /// </summary>
        public PumpDevice(Guid id, string name, double maxFlowRate = 100, double minFlowRate = 0, ILogger? logger = null)
            : base(id, name, logger)
        {
            MaxFlowRate = maxFlowRate;
            MinFlowRate = minFlowRate;
            _monitorTimer = new Timer(OnMonitorTimer, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }

        /// <summary>
        /// Sets the flow rate of the pump asynchronously.
        /// </summary>
        public async Task SetFlowRateAsync(double value)
        {
            if (value < MinFlowRate || value > MaxFlowRate)
                throw new ArgumentOutOfRangeException(nameof(value), $"Flow rate must be between {MinFlowRate} and {MaxFlowRate}");

            FlowRate = value;
            await SetPropertyAsync("FlowRate", FlowRate, GetDefaultPropertyMetadata("FlowRate"));
        }

        /// <inheritdoc/>
        protected override async Task OnInitializeAsync(CancellationToken ct)
        {
            FlowRate = 0;
            IsRunning = false;

            // ✅ Set default properties with metadata
            await SetPropertyAsync("FlowRate", FlowRate, GetDefaultPropertyMetadata("FlowRate"));
            await SetPropertyAsync("IsRunning", IsRunning, GetDefaultPropertyMetadata("IsRunning"));
            await SetPropertyAsync("MaxFlowRate", MaxFlowRate, GetDefaultPropertyMetadata("MaxFlowRate"));
            await SetPropertyAsync("MinFlowRate", MinFlowRate, GetDefaultPropertyMetadata("MinFlowRate"));

            // ✅ Virtual property as per request
            await SetPropertyAsync("VirtualPropTest", "testProp", new PropertyMetadata(true, false, "Virtual Prop Test", "A test virtual property"));

            await base.OnInitializeAsync(ct);
        }

        /// <inheritdoc/>
        protected override async Task OnStartAsync(CancellationToken ct)
        {
            IsRunning = true;
            await SetPropertyAsync("IsRunning", IsRunning, GetDefaultPropertyMetadata("IsRunning"));
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
                IsRunning = false;
                await SetPropertyAsync("IsRunning", IsRunning, GetDefaultPropertyMetadata("IsRunning"));
            }
        }

        /// <summary>
        /// Constructs the default property metadata for the Pump device.
        /// Overrides the base metadata for IoT devices with pump-specific values.
        /// </summary>
        /// <param name="name">The property name.</param>
        /// <param name="isEditable">Indicates whether the property is editable.</param>
        /// <param name="isVisible">Indicates whether the property is visible.</param>
        /// <returns>The default <see cref="IPropertyMetadata"/> for the property.</returns>
        public override IPropertyMetadata ConstructDefaultPropertyMetadata(string name, bool isEditable = true, bool isVisible = true)
        {
            // Retrieve base metadata from IoTDeviceBase
            var baseMetadata = base.ConstructDefaultPropertyMetadata(name, isEditable, isVisible);

            return name switch
            {
                "FlowRate" => new PropertyMetadata(true, true, "Flow Rate", "The percentage of pump flow rate"),
                "IsRunning" => new PropertyMetadata(false, true, "Pump Running", "Indicates if the pump is running"),
                "MaxFlowRate" => new PropertyMetadata(false, true, "Max Flow Rate", "The maximum possible flow rate"),
                "MinFlowRate" => new PropertyMetadata(false, true, "Min Flow Rate", "The minimum possible flow rate"),
                "CurrentFlowRate" => new PropertyMetadata(false, true, "Current Flow Rate", "The actual measured flow rate"),
                "Timestamp" => new PropertyMetadata(false, true, "Last Updated", "The last recorded update timestamp"),
                _ => baseMetadata
            };
        }


        /// <inheritdoc/>
        protected override async Task OnStopAsync(CancellationToken ct)
        {
            _monitorTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            IsRunning = false;
            await SetPropertyAsync("IsRunning", IsRunning, GetDefaultPropertyMetadata("IsRunning"));
            await base.OnStopAsync(ct);
        }

        private async Task SimulatePumpOperationAsync(CancellationToken ct)
        {
            double actualFlowRate = FlowRate;
            if (IsRunning && FlowRate > 0)
            {
                var random = new Random();
                actualFlowRate = FlowRate * (0.98 + 0.04 * random.NextDouble());
            }
            await SetPropertyAsync("CurrentFlowRate", actualFlowRate, GetDefaultPropertyMetadata("CurrentFlowRate"));
        }

        private async void OnMonitorTimer(object? state)
        {
            try
            {
                if (IsRunning)
                {
                    double actualFlowRate = FlowRate;
                    if (FlowRate > 0)
                    {
                        var random = new Random();
                        actualFlowRate = FlowRate * (0.98 + 0.04 * random.NextDouble());
                    }
                    await SetPropertyAsync("CurrentFlowRate", actualFlowRate, GetDefaultPropertyMetadata("CurrentFlowRate"));
                    await SetPropertyAsync("Timestamp", DateTime.UtcNow, GetDefaultPropertyMetadata("Timestamp"));
                }
            }
            catch (Exception ex)
            {
                _logger.Log(ex, "Error updating pump properties");
            }
        }

        /// <summary>
        /// Returns the default metadata for a given property.
        /// </summary>
        private IPropertyMetadata GetDefaultPropertyMetadata(string propertyName, bool isEditable = true,bool isVisible = true) =>
            propertyName switch
            {
                "FlowRate" => new PropertyMetadata(true, true, "Flow Rate", "The percentage of pump flow rate"),
                "IsRunning" => new PropertyMetadata(false, true, "Pump Running", "Indicates if the pump is running"),
                "MaxFlowRate" => new PropertyMetadata(false, true, "Max Flow Rate", "The maximum possible flow rate"),
                "MinFlowRate" => new PropertyMetadata(false, true, "Min Flow Rate", "The minimum possible flow rate"),
                "CurrentFlowRate" => new PropertyMetadata(false, true, "Current Flow Rate", "The actual measured flow rate"),
                "Timestamp" => new PropertyMetadata(false, true, "Last Updated", "The last recorded update timestamp"),
                _ => new PropertyMetadata(isEditable, isVisible, propertyName, $"Property {propertyName}")
            };

        /// <inheritdoc/>
        public override void Dispose()
        {
            _monitorTimer.Dispose();
            base.Dispose();
        }
    }
}
