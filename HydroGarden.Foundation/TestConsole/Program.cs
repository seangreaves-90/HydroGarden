using HydroGarden.Foundation.Abstractions.Interfaces.Components;
using HydroGarden.Foundation.Common.Events;
using HydroGarden.Foundation.Common.Events.RetryPolicies;
using HydroGarden.Foundation.Common.Events.Stores;
using HydroGarden.Foundation.Common.Events.Transforms;
using HydroGarden.Foundation.Common.Logging;
using HydroGarden.Foundation.Core.Components.Devices;
using HydroGarden.Foundation.Core.Services;
using HydroGarden.Foundation.Core.Stores;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace HydroGarden.TestConsole
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("🌱 Starting HydroGarden Pump Device Simulation 🌱");
            Console.WriteLine("================================================");

            // Setup logger, storage, and persistence service
            var logger = new HydroGardenLogger();
            var store = new JsonStore(Path.Combine(Directory.GetCurrentDirectory(), "DeviceData"), logger);
            var persistenceService = new PersistenceService(store, new EventBus(logger,new DeadLetterEventStore(),new ExponentialBackoffRetryPolicy(), new DefaultEventTransformer()));

            // Simulating a Pump Device
            var pumpId = Guid.NewGuid();
            Console.WriteLine($"🔧 Creating pump device with ID: {pumpId}");
            var pump = new PumpDevice(pumpId, "Main Irrigation Pump", 100, 0, logger);

            // Register pump with the persistence service
            await persistenceService.AddOrUpdateAsync(pump);
            Console.WriteLine("✅ Pump registered with persistence service");

            Console.WriteLine("\n🚀 Initializing pump...");
            await pump.InitializeAsync();
            Console.WriteLine("✅ Pump initialized");

            Console.WriteLine("\n🔄 Setting initial flow rate to 40%...");
            await pump.SetFlowRateAsync(40);
            await DisplayPumpStatus(pump);

            Console.WriteLine("\n▶️ Starting pump...");
            await pump.StartAsync();
            Console.WriteLine("⏳ Pump running for 3 seconds...");
            await Task.Delay(3000);
            await DisplayPumpStatus(pump);

            Console.WriteLine("\n🔄 Changing flow rate to 75%...");
            await pump.SetFlowRateAsync(75);
            await Task.Delay(2000);
            await DisplayPumpStatus(pump);

            Console.WriteLine("\n⏹️ Stopping pump...");
            await pump.StopAsync();
            Console.WriteLine("✅ Pump stopped");
            await DisplayPumpStatus(pump);

            Console.WriteLine("\n🔄 Testing optimistic property updates...");
            bool success = await pump.UpdatePropertyOptimisticAsync<double>("FlowRate", current => current + 10);
            Console.WriteLine($"🔄 Optimistic update {(success ? "succeeded" : "failed")}");
            await DisplayPumpStatus(pump);

            // Simulate persistence by reloading the pump with the same ID
            Console.WriteLine("\n💾 Simulating a pump restart...");
            var reloadedPump = new PumpDevice(pumpId, "Reloaded Pump", 100, 0, logger);
            await persistenceService.AddOrUpdateAsync(reloadedPump);
            await reloadedPump.InitializeAsync();
            Console.WriteLine("\n🔁 Reloaded pump properties:");
            await DisplayPumpStatus(reloadedPump);

            // Test transactions
            Console.WriteLine("\n🔄 Testing persistence with transactions...");
            await using (var transaction = await store.BeginTransactionAsync())
            {
                var transactionProps = new Dictionary<string, object>
                {
                    { "TransactionTest", "Stored via transaction" },
                    { "Timestamp", DateTime.UtcNow }
                };
                await transaction.SaveAsync(pumpId, transactionProps);
                await transaction.CommitAsync();
                Console.WriteLine("✅ Transaction committed");
            }

            // Reload final pump after transaction
            var finalPump = new PumpDevice(pumpId, "Final Test Pump", 100, 0, logger);
            await persistenceService.AddOrUpdateAsync(finalPump);
            await finalPump.InitializeAsync();
            Console.WriteLine("\n🔁 Pump after transaction updates:");
            await DisplayPumpStatus(finalPump);

            // Cleanup
            pump.Dispose();
            reloadedPump.Dispose();
            finalPump.Dispose();
            await persistenceService.DisposeAsync();

            Console.WriteLine("\n✅ Simulation completed successfully!");
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        /// <summary>
        /// Displays the current status of a pump device.
        /// </summary>
        static async Task DisplayPumpStatus(PumpDevice pump)
        {
            Console.WriteLine("\n🔍 Pump Status:");
            Console.WriteLine("-----------------------------");

            var flowRate = await pump.GetPropertyAsync<double>("FlowRate");
            var currentFlowRate = await pump.GetPropertyAsync<double>("CurrentFlowRate");
            var isRunning = await pump.GetPropertyAsync<bool>("IsRunning");
            var state = await pump.GetPropertyAsync<ComponentState>("State");
            var timestamp = await pump.GetPropertyAsync<DateTime?>("Timestamp");

            Console.WriteLine($"📌 State: {state}");
            Console.WriteLine($"💧 Flow Rate Setting: {flowRate}%");
            Console.WriteLine($"💨 Current Flow Rate: {currentFlowRate:F2}%");
            Console.WriteLine($"⏳ Running: {isRunning}");
            Console.WriteLine($"🕒 Last Update: {timestamp?.ToString() ?? "N/A"}");

            // Show transaction test property if it exists
            var transactionTest = await pump.GetPropertyAsync<string>("TransactionTest");
            if (transactionTest != null)
            {
                Console.WriteLine($"🔄 Transaction Test: {transactionTest}");
            }

            Console.WriteLine("\n📌 All Pump Properties with Metadata:");
            var allMetadata = pump.GetAllPropertyMetadata();
            var allProps = pump.GetProperties();

            foreach (var prop in allProps)
            {
                var metadata = allMetadata.TryGetValue(prop.Key, out var meta) ? meta : null;
                string displayName = metadata?.DisplayName ?? prop.Key;
                string editableStatus = metadata?.IsEditable == true ? "Editable" : "Read-only";
                string description = metadata?.Description ?? "No description";

                Console.WriteLine($"- {displayName} ({prop.Key}): {prop.Value} [{editableStatus}]");
                Console.WriteLine($"  📝 Description: {description}");
            }

            Console.WriteLine("-----------------------------\n");
        }
    }
}
