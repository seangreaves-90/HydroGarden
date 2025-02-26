using System.Text.Json;
using System.Text.Json.Serialization;
using HydroGarden.Foundation.Abstractions.Interfaces;
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
        private readonly IHydroGardenLogger _logger;

        public JsonStore(string basePath, IHydroGardenLogger logger)
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

            _logger = logger ?? new HydroGardenLogger();
            _logger.Log($"JsonStore initialized with file path: {_filePath}");
        }

        public async Task<IStoreTransaction> BeginTransactionAsync(CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
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

        public async Task<IDictionary<string, object>?> LoadAsync(Guid id, CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
                if (!File.Exists(_filePath)) return null;

                var store = await LoadStoreAsync(ct);
                if (store.TryGetValue(id.ToString(), out var component))
                {
                    return DeserializeComponentProperties(component.Properties);
                }

                return null;
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<IDictionary<string, IPropertyMetadata>?> LoadMetadataAsync(Guid id, CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
                if (!File.Exists(_filePath)) return null;

                var store = await LoadStoreAsync(ct);
                if (store.TryGetValue(id.ToString(), out var component))
                {
                    return component.Metadata?.ToDictionary(kvp => kvp.Key, kvp => (IPropertyMetadata)kvp.Value);
                }

                return null;
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
                    : new Dictionary<string, ComponentStore>();
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

        private Dictionary<string, object> DeserializeComponentProperties(Dictionary<string, object> properties)
        {
            return properties.ToDictionary(kvp => kvp.Key, kvp => kvp.Value is JsonElement jsonElement
                ? DeserializeJsonElement(jsonElement)
                : kvp.Value);
        }

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

        // Convert DateTime strings back to DateTime
        private DateTime? TryParseDateTime(string? value)
        {
            return DateTime.TryParse(value, null, System.Globalization.DateTimeStyles.RoundtripKind, out var result)
                ? result
                : null;
        }

        // Convert GUID strings back to GUID
        private Guid? TryParseGuid(string? value)
        {
            return Guid.TryParse(value, out var result) ? result : null;
        }

        // Convert TimeSpan strings back to TimeSpan
        private TimeSpan? TryParseTimeSpan(string? value)
        {
            return TimeSpan.TryParse(value, out var result) ? result : null;
        }

        // Convert Type strings back to Type
        private Type? TryParseType(string? value)
        {
            return !string.IsNullOrWhiteSpace(value) ? Type.GetType(value) : null;
        }


        public class ComponentStore
        {
            public Dictionary<string, object> Properties { get; set; } = new();
            public Dictionary<string, IPropertyMetadata> Metadata { get; set; } = new();

        }
    }


    public class JsonStoreTransaction : IStoreTransaction
    {
        private readonly JsonStore _store;
        private readonly Dictionary<string, JsonStore.ComponentStore> _workingState;
        private bool _isCommitted;
        private bool _isRolledBack;
        private bool _isDisposed;

        internal JsonStoreTransaction(JsonStore store, Dictionary<string, JsonStore.ComponentStore> currentState)
        {
            _store = store;
            _workingState = new Dictionary<string, JsonStore.ComponentStore>(currentState);
        }

        public Task SaveAsync(Guid id, IDictionary<string, object> properties)
        {
            var componentId = id.ToString();
            _workingState[componentId] = new JsonStore.ComponentStore
            {
                Properties = new Dictionary<string, object>(properties)
            };
            return Task.CompletedTask;
        }

        public Task SaveWithMetadataAsync(Guid id, IDictionary<string, object> properties, IDictionary<string, IPropertyMetadata>? metadata)
        {
            var componentId = id.ToString();
            _workingState[componentId] = new JsonStore.ComponentStore
            {
                Properties = new Dictionary<string, object>(properties),
                Metadata = metadata?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value) ?? new Dictionary<string, IPropertyMetadata>()
            };
            return Task.CompletedTask;
        }

        public async Task CommitAsync(CancellationToken ct = default)
        {
            if (_isCommitted || _isRolledBack) throw new InvalidOperationException("Transaction already finalized.");
            await _store.SaveStoreAsync(_workingState, ct);
            _isCommitted = true;
        }

        public Task RollbackAsync(CancellationToken ct = default)
        {
            _isRolledBack = true;
            return Task.CompletedTask;
        }

        public async ValueTask DisposeAsync()
        {
            if (_isDisposed) return;
            if (!_isCommitted && !_isRolledBack) await RollbackAsync();
            _isDisposed = true;
        }
    }
}
