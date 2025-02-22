using HydroGarden.Foundation.Abstractions.Interfaces;
using HydroGarden.Foundation.Common.Caching;
using HydroGarden.Foundation.Core.Devices;
using HydroGarden.Foundation.Core.PropertyManager;
using HydroGarden.Foundation.Core.Stores;

namespace TestConsole
{
    // A simple concrete IoTDevice for demonstration.
    public class DemoIoTDevice : IoTDeviceBase
    {
        public DemoIoTDevice(Guid id, string displayName, IPropertyManager properties, ILogger logger)
            : base(id, displayName, "DemoDevice", properties, logger)
        {
        }

        // For demo purposes, no real execution logic.
        public override Task ExecuteCoreAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    internal class Program
    {
        static async Task Main(string[] args)
        {
            // Define a directory for property storage.
            string basePath = Path.Combine(Directory.GetCurrentDirectory(), "DeviceProperties");
            Directory.CreateDirectory(basePath);

            // Create a JSON file-based property store.
            IPropertyStore store = new JsonPropertyStore(basePath, logger: null);

            // Create a SmartCache instance with a 5-minute sliding expiration.
            IPropertyCache cache = new SmartCache(TimeSpan.FromMinutes(5), maxSize: 100);

            // Create a PropertyManager for our device (with id "device1").
            IPropertyManager propManager = new PropertyManager("device1", store, cache, logger: null);

            // IMPORTANT: Load properties from the store into the PropertyManager.
            await propManager.LoadAsync(CancellationToken.None);

            // Create and initialize a new IoT device.
            Guid deviceId = Guid.NewGuid();
            DemoIoTDevice device = new DemoIoTDevice(deviceId, "Test Device", propManager, logger: null);

            // Initialization sets default properties: "Id", "DisplayName", "DeviceType".
            await device.InitializeAsync(CancellationToken.None);

            // Save the device properties to file via the PropertyManager.
            await device.SaveAsync(CancellationToken.None);
            Console.WriteLine("Device properties saved to file.");

            // --------------------------------------------------------------
            // Simulate a new device instance by creating a new PropertyManager,
            // with a fresh cache but using the same underlying store.
            IPropertyCache newCache = new SmartCache(TimeSpan.FromMinutes(5), maxSize: 100);
            IPropertyManager newPropManager = new PropertyManager("device1", store, newCache, logger: null);

            // IMPORTANT: Load properties from file into the new PropertyManager.
            await newPropManager.LoadAsync(CancellationToken.None);
            Console.WriteLine("Device properties loaded from file.");

            // Create a new IoT device instance using the loaded properties.
            DemoIoTDevice loadedDevice = new DemoIoTDevice(deviceId, "Test Device", newPropManager, logger: null);

            // Retrieve a property to observe cache usage.
            string? displayName = await newPropManager.GetPropertyAsync<string>("DisplayName", CancellationToken.None);
            Console.WriteLine($"Loaded DisplayName: {displayName}");

            // Update the "DisplayName" property and read it back to see caching in effect.
            await newPropManager.SetPropertyAsync("DisplayName", "Updated Test Device", isReadOnly: false, validator: null, CancellationToken.None);
            string? updatedDisplayName = await newPropManager.GetPropertyAsync<string>("DisplayName", CancellationToken.None);
            Console.WriteLine($"Updated DisplayName: {updatedDisplayName}");

            // At this point, subsequent calls to GetPropertyAsync("DisplayName") will hit the cache until expiration.
            Console.WriteLine("Demo complete. Press any key to exit.");
            Console.ReadKey();
        }
    }
}
