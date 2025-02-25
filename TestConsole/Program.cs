// TestProgram.cs
using HydroGarden.Foundation.Abstractions.Interfaces;
using HydroGarden.Foundation.Common.Logging;
using HydroGarden.Foundation.Core.Components.Devices;
using HydroGarden.Foundation.Core.Services;
using HydroGarden.Foundation.Core.Stores;

namespace HydroGarden.TestConsole
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Starting HydroGarden Pump Device Test");
            Console.WriteLine("=====================================");

            // Create a logger
            var logger = new HydroGardenLogger();

            // Create a store for persistence
            var store = new JsonStore(Path.Combine(Directory.GetCurrentDirectory(), "DeviceData"));

            // Create a persistence service that will handle property changes
            var persistenceService = new PersistenceService(store, logger);

            // Create a pump device with a unique ID
            var pumpId = Guid.NewGuid();
            Console.WriteLine($"Creating pump with ID: {pumpId}");
            var pump = new PumpDevice(pumpId, "Main Nutrient Pump", 100, 0, logger);

            // Register the device with the persistence service
            await persistenceService.AddDeviceAsync(pump);
            Console.WriteLine("Pump registered with persistence service");

            // Initialize the device
            Console.WriteLine("Initializing pump...");
            await pump.InitializeAsync();
            Console.WriteLine("Pump initialized");

            // Set the flow rate
            Console.WriteLine("Setting flow rate to 50%...");
            await pump.SetFlowRateAsync(50);

            // Display current properties
            await DisplayPumpStatus(pump, persistenceService);

            // Start the pump
            Console.WriteLine("Starting pump...");
            var pumpTask = pump.StartAsync();

            // Wait a moment for the pump to run
            Console.WriteLine("Pump running for 3 seconds...");
            await Task.Delay(3000);

            // Display updated properties
            await DisplayPumpStatus(pump, persistenceService);

            // Change flow rate while running
            Console.WriteLine("Changing flow rate to 75%...");
            await pump.SetFlowRateAsync(75);
            await Task.Delay(2000);

            // Display updated properties again
            await DisplayPumpStatus(pump, persistenceService);

            // Stop the pump
            Console.WriteLine("Stopping pump...");
            await pump.StopAsync();
            Console.WriteLine("Pump stopped");

            // Final status
            await DisplayPumpStatus(pump, persistenceService);

            // Let's demonstrate loading properties from storage
            Console.WriteLine("\nTesting device properties persistence...");

            // Create a new instance with the same ID to simulate app restart
            Console.WriteLine("Creating new pump instance with same ID to simulate restart");
            var reloadedPump = new PumpDevice(pumpId, "Reloaded Pump", 100, 0, logger);

            // Register with persistence service (which will load saved properties)
            await persistenceService.AddDeviceAsync(reloadedPump);

            // Initialize it
            await reloadedPump.InitializeAsync();

            // Show the reloaded properties
            Console.WriteLine("Reloaded pump properties:");
            await DisplayPumpStatus(reloadedPump, persistenceService);

            // Clean up
            pump.Dispose();
            reloadedPump.Dispose();

            Console.WriteLine("\nTest completed");
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        static async Task DisplayPumpStatus(PumpDevice pump, PersistenceService persistenceService)
        {
            Console.WriteLine("\nPump Status:");
            Console.WriteLine("------------");

            var flowRate = await pump.GetPropertyAsync<double>("FlowRate");
            var currentFlowRate = await pump.GetPropertyAsync<double>("CurrentFlowRate");
            var isRunning = await pump.GetPropertyAsync<bool>("IsRunning");
            var state = await pump.GetPropertyAsync<ComponentState>("State");
            var timestamp = await pump.GetPropertyAsync<DateTime?>("Timestamp");

            Console.WriteLine($"State: {state}");
            Console.WriteLine($"Flow Rate Setting: {flowRate}%");
            Console.WriteLine($"Current Flow Rate: {currentFlowRate.ToString("F2") ?? "N/A"}%");
            Console.WriteLine($"Running: {isRunning}");
            Console.WriteLine($"Last Update: {timestamp?.ToString() ?? "N/A"}");

            // Show all properties with metadata
            Console.WriteLine("\nAll Pump Properties with Metadata:");
            var allMetadata = pump.GetAllPropertyMetadata();
            var allProps = pump.GetProperties();

            foreach (var prop in allProps)
            {
                var metadata = allMetadata.TryGetValue(prop.Key, out var meta) ? meta : null;
                string displayName = metadata?.DisplayName ?? prop.Key;
                string editableStatus = metadata?.IsEditable == true ? "Editable" : "Read-only";
                string description = metadata?.Description ?? "No description";

                Console.WriteLine($"- {displayName} ({prop.Key}): {prop.Value} [{editableStatus}]");
                Console.WriteLine($"  Description: {description}");
            }

            Console.WriteLine();
        }
    }
}