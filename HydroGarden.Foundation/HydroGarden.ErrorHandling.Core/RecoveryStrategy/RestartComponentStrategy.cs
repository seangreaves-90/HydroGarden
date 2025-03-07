//using HydroGarden.ErrorHandling.Core.Abstractions;
//using HydroGarden.ErrorHandling.Core.Common;
//using HydroGarden.Foundation.Abstractions.Interfaces.Components;
//using HydroGarden.Foundation.Abstractions.Interfaces.Services;
//using HydroGarden.Logger.Abstractions;

//namespace HydroGarden.ErrorHandling.Core.RecoveryStrategy
//{
//    /// <summary>
//    /// Attempts to recover a device by restarting it.
//    /// </summary>
//    public class RestartDeviceStrategy(ILogger logger, IPersistenceService persistenceService) : RecoveryStrategyBase(logger)
//    {
//        private readonly IPersistenceService _persistenceService = persistenceService ?? throw new ArgumentNullException(nameof(persistenceService));

//        /// <summary>
//        /// Gets the name of this recovery strategy.
//        /// </summary>
//        public override string Name => "Device Restart";

//        /// <summary>
//        /// Restart is a high-priority strategy.
//        /// </summary>
//        public override int Priority => 10;

//        /// <summary>
//        /// This strategy can recover from device state errors.
//        /// </summary>
//        public override bool CanRecover(IApplicationError error)
//        {
//            if (error is not ComponentError componentError)
//                return false;

//            // Handle state transition failures and communication issues
//            return error.ErrorCode == ErrorCodes.Device.STATE_TRANSITION_FAILED ||
//                   error.ErrorCode == ErrorCodes.Device.COMMUNICATION_LOST ||
//                   (error.Source == ErrorSource.Device && componentError.IsRecoverable);
//        }

//        /// <summary>
//        /// Attempts to restart the device.
//        /// </summary>
//        protected override async Task<bool> ExecuteRecoveryAsync(IApplicationError error, CancellationToken ct)
//        {
//            try
//            {
//                // Try to retrieve the device
//                var device = await _persistenceService.GetPropertyAsync<IIoTDevice>(error.DeviceId, "Device", ct);
//                if (device == null)
//                {
//                    Logger.Log($"Device {error.DeviceId} not found for restart recovery");
//                    return false;
//                }

//                // Stop the device if it's running
//                if (device.State == ComponentState.Running)
//                {
//                    await device.StopAsync(ct);
//                }

//                // Wait a moment for resources to clean up
//                await Task.Delay(TimeSpan.FromSeconds(2), ct);

//                // Initialize and start the device
//                if (device.State == ComponentState.Error || device.State == ComponentState.Ready)
//                {
//                    await device.StartAsync(ct);
//                    return device.State == ComponentState.Running;
//                }

//                // Need to re-initialize
//                if (device.State != ComponentState.Ready)
//                {
//                    await device.InitializeAsync(ct);

//                    if (device.State == ComponentState.Ready)
//                    {
//                        await device.StartAsync(ct);
//                        return device.State == ComponentState.Running;
//                    }
//                }

//                return device.State == ComponentState.Ready || device.State == ComponentState.Running;
//            }
//            catch (Exception ex)
//            {
//                Logger.Log(ex, $"Error during device restart recovery for device {error.DeviceId}");
//                return false;
//            }
//        }
//    }
//}
