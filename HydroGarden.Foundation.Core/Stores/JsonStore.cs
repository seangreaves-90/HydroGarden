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
    /// <summary>
    /// JsonStore provides a persistent key-value store using JSON file storage.
    /// </summary>
    public class JsonStore : IStore
    {
        private readonly string _filePath;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private readonly JsonSerializerOptions _serializerOptions;
        private readonly IHydroGardenLogger _logger;

        /// <summary>
        /// Initializes the JsonStore with a specified base path for storage and a logger.
        /// </summary>
        /// <param name="basePath">The directory where the store file will be saved.</param>
        /// <param name="logger">Logger for capturing store-related logs.</param>
        public JsonStore(string basePath, IHydroGardenLogger logger)
        {
            // Set up the storage file path
            string fullPath = Path.GetFullPath(basePath);
            _filePath = Path.Combine(fullPath, "ComponentProperties.json");

            // Ensure the directory exists
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);

            // Configure JSON serialization options
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

            // Assign logger, defaulting to a new instance if not provided
            _logger = logger ?? new HydroGardenLogger();
            _logger.Log($"JsonStore initialized with file path: {_filePath}");
        }

        /// <summary>
        /// Begins a new store transaction with thread safety.
        /// </summary>
        /// <param name="ct">Cancellation token for async operation.</param>
        /// <returns>An instance of JsonStoreTransaction.</returns>
        public async Task<IStoreTransaction> BeginTransactionAsync(CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
                // Load store data if file exists, otherwise create an empty store
                var store = File.Exists(_filePath)
                    ? await LoadStoreAsync(ct)
                    : new Dictionary<string, ComponentStore>();

                return new JsonStoreTransaction(this, store);
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Loads the stored properties for a specific component.
        /// </summary>
        /// <param name="id">The component ID.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Dictionary of properties if found, otherwise null.</returns>
        public async Task<IDictionary<string, object>?> LoadAsync(Guid id, CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
                if (!File.Exists(_filePath)) return null;

                var store = await LoadStoreAsync(ct);
                return store.TryGetValue(id.ToString(), out var component)
                    ? DeserializeComponentProperties(component.Properties)
                    : null;
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Loads metadata associated with a specific component.
        /// </summary>
        /// <param name="id">The component ID.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Dictionary of metadata if found, otherwise null.</returns>
        public async Task<IDictionary<string, IPropertyMetadata>?> LoadMetadataAsync(Guid id, CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
                if (!File.Exists(_filePath)) return null;

                var store = await LoadStoreAsync(ct);
                return store.TryGetValue(id.ToString(), out var component)
                    ? component.Metadata?.ToDictionary(kvp => kvp.Key, kvp => (IPropertyMetadata)kvp.Value)
                    : null;
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Saves properties for a component without metadata.
        /// </summary>
        public async Task SaveAsync(Guid id, IDictionary<string, object> properties, CancellationToken ct = default)
        {
            await SaveWithMetadataAsync(id, properties, null, ct);
        }

        /// <summary>
        /// Saves properties and metadata for a component.
        /// </summary>
        public async Task SaveWithMetadataAsync(Guid id, IDictionary<string, object> properties, IDictionary<string, IPropertyMetadata>? metadata, CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
                // Load existing store data or initialize a new store
                var store = File.Exists(_filePath)
                    ? await LoadStoreAsync(ct)
                    : new Dictionary<string, ComponentStore>();

                // Store properties and metadata
                store[id.ToString()] = new ComponentStore
                {
                    Properties = properties.ToDictionary(kvp => kvp.Key, kvp => kvp.Value is Type type ? type.FullName! : kvp.Value),
                    Metadata = metadata?.ToDictionary(kvp => kvp.Key, kvp => (IPropertyMetadata)new PropertyMetadata(
                        kvp.Value.IsEditable,
                        kvp.Value.IsVisible,
                        kvp.Value.DisplayName,
                        kvp.Value.Description
                    )) ?? new Dictionary<string, IPropertyMetadata>()
                };

                await SaveStoreAsync(store, ct);
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Loads the entire store from a JSON file.
        /// </summary>
        private async Task<Dictionary<string, ComponentStore>> LoadStoreAsync(CancellationToken ct)
        {
            try
            {
                string json = await File.ReadAllTextAsync(_filePath, ct);
                if (string.IsNullOrWhiteSpace(json))
                    throw new JsonException("Invalid JSON file: Empty content detected.");

                return JsonSerializer.Deserialize<Dictionary<string, ComponentStore>>(json, _serializerOptions) ?? new();
            }
            catch (JsonException ex)
            {
                _logger.Log(ex, "Error parsing JSON file.");
                throw;
            }
        }

        /// <summary>
        /// Saves the current store state to a JSON file.
        /// </summary>
        internal async Task SaveStoreAsync(Dictionary<string, ComponentStore> store, CancellationToken ct = default)
        {
            string tempFile = $"{_filePath}.tmp";
            try
            {
                string json = JsonSerializer.Serialize(store, _serializerOptions);
                await File.WriteAllTextAsync(tempFile, json, ct);
                File.Move(tempFile, _filePath, true);
            }
            catch (Exception ex)
            {
                _logger.Log(ex, "Error saving store to file.");
                if (File.Exists(tempFile)) File.Delete(tempFile);
                throw;
            }
        }

        /// <summary>
        /// Deserializes stored JSON properties into a usable dictionary.
        /// </summary>
        private Dictionary<string, object> DeserializeComponentProperties(Dictionary<string, object> properties)
        {
            return properties.ToDictionary(kvp => kvp.Key, kvp => kvp.Value is JsonElement jsonElement
                ? DeserializeJsonElement(jsonElement)
                : kvp.Value);
        }

        /// <summary>
        /// Converts JsonElement to a strongly typed object.
        /// </summary>
        private object DeserializeJsonElement(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => TryParseDateTime(element.GetString()) ??
                                        TryParseGuid(element.GetString()) ??
                                        TryParseTimeSpan(element.GetString()) ??
                                        (object?)TryParseType(element.GetString()) ??
                                        element.GetString()!,
                JsonValueKind.Number => element.TryGetDouble(out var doubleValue) ? doubleValue :
                                        element.TryGetInt64(out var longValue) ? longValue :
                                        element.GetDecimal(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => element.GetRawText()
            };
        }

        // Helper methods for type conversion
        private DateTime? TryParseDateTime(string? value) => DateTime.TryParse(value, null, System.Globalization.DateTimeStyles.RoundtripKind, out var result) ? result : null;
        private Guid? TryParseGuid(string? value) => Guid.TryParse(value, out var result) ? result : null;
        private TimeSpan? TryParseTimeSpan(string? value) => TimeSpan.TryParse(value, out var result) ? result : null;
        private Type? TryParseType(string? value) => !string.IsNullOrWhiteSpace(value) ? Type.GetType(value) : null;

        /// <summary>
        /// Represents a stored component with properties and metadata.
        /// </summary>
        public class ComponentStore
        {
            public Dictionary<string, object> Properties { get; set; } = new();
            public Dictionary<string, IPropertyMetadata> Metadata { get; set; } = new();
        }
    }
}
