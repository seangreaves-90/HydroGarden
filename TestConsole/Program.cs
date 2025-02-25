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

            // Initialize components
            var logger = new HydroGardenLogger();
            var store = new JsonStore(Path.Combine(Directory.GetCurrentDirectory(), "DeviceData"),new HydroGardenLogger());
            var persistenceService = new PersistenceService(store, logger, 100, TimeSpan.FromSeconds(1));

            // Create a unique ID for the pump
            var pumpId = Guid.NewGuid();

            Console.WriteLine($"Creating pump with ID: {pumpId}");
            var pump = new PumpDevice(pumpId, "Main Nutrient Pump", 100, 0, logger);
            await persistenceService.AddDeviceAsync(pump);
            Console.WriteLine("Pump registered with persistence service");

            // Initialize the pump
            Console.WriteLine("Initializing pump...");
            await pump.InitializeAsync();
            Console.WriteLine("Pump initialized");

            // Set flow rate
            Console.WriteLine("Setting flow rate to 50%...");
            await pump.SetFlowRateAsync(50);
            await DisplayPumpStatus(pump, persistenceService);

            // Start the pump
            Console.WriteLine("Starting pump...");
            var pumpTask = pump.StartAsync();
            Console.WriteLine("Pump running for 3 seconds...");
            await Task.Delay(3000);
            await DisplayPumpStatus(pump, persistenceService);

            // Change flow rate while running
            Console.WriteLine("Changing flow rate to 75%...");
            await pump.SetFlowRateAsync(75);
            await Task.Delay(2000);
            await DisplayPumpStatus(pump, persistenceService);

            // Stop the pump
            Console.WriteLine("Stopping pump...");
            await pump.StopAsync();
            Console.WriteLine("Pump stopped");
            await DisplayPumpStatus(pump, persistenceService);

            // Test optimistic property updates
            Console.WriteLine("\nTesting optimistic property updates...");
            bool success = await pump.UpdatePropertyOptimisticAsync<double>("FlowRate", current => current + 10);
            Console.WriteLine($"Optimistic update {(success ? "succeeded" : "failed")}");
            await DisplayPumpStatus(pump, persistenceService);

            // Test persistence by creating a new pump with the same ID
            Console.WriteLine("\nTesting device properties persistence...");
            Console.WriteLine("Creating new pump instance with same ID to simulate restart");

            var reloadedPump = new PumpDevice(pumpId, "Reloaded Pump", 100, 0, logger);
            await persistenceService.AddDeviceAsync(reloadedPump);
            await reloadedPump.InitializeAsync();
            Console.WriteLine("Reloaded pump properties:");
            await DisplayPumpStatus(reloadedPump, persistenceService);

            // Test transaction support
            Console.WriteLine("\nTesting transaction support...");
            await using (var transaction = await store.BeginTransactionAsync())
            {
                var transactionProps = new Dictionary<string, object>
                {
                    { "TransactionTest", "This value was set in a transaction!" },
                    { "Timestamp", DateTime.UtcNow }
                };
                await transaction.SaveAsync(pumpId, transactionProps);
                Console.WriteLine("Transaction created and committed");
                await transaction.CommitAsync();
            }

            // Reload the pump to see transaction changes
            var finalPump = new PumpDevice(pumpId, "Final Test Pump", 100, 0, logger);
            await persistenceService.AddDeviceAsync(finalPump);
            await finalPump.InitializeAsync();
            Console.WriteLine("Pump after transaction changes:");
            await DisplayPumpStatus(finalPump, persistenceService);

            // Clean up resources
            pump.Dispose();
            reloadedPump.Dispose();
            finalPump.Dispose();
            await persistenceService.DisposeAsync();

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

            // Show transaction test property if it exists
            var transactionTest = await pump.GetPropertyAsync<string>("TransactionTest");
            if (transactionTest != null)
            {
                Console.WriteLine($"Transaction Test: {transactionTest}");
            }

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