

using HydroGarden.Foundation.Core.Devices;
using HydroGarden.Foundation.Core.Services;
using HydroGarden.Foundation.Core.Stores;

namespace TestConsole
{
//    Add validation rules for properties?
//Show how to implement property change notifications to external systems?
//Add batching of property updates for better performance?
//Demonstrate how to handle device configuration changes?


    internal class Program
    {
        static async Task Main(string[] args)
        {
            var deviceId = Guid.NewGuid();
            var store = new JsonStore(Path.Combine(Directory.GetCurrentDirectory(), "DeviceData"));
            var persistenceService = new PersistenceService(store);

     
            var pump = new PumpDevice(deviceId, "Main Nutrient Pump");

            await persistenceService.AddDeviceAsync(pump);
            await pump.InitializeAsync();
            await pump.SetFlowRateAsync(5);
            _ = pump.ExecuteCoreAsync(); 
            var flowRate = await persistenceService.GetPropertyAsync<double>(deviceId, "FlowRate");
            var isRunning = await persistenceService.GetPropertyAsync<bool>(deviceId, "IsRunning");
            var lastTimestamp = await persistenceService.GetPropertyAsync<DateTime>(deviceId, "Timestamp");

            Console.WriteLine($"Pump Status:");
            Console.WriteLine($"Flow Rate: {flowRate}");
            Console.WriteLine($"Running: {isRunning}");
            Console.WriteLine($"Last Update: {lastTimestamp}");

            // Stop the pump
            await pump.StopAsync();
            await Task.Delay(1000); // Wait for shutdown

            // Verify stopped state
            isRunning = await persistenceService.GetPropertyAsync<bool>(deviceId, "IsRunning");
            Console.WriteLine($"Final Running State: {isRunning}");

            pump.Dispose();
        }
    }
}
