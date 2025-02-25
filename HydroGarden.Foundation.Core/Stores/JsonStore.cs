using System.Text.Json;
using System.Text.Json.Serialization;
using HydroGarden.Foundation.Abstractions.Interfaces;
using HydroGarden.Foundation.Common.PropertyMetadata;
using HydroGarden.Foundation.Core.Serialization;

namespace HydroGarden.Foundation.Core.Stores
{
    /// <summary>
    /// Provides a JSON file-based storage mechanism for component properties and metadata.
    /// </summary>
    public class JsonStore : IStore
    {
        private readonly string _filePath;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private readonly JsonSerializerOptions _serializerOptions;

        /// <summary>
        /// Initializes a new instance of the JsonStore class.
        /// </summary>
        /// <param name="basePath">The base directory path for storing JSON files.</param>
        public JsonStore(string basePath)
        {
            _filePath = Path.Combine(Path.GetFullPath(basePath), "ComponentProperties.json");
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
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
        }

        /// <inheritdoc/>
        public async Task<IStoreTransaction> BeginTransactionAsync(CancellationToken ct = default)
        {
            // Load current state to get a snapshot
            await _lock.WaitAsync(ct);
            try
            {
                Dictionary<string, ComponentStore> store;
                if (File.Exists(_filePath))
                {
                    string json = await File.ReadAllTextAsync(_filePath, ct);
                    store = JsonSerializer.Deserialize<Dictionary<string, ComponentStore>>(json, _serializerOptions) ?? new();
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

        /// <inheritdoc/>
        public async Task<IDictionary<string, object>?> LoadAsync(Guid id, CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
                if (!File.Exists(_filePath)) return null;
                string json = await File.ReadAllTextAsync(_filePath, ct);
                var store = JsonSerializer.Deserialize<Dictionary<string, ComponentStore>>(json, _serializerOptions);
                return store?.GetValueOrDefault(id.ToString())?.Properties;
            }
            catch (JsonException)
            {
                return null;
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <inheritdoc/>
        public async Task<IDictionary<string, IPropertyMetadata>?> LoadMetadataAsync(Guid id, CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
                if (!File.Exists(_filePath)) return null;
                string json = await File.ReadAllTextAsync(_filePath, ct);
                var store = JsonSerializer.Deserialize<Dictionary<string, ComponentStore>>(json, _serializerOptions);
                return store?.GetValueOrDefault(id.ToString())?.Metadata?
                    .ToDictionary(kvp => kvp.Key, kvp => (IPropertyMetadata)kvp.Value)
                    ?? new Dictionary<string, IPropertyMetadata>();
            }
            catch (JsonException)
            {
                return null;
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <inheritdoc/>
        public async Task SaveAsync(Guid id, IDictionary<string, object> properties, CancellationToken ct = default)
        {
            await SaveWithMetadataAsync(id, properties, null, ct);
        }

        /// <inheritdoc/>
        public async Task SaveWithMetadataAsync(Guid id, IDictionary<string, object> properties,
            IDictionary<string, IPropertyMetadata>? metadata, CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
                Dictionary<string, ComponentStore> store;
                if (File.Exists(_filePath))
                {
                    string json = await File.ReadAllTextAsync(_filePath, ct);
                    store = JsonSerializer.Deserialize<Dictionary<string, ComponentStore>>(json, _serializerOptions) ?? new();
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
            await File.WriteAllTextAsync(tempFile, JsonSerializer.Serialize(store, _serializerOptions), ct);
            File.Move(tempFile, _filePath, true);
        }

        /// <summary>
        /// Class that represents a component's properties and metadata.
        /// </summary>
        internal class ComponentStore
        {
            public Dictionary<string, object> Properties { get; set; } = new();
            public Dictionary<string, PropertyMetadata> Metadata { get; set; } = new();
        }

        /// <summary>
        /// Converter for PropertyMetadata objects.
        /// </summary>
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

    /// <summary>
    /// Provides transaction support for JsonStore.
    /// </summary>
    public class JsonStoreTransaction : IStoreTransaction
    {
        private readonly JsonStore _store;
        private readonly Dictionary<string, JsonStore.ComponentStore> _originalState;
        private readonly Dictionary<string, JsonStore.ComponentStore> _workingState;
        private bool _isCommitted;
        private bool _isRolledBack;
        private bool _isDisposed;

        /// <summary>
        /// Initializes a new instance of the JsonStoreTransaction class.
        /// </summary>
        /// <param name="store">The JsonStore that owns this transaction.</param>
        /// <param name="currentState">The current state of the store.</param>
        internal JsonStoreTransaction(JsonStore store, Dictionary<string, JsonStore.ComponentStore> currentState)
        {
            _store = store;
            // Create a deep copy of the current state
            _originalState = DeepCopyState(currentState);
            _workingState = DeepCopyState(currentState);
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        public async Task CommitAsync(CancellationToken ct = default)
        {
            ThrowIfFinalized();

            await _store.SaveStoreAsync(_workingState, ct);
            _isCommitted = true;
        }

        /// <inheritdoc/>
        public Task RollbackAsync(CancellationToken ct = default)
        {
            ThrowIfFinalized();

            // No need to do anything since we haven't committed yet
            _isRolledBack = true;
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            if (_isDisposed)
                return;

            if (!_isCommitted && !_isRolledBack)
            {
                // Auto-rollback if transaction was not explicitly committed or rolled back
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