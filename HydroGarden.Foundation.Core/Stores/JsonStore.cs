using System.Text.Json;
using System.Text.Json.Serialization;
using HydroGarden.Foundation.Abstractions.Interfaces;

namespace HydroGarden.Foundation.Core.Stores
{
    public class JsonStore : IStore
    {
        private readonly string _filePath;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private readonly JsonSerializerOptions _serializerOptions;

        public JsonStore(string basePath)
        {
            _filePath = Path.Combine(Path.GetFullPath(basePath), "DeviceProperties.json");
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);

            _serializerOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() }
            };
        }

        public async Task<IDictionary<Guid, object>?> LoadAsync(Guid id, CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
                var devices = await LoadDevicesAsync(ct);
                return devices.TryGetValue(id, out var device) ? device.Properties : null;
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<IDictionary<string, object>?> LoadAllAsync(CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
                var devices = await LoadDevicesAsync(ct);
                return devices.ToDictionary(
                    d => d.Key,
                    d => (object)new
                    {
                        Properties = d.Value.Properties
                    });
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task SaveAsync(Guid id, IDictionary<string, object> data, CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
                var devices = await LoadDevicesAsync(ct);


                // Convert IDictionary to Dictionary
                var propertyDictionary = new Dictionary<string, object>(data);

                devices[id.ToString()] = new DeviceData
                {
       
                    Properties = propertyDictionary
                };

                await SaveDevicesAsync(devices, ct);
            }
            finally
            {
                _lock.Release();
            }
        }

        private async Task<Dictionary<Guid, DeviceData>> LoadDevicesAsync(CancellationToken ct)
        {
            if (!File.Exists(_filePath))
                return new Dictionary<Guid, DeviceData>();

            var json = await File.ReadAllTextAsync(_filePath, ct);
            return JsonSerializer.Deserialize<Dictionary<Guid, DeviceData>>(json, _serializerOptions)
                   ?? new Dictionary<Guid, DeviceData>();
        }

        private async Task SaveDevicesAsync(Dictionary<string, DeviceData> devices, CancellationToken ct)
        {
            var json = JsonSerializer.Serialize(devices, _serializerOptions);
            await File.WriteAllTextAsync(_filePath, json, ct);
        }

        private class DeviceData
        {
            public Dictionary<string, object> Properties { get; set; } = new();
        }
    }
}