using HydroGarden.Foundation.Abstractions.Interfaces.Components;
using HydroGarden.Foundation.Common.Events;
using HydroGarden.Foundation.Common.Events.RetryPolicies;
using HydroGarden.Foundation.Common.Events.Stores;
using HydroGarden.Foundation.Common.Events.Transforms;
using HydroGarden.Foundation.Common.Logging;
using HydroGarden.Foundation.Core.Components.Devices;
using HydroGarden.Foundation.Core.Services;
using HydroGarden.Foundation.Core.Stores;
using HydroGarden.Foundation.Common.PropertyMetadata;

namespace TestConsole
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("🌱 HydroGarden Pump Device Test Console 🌱");
            Console.WriteLine("=========================================");

            // Setup services
            var logger = new HydroGardenLogger();
            var storePath = Path.Combine(Directory.GetCurrentDirectory(), "DeviceData");
            var store = new JsonStore(storePath, logger);
            var eventBus = new EventBus(
                logger,
                new DeadLetterEventStore(),
                new ExponentialBackoffRetryPolicy(),
                new DefaultEventTransformer());
            var persistenceService = new PersistenceService(store, eventBus);

            try
            {
                await TestPumpDevice(logger, persistenceService);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Test failed with error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
            finally
            {
                await persistenceService.DisposeAsync();
            }

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }

        static async Task TestPumpDevice(HydroGardenLogger logger, PersistenceService persistenceService)
        {
            // Create pump device
            var pumpId = Guid.NewGuid();
            Console.WriteLine($"Creating pump with ID: {pumpId}");

            using var pump = new PumpDevice(pumpId, "Test Pump", 100, 0, logger);
            // Define property metadata
            var metadata = new PropertyMetadata(true, true, "Sean", "Greaves");
            await pump.SetPropertyAsync("Sean", 50, metadata);
            await persistenceService.AddOrUpdateAsync(pump);


            await DisplayPumpStatus(pump);

            // Set flow rate with metadata
            Console.WriteLine("\nSetting flow rate to 50%...");
            metadata = new PropertyMetadata(true, true, "Flow Rate", "The percentage of pump flow rate");
            await pump.SetPropertyAsync("FlowRate", 50, metadata);
            await DisplayPumpStatus(pump);

            // Start the pump asynchronously
            Console.WriteLine("\nStarting pump...");

            using var pumpCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var pumpCompletionSource = new TaskCompletionSource<bool>();

            _ = Task.Run(async () =>
            {
                try
                {
                    await pump.StartAsync(pumpCts.Token);
                    pumpCompletionSource.TrySetResult(true);
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("Pump operation was canceled as expected");
                    pumpCompletionSource.TrySetResult(true);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in pump operation: {ex.Message}");
                    pumpCompletionSource.TrySetException(ex);
                }
            });

            Console.WriteLine("Pump is running...");
            await Task.Delay(2000);
            await DisplayPumpStatus(pump);

            // Adjust flow rate while running
            Console.WriteLine("\nAdjusting flow rate to 75%...");
            await pump.SetPropertyAsync("FlowRate", 75, metadata);
            await Task.Delay(2000);
            await DisplayPumpStatus(pump);

            // Wait for user input to stop the pump manually
            Console.WriteLine("\nPress any key to stop the pump...");
            Console.ReadKey(true);

            Console.WriteLine("Stopping pump...");
            pumpCts.Cancel();
            await pump.StopAsync();
            await Task.WhenAny(pumpCompletionSource.Task, Task.Delay(3000));

            await DisplayPumpStatus(pump);
            Console.WriteLine("Pump test completed successfully!");
        }

        static async Task DisplayPumpStatus(PumpDevice pump)
        {
            Console.WriteLine("\n--- Pump Status ---");

            var flowRate = await pump.GetPropertyAsync<double>("FlowRate");
            var currentFlowRate = await pump.GetPropertyAsync<double>("CurrentFlowRate");
            var isRunning = await pump.GetPropertyAsync<bool>("IsRunning");
            var state = await pump.GetPropertyAsync<ComponentState>("State");
            var timestamp = await pump.GetPropertyAsync<DateTime?>("Timestamp");

            var metadata = pump.GetPropertyMetadata("FlowRate");

            Console.WriteLine($"State: {state}");
            Console.WriteLine($"Flow Rate Setting: {flowRate}%");
            Console.WriteLine($"Current Flow Rate: {currentFlowRate:F2}%");
            Console.WriteLine($"Running: {isRunning}");
            Console.WriteLine($"Last Update: {timestamp?.ToString() ?? "N/A"}");

            // Display metadata information
            if (metadata != null)
            {
                Console.WriteLine("--- Property Metadata ---");
                Console.WriteLine($"Display Name: {metadata.DisplayName}");
                Console.WriteLine($"Description: {metadata.Description}");
                Console.WriteLine($"Editable: {metadata.IsEditable}");
                Console.WriteLine($"Visible: {metadata.IsVisible}");
            }

            Console.WriteLine("------------------");
        }
    }
}