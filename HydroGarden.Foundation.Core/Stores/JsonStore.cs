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

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to create directory at {Path.GetDirectoryName(_filePath)}", ex);
            }

            _serializerOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true,
                ReferenceHandler = ReferenceHandler.IgnoreCycles,
                Converters =
                {
                    new JsonStringEnumConverter(),
                    new PropertyMetadataConverter(),
                    new ComponentPropertiesConverter()
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
                Dictionary<string, ComponentStore> store;
                if (File.Exists(_filePath))
                {
                    try
                    {
                        string json = await File.ReadAllTextAsync(_filePath, ct);
                        if (string.IsNullOrWhiteSpace(json))
                        {
                            store = new Dictionary<string, ComponentStore>();
                        }
                        else
                        {
                            store = JsonSerializer.Deserialize<Dictionary<string, ComponentStore>>(json, _serializerOptions) ?? new();
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.Log(ex, $"Error deserializing JSON store. Creating a new store.");
                        store = new Dictionary<string, ComponentStore>();
                    }
                }
                else
                {
                    store = new Dictionary<string, ComponentStore>();
                }
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
                try
                {
                    string json = await File.ReadAllTextAsync(_filePath, ct);
                    if (string.IsNullOrWhiteSpace(json)) return null;

                    var store = JsonSerializer.Deserialize<Dictionary<string, ComponentStore>>(json, _serializerOptions);
                    return store?.GetValueOrDefault(id.ToString())?.Properties;
                }
                catch (JsonException ex)
                {
                    _logger.Log(ex, $"Error deserializing JSON when loading properties for ID {id}");
                    return null;
                }
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
                if (!File.Exists(_filePath))
                {
                    _logger.Log($"[DEBUG] LoadMetadataAsync: File {_filePath} does not exist.");
                    return null;
                }

                try
                {
                    string json = await File.ReadAllTextAsync(_filePath, ct);
                    if (string.IsNullOrWhiteSpace(json))
                    {
                        _logger.Log($"[DEBUG] LoadMetadataAsync: JSON file is empty.");
                        return null;
                    }

                    var store = JsonSerializer.Deserialize<Dictionary<string, ComponentStore>>(json, _serializerOptions);
                    if (store == null || !store.ContainsKey(id.ToString()))
                    {
                        _logger.Log($"[DEBUG] LoadMetadataAsync: No entry found for ID {id}");
                        return null;
                    }

                    var metadata = store[id.ToString()].Metadata;
                    if (metadata == null || metadata.Count == 0)
                    {
                        _logger.Log($"[DEBUG] LoadMetadataAsync: Metadata is empty for ID {id}");
                        return null;
                    }

                    _logger.Log($"[DEBUG] LoadMetadataAsync: Successfully loaded metadata for ID {id}");
                    return metadata.ToDictionary(kvp => kvp.Key, kvp => (IPropertyMetadata)kvp.Value);
                }
                catch (JsonException ex)
                {
                    _logger.Log(ex, $"[ERROR] LoadMetadataAsync: JSON Parsing failed - {ex.Message}");
                    return null;
                }
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
        public async Task SaveWithMetadataAsync(Guid id, IDictionary<string, object> properties,
            IDictionary<string, IPropertyMetadata>? metadata, CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
                Dictionary<string, ComponentStore> store;
                if (File.Exists(_filePath))
                {
                    try
                    {
                        string json = await File.ReadAllTextAsync(_filePath, ct);
                        if (string.IsNullOrWhiteSpace(json))
                        {
                            store = new Dictionary<string, ComponentStore>();
                        }
                        else
                        {
                            store = JsonSerializer.Deserialize<Dictionary<string, ComponentStore>>(json, _serializerOptions) ?? new();
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.Log(ex, $"Error deserializing JSON when saving. Creating new store.");
                        store = new Dictionary<string, ComponentStore>();
                    }
                }
                else
                {
                    store = new Dictionary<string, ComponentStore>();
                }

                store[id.ToString()] = new ComponentStore
                {
                    Properties = new Dictionary<string, object>(properties),
                    Metadata = metadata != null
                        ? metadata.ToDictionary(kvp => kvp.Key, kvp => new PropertyMetadata
                        {
                            IsEditable = kvp.Value.IsEditable,
                            IsVisible = kvp.Value.IsVisible,
                            DisplayName = kvp.Value.DisplayName,
                            Description = kvp.Value.Description
                        })
                        : new Dictionary<string, PropertyMetadata>()
                };

                await SaveStoreAsync(store, ct);
            }
            finally
            {
                _lock.Release();
            }
        }
        internal async Task SaveStoreAsync(Dictionary<string, ComponentStore> store, CancellationToken ct = default)
        {
            string tempFile = $"{_filePath}.tmp";
            try
            {
                var serializableStore = store.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new ComponentStore
                    {
                        Properties = kvp.Value.GetSerializableProperties(),
                        Metadata = kvp.Value.Metadata
                    });

                string json = JsonSerializer.Serialize(serializableStore, _serializerOptions);
                await File.WriteAllTextAsync(tempFile, json, ct);
                File.Move(tempFile, _filePath, true);
            }
            catch (Exception ex)
            {
                _logger.Log(ex, "Error saving store to file");
                if (File.Exists(tempFile))
                {
                    try
                    {
                        File.Delete(tempFile);
                    }
                    catch
                    {
                        // Ignore cleanup errors
                    }
                }
                throw;
            }
        }
        public class ComponentStore
        {
            public Dictionary<string, object> Properties { get; set; } = new();
            public Dictionary<string, PropertyMetadata> Metadata { get; set; } = new();
            public Dictionary<string, object> GetSerializableProperties()
            {
                return Properties
                    .Where(kvp => kvp.Value is not Type)
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            }
        }
        private class PropertyMetadataConverter : JsonConverter<PropertyMetadata>
        {
            public override PropertyMetadata Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType != JsonTokenType.StartObject)
                    throw new JsonException("Expected StartObject token");
                var metadata = new PropertyMetadata();
                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndObject)
                        break;
                    if (reader.TokenType != JsonTokenType.PropertyName)
                        throw new JsonException("Expected PropertyName token");
                    var propertyName = reader.GetString();
                    reader.Read();
                    switch (propertyName)
                    {
                        case "IsEditable":
                            metadata.IsEditable = reader.GetBoolean();
                            break;
                        case "IsVisible":
                            metadata.IsVisible = reader.GetBoolean();
                            break;
                        case "DisplayName":
                            metadata.DisplayName = reader.TokenType == JsonTokenType.Null ? null : reader.GetString();
                            break;
                        case "Description":
                            metadata.Description = reader.TokenType == JsonTokenType.Null ? null : reader.GetString();
                            break;
                    }
                }
                return metadata;
            }
            public override void Write(Utf8JsonWriter writer, PropertyMetadata value, JsonSerializerOptions options)
            {
                writer.WriteStartObject();
                writer.WriteBoolean("IsEditable", value.IsEditable);
                writer.WriteBoolean("IsVisible", value.IsVisible);
                writer.WriteString("DisplayName", value.DisplayName);
                writer.WriteString("Description", value.Description);
                writer.WriteEndObject();
            }
        }
    }
    public class JsonStoreTransaction : IStoreTransaction
    {
        private readonly JsonStore _store;
        private readonly Dictionary<string, JsonStore.ComponentStore> _originalState;
        private readonly Dictionary<string, JsonStore.ComponentStore> _workingState;
        private bool _isCommitted;
        private bool _isRolledBack;
        private bool _isDisposed;
        internal JsonStoreTransaction(JsonStore store, Dictionary<string, JsonStore.ComponentStore> currentState)
        {
            _store = store;
            _originalState = DeepCopyState(currentState);
            _workingState = DeepCopyState(currentState);
        }
        public Task SaveAsync(Guid id, IDictionary<string, object> properties)
        {
            ThrowIfFinalized();
            var componentId = id.ToString();
            if (!_workingState.TryGetValue(componentId, out var componentStore))
            {
                componentStore = new JsonStore.ComponentStore();
                _workingState[componentId] = componentStore;
            }
            componentStore.Properties = new Dictionary<string, object>(properties);
            return Task.CompletedTask;
        }
        public Task SaveWithMetadataAsync(Guid id, IDictionary<string, object> properties, IDictionary<string, IPropertyMetadata>? metadata)
        {
            ThrowIfFinalized();
            var componentId = id.ToString();
            if (!_workingState.TryGetValue(componentId, out var componentStore))
            {
                componentStore = new JsonStore.ComponentStore();
                _workingState[componentId] = componentStore;
            }
            componentStore.Properties = new Dictionary<string, object>(properties);
            if (metadata != null)
            {
                componentStore.Metadata = metadata.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new PropertyMetadata
                    {
                        IsEditable = kvp.Value.IsEditable,
                        IsVisible = kvp.Value.IsVisible,
                        DisplayName = kvp.Value.DisplayName,
                        Description = kvp.Value.Description
                    });
            }
            return Task.CompletedTask;
        }
        public async Task CommitAsync(CancellationToken ct = default)
        {
            ThrowIfFinalized();
            await _store.SaveStoreAsync(_workingState, ct);
            _isCommitted = true;
        }
        public Task RollbackAsync(CancellationToken ct = default)
        {
            ThrowIfFinalized();
            _isRolledBack = true;
            return Task.CompletedTask;
        }
        public async ValueTask DisposeAsync()
        {
            if (_isDisposed)
                return;
            if (!_isCommitted && !_isRolledBack)
            {
                await RollbackAsync();
            }
            _isDisposed = true;
        }
        private void ThrowIfFinalized()
        {
            if (_isCommitted)
                throw new InvalidOperationException("Transaction has already been committed.");
            if (_isRolledBack)
                throw new InvalidOperationException("Transaction has already been rolled back.");
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(JsonStoreTransaction));
        }
        private Dictionary<string, JsonStore.ComponentStore> DeepCopyState(Dictionary<string, JsonStore.ComponentStore> source)
        {
            var result = new Dictionary<string, JsonStore.ComponentStore>();
            foreach (var (key, value) in source)
            {
                result[key] = new JsonStore.ComponentStore
                {
                    Properties = new Dictionary<string, object>(value.Properties),
                    Metadata = new Dictionary<string, PropertyMetadata>(value.Metadata)
                };
            }
            return result;
        }
    }
}