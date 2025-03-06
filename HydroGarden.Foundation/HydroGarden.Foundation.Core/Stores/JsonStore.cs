using System.Text.Json;
using System.Text.Json.Serialization;
using HydroGarden.Foundation.Abstractions.Interfaces;
using HydroGarden.Foundation.Abstractions.Interfaces.Logging;
using HydroGarden.Foundation.Abstractions.Interfaces.Services;
using HydroGarden.Foundation.Common.Logging;
using HydroGarden.Foundation.Common.PropertyMetadata;
using HydroGarden.Foundation.Core.Serialization;

namespace HydroGarden.Foundation.Core.Stores
{
    public class JsonStore : IStore
    {
        private readonly string _filePath;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private readonly JsonSerializerOptions _serializerOptions;
        private readonly ILogger _logger;

        public JsonStore(string basePath, ILogger logger)
        {
            string fullPath = Path.GetFullPath(basePath);
            _filePath = Path.Combine(fullPath, "ComponentProperties.json");
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);

            _serializerOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true,
                ReferenceHandler = ReferenceHandler.IgnoreCycles,
                Converters =
                {
                    new JsonStringEnumConverter(),
                    new PropertyMetadataConverter()
                }
            };

            _logger = logger ?? new Logger();
            _logger.Log($"JsonStore initialized with file path: {_filePath}");
        }

        public async Task<IStoreTransaction> BeginTransactionAsync(CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
                var store = File.Exists(_filePath)
                    ? await LoadStoreAsync(ct)
                    : new JsonStoreStructure();
                return new JsonStoreTransaction(this, store);
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<IDictionary<string, object>?> LoadAsync(Guid id, CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
                if (!File.Exists(_filePath)) return null;
                var store = await LoadStoreAsync(ct);
                var device = store.Devices.FirstOrDefault(d => d.Id == id);
                return device?.Properties.ToDictionary(
                    kvp => kvp.Key,
                    kvp => ConvertJsonElement(kvp.Value) // 🟢 Ensure we correctly convert properties
                );
            }
            finally
            {
                _lock.Release();
            }
        }


        private object ConvertJsonElement(object obj)
        {
            if (obj is JsonElement jsonElement)
            {
                return jsonElement.ValueKind switch
                {
                    JsonValueKind.String => jsonElement.GetString()?.Trim() ?? string.Empty,
                    JsonValueKind.Number => jsonElement.TryGetDouble(out double d) ? d : (double)jsonElement.GetInt64(), // Force double conversion
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Null => null!,
                    _ => jsonElement.GetRawText()
                };
            }
            return obj;
        }


        public async Task<IDictionary<string, IPropertyMetadata>?> LoadMetadataAsync(Guid id, CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
                if (!File.Exists(_filePath)) return null;
                var store = await LoadStoreAsync(ct);
                var device = store.Devices.FirstOrDefault(d => d.Id == id);
                return device?.Metadata.ToDictionary(
                    kvp => kvp.Key,
                    kvp => (IPropertyMetadata)ConvertJsonElement(kvp.Value) // 🟢 Convert metadata values correctly
                );
            }
            finally
            {
                _lock.Release();
            }
        }


        public async Task SaveAsync(Guid id, IDictionary<string, object> properties, CancellationToken ct = default)
        {
            await SaveWithMetadataAsync(id, properties, null, ct);
        }

        public async Task SaveWithMetadataAsync(Guid id, IDictionary<string, object> properties, IDictionary<string, IPropertyMetadata>? metadata, CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
                var store = File.Exists(_filePath)
                    ? await LoadStoreAsync(ct)
                    : new JsonStoreStructure();

                var existingDevice = store.Devices.FirstOrDefault(d => d.Id == id);
                if (existingDevice != null)
                {
                    existingDevice.Properties = new Dictionary<string, object>(properties);
                    if (metadata != null)
                    {
                        existingDevice.Metadata = new Dictionary<string, IPropertyMetadata>(metadata);
                    }
                }
                else
                {
                    store.Devices.Add(new DeviceStore
                    {
                        Id = id,
                        Properties = new Dictionary<string, object>(properties),
                        Metadata = metadata != null
                            ? new Dictionary<string, IPropertyMetadata>(metadata)
                            : new Dictionary<string, IPropertyMetadata>()
                    });
                }

                await SaveStoreAsync(store, ct);
            }
            finally
            {
                _lock.Release();
            }
        }

        private async Task<JsonStoreStructure> LoadStoreAsync(CancellationToken ct)
        {
            try
            {
                string json = await File.ReadAllTextAsync(_filePath, ct);
                return JsonSerializer.Deserialize<JsonStoreStructure>(json, _serializerOptions) ?? new JsonStoreStructure();
            }
            catch (JsonException? ex)
            {
                _logger.Log(ex, "Error parsing JSON file.");
                throw;
            }
        }

        public async Task SaveStoreAsync(JsonStoreStructure store, CancellationToken ct = default)
        {
            string tempFile = $"{_filePath}.tmp";
            try
            {
                string json = JsonSerializer.Serialize(store, _serializerOptions);
                await File.WriteAllTextAsync(tempFile, json, ct);
                File.Move(tempFile, _filePath, true);
            }
            catch (Exception? ex)
            {
                _logger.Log(ex, "Error saving store to file.");
                if (File.Exists(tempFile)) File.Delete(tempFile);
                throw;
            }
        }

        public class JsonStoreStructure
        {
            public List<DeviceStore> Devices { get; set; } = new();
        }

        public class DeviceStore
        {
            public Guid Id { get; set; }
            public Dictionary<string, object> Properties { get; set; } = new();
            public Dictionary<string, IPropertyMetadata> Metadata { get; set; } = new();
        }
    }
}
