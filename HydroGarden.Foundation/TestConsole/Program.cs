using HydroGarden.Foundation.Abstractions.Interfaces.Components;
using HydroGarden.Foundation.Common.Events;
using HydroGarden.Foundation.Common.Events.RetryPolicies;
using HydroGarden.Foundation.Common.Events.Stores;
using HydroGarden.Foundation.Common.Events.Transforms;
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
            await persistenceService.AddOrUpdateAsync(pump);

            await DisplayPumpStatus(pump);

            // Set flow rate
            Console.WriteLine("\nSetting flow rate to 50%...");
            await pump.SetFlowRateAsync(50);
            await DisplayPumpStatus(pump);

            // Start the pump without blocking using CancellationTokenSource
            Console.WriteLine("\nStarting pump...");

            // Create a CancellationTokenSource with a timeout
            // This ensures the pump will automatically stop after the specified duration
            using var pumpCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            // Create a TaskCompletionSource to signal when pump operation is complete
            var pumpCompletionSource = new TaskCompletionSource<bool>();

            // Start the pump in a separate task
            _ = Task.Run(async () => {
                try
                {
                    // The pump will run until the token is canceled or an exception occurs
                    await pump.StartAsync(pumpCts.Token);
                    pumpCompletionSource.TrySetResult(true);
                }
                catch (OperationCanceledException)
                {
                    // This is expected when the token is canceled
                    Console.WriteLine("Pump operation was canceled as expected");
                    pumpCompletionSource.TrySetResult(true);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in pump operation: {ex.Message}");
                    pumpCompletionSource.TrySetException(ex);
                }
            });

            // Wait a moment for the pump to start running
            Console.WriteLine("Pump is running...");
            await Task.Delay(2000);
            await DisplayPumpStatus(pump);

            // Simulate adjusting flow rate while running
            Console.WriteLine("\nAdjusting flow rate to 75%...");
            await pump.SetFlowRateAsync(75);
            await Task.Delay(2000);
            await DisplayPumpStatus(pump);

            // Wait for user input to stop the pump manually
            Console.WriteLine("\nPress any key to stop the pump...");
            Console.ReadKey(true);

            // Stop the pump
            Console.WriteLine("Stopping pump...");

            // Cancel the token to signal the pump to stop
            pumpCts.Cancel();

            // Also call StopAsync to ensure proper shutdown
            await pump.StopAsync();

            // Wait for the pump operation to complete
            await Task.WhenAny(pumpCompletionSource.Task, Task.Delay(3000));

            // Show final status
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

            Console.WriteLine($"State: {state}");
            Console.WriteLine($"Flow Rate Setting: {flowRate}%");
            Console.WriteLine($"Current Flow Rate: {currentFlowRate:F2}%");
            Console.WriteLine($"Running: {isRunning}");
            Console.WriteLine($"Last Update: {timestamp?.ToString() ?? "N/A"}");
            Console.WriteLine("------------------");
        }
    }
}