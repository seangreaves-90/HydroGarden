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
using System.Text.Json;
using HydroGarden.Foundation.Abstractions.Interfaces;

namespace TestConsole
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("🌱 HydroGarden Device Test Console 🌱");
            Console.WriteLine("=========================================");

            // Setup services
            var logger = new Logger();
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
                await LoadOrCreateDevices(logger, persistenceService);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
            finally
            {
                await persistenceService.DisposeAsync();
            }

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }

        /// <summary>
        /// Loads existing devices from the JSON store or creates a new one if no file exists.
        /// </summary>
        static async Task LoadOrCreateDevices(Logger logger, PersistenceService persistenceService)
        {
            Console.WriteLine("\n🔍 Checking for stored devices...");

            var deviceDataPath = Path.Combine(Directory.GetCurrentDirectory(), "DeviceData", "ComponentProperties.json");
            List<PumpDevice> devices = new();

            if (File.Exists(deviceDataPath))
            {
                Console.WriteLine("📂 Found existing device data. Loading devices...");
                devices = await LoadDevicesFromFile(deviceDataPath, logger);
            }

            if (devices.Count == 0)
            {
                Console.WriteLine("❌ No devices found. Creating a new pump device...");
                var newPump = await CreateNewPumpDevice(logger, persistenceService);
                devices.Add(newPump);
            }

            Console.WriteLine("\n📋 Available Devices:");
            for (int i = 0; i < devices.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {devices[i].Name} (ID: {devices[i].Id})");
            }

            Console.Write("\nEnter the number of the device to test or 'N' to create a new one: ");
            var input = Console.ReadLine();

            if (input?.Trim().ToUpper() == "N")
            {
                var newPump = await CreateNewPumpDevice(logger, persistenceService);
                await TestPumpDevice(newPump, persistenceService);
            }
            else if (int.TryParse(input, out int choice) && choice > 0 && choice <= devices.Count)
            {
                var selectedDevice = devices[choice - 1];
                Console.WriteLine($"\n✅ Selected Device: {selectedDevice.Name}");
                await TestPumpDevice(selectedDevice, persistenceService);
            }
            else
            {
                Console.WriteLine("❌ Invalid selection. Exiting...");
            }
        }

        /// <summary>
        /// Reads stored devices from the JSON file.
        /// </summary>
        static async Task<List<PumpDevice>> LoadDevicesFromFile(string filePath, Logger logger)
        {
            var devices = new List<PumpDevice>();

            try
            {
                // Read the JSON file content
                var json = await File.ReadAllTextAsync(filePath);
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;

                // Ensure the JSON contains a "Devices" array
                if (root.TryGetProperty("Devices", out var devicesElement) && devicesElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var deviceElement in devicesElement.EnumerateArray())
                    {
                        // Extract device ID
                        var id = Guid.Parse(deviceElement.GetProperty("Id").GetString() ?? Guid.NewGuid().ToString());

                        // Extract device name
                        var name = deviceElement.GetProperty("Properties").GetProperty("Name").GetString() ?? "Unknown Device";

                        // Deserialize properties dictionary
                        var properties = JsonSerializer.Deserialize<Dictionary<string, object>>(
                            deviceElement.GetProperty("Properties").GetRawText()) ?? new();

                        // Deserialize metadata into Dictionary<string, PropertyMetadata>
                        var metadata = JsonSerializer.Deserialize<Dictionary<string, PropertyMetadata>>(
                            deviceElement.GetProperty("Metadata").GetRawText()) ?? new();

                        // 🔥 Convert Dictionary<string, PropertyMetadata> to IDictionary<string, IPropertyMetadata>
                        IDictionary<string, IPropertyMetadata> convertedMetadata = metadata.ToDictionary(
                            kvp => kvp.Key,
                            kvp => (IPropertyMetadata)kvp.Value // ✅ Explicitly cast PropertyMetadata to IPropertyMetadata
                        );

                        // Create a new PumpDevice instance
                        var pump = new PumpDevice(id, name, 100, 0, logger);

                        // Load the stored properties and metadata into the pump device
                        await pump.LoadPropertiesAsync(properties, convertedMetadata);

                        // Add the pump device to the list
                        devices.Add(pump);

                        Console.WriteLine($"✅ Loaded Device: {name} (ID: {id})");
                    }
                }
                else
                {
                    Console.WriteLine("❌ No 'Devices' array found in JSON.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error loading devices from file: {ex.Message}");
            }

            return devices;
        }



        /// <summary>
        /// Creates a new pump device, saves it, and returns it.
        /// </summary>
        static async Task<PumpDevice> CreateNewPumpDevice(Logger logger, PersistenceService persistenceService)
        {
            var pumpId = Guid.NewGuid();
            var pump = new PumpDevice(pumpId, "Test Pump", 100, 0, logger);
            await persistenceService.AddOrUpdateAsync(pump);
            Console.WriteLine($"✅ New pump device created with ID: {pumpId}");
            return pump;
        }

        /// <summary>
        /// Runs tests on a selected pump device.
        /// </summary>
        static async Task TestPumpDevice(PumpDevice pump, PersistenceService persistenceService)
        {
            await DisplayPumpStatus(pump);

            Console.WriteLine("\n🔄 Setting flow rate to 50%...");
            var metadata = new PropertyMetadata(true, true, "Flow Rate", "The percentage of pump flow rate");
            await pump.SetPropertyAsync("FlowRate", 50, metadata);
            await persistenceService.AddOrUpdateAsync(pump);
            await DisplayPumpStatus(pump);

            Console.WriteLine("\n▶️ Starting pump...");
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
                    Console.WriteLine("⏹ Pump operation was canceled.");
                    pumpCompletionSource.TrySetResult(true);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Pump error: {ex.Message}");
                    pumpCompletionSource.TrySetException(ex);
                }
            });

            await Task.Delay(2000);
            await DisplayPumpStatus(pump);

            Console.WriteLine("\n⏹ Press any key to stop the pump...");
            Console.ReadKey(true);

            Console.WriteLine("⏹ Stopping pump...");
            pumpCts.Cancel();
            await pump.StopAsync();
            await Task.WhenAny(pumpCompletionSource.Task, Task.Delay(3000));

            await DisplayPumpStatus(pump);
            await persistenceService.AddOrUpdateAsync(pump);
            Console.WriteLine("✅ Pump test completed successfully!");
        }

        /// <summary>
        /// Displays the current status of a pump device.
        /// </summary>
        static async Task DisplayPumpStatus(PumpDevice pump)
        {
            Console.WriteLine("\n--- 🔍 Pump Status ---");
            Console.WriteLine($"📌 State: {await pump.GetPropertyAsync<ComponentState>("State")}");
            Console.WriteLine($"💧 Flow Rate: {await pump.GetPropertyAsync<double>("FlowRate")}%");
            Console.WriteLine($"⚡ Current Flow: {await pump.GetPropertyAsync<double>("CurrentFlowRate")}%");
            Console.WriteLine($"▶️ Running: {await pump.GetPropertyAsync<bool>("IsRunning")}");
            Console.WriteLine("----------------------");
        }
    }
}
