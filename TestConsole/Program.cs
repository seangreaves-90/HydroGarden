using HydroGarden.Foundation.Core.Stores;

namespace TestConsole
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            // Create the store instance
            var store = new JsonPropertyStore(
                basePath: Environment.CurrentDirectory,
                logger: null
            );

            // Save properties
            await store.SaveAsync("device1", new Dictionary<string, object>
            {
                ["setting1"] = "value1",
                ["setting2"] = 42
            });

            // Load properties
            var properties = await store.LoadAsync("device1");
        }
    }
}
