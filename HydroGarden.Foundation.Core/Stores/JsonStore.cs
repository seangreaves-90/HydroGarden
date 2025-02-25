// HydroGarden.Foundation.Core.Stores/JsonStore.cs
using HydroGarden.Foundation.Abstractions.Interfaces;
using HydroGarden.Foundation.Common.PropertyMetadata;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HydroGarden.Foundation.Core.Stores
{
    public class JsonStore : IStore
    {
        private readonly string _filePath;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private readonly JsonSerializerOptions _serializerOptions;

        public JsonStore(string basePath)
        {
            _filePath = Path.Combine(Path.GetFullPath(basePath), "ComponentProperties.json");
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            _serializerOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true,
                Converters =
                {
                    new JsonStringEnumConverter(),
                    new ComponentPropertiesConverter(),
                    new PropertyMetadataConverter()
                }
            };
        }

        public async Task<IDictionary<string, object>?> LoadAsync(Guid id, CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
                if (!File.Exists(_filePath))
                    return null;

                var json = await File.ReadAllTextAsync(_filePath, ct);
                var store = JsonSerializer.Deserialize<Dictionary<string, ComponentStore>>(json, _serializerOptions);

                if (store?.TryGetValue(id.ToString(), out var componentData) == true)
                {
                    return componentData.Properties;
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
                if (!File.Exists(_filePath))
                    return null;

                var json = await File.ReadAllTextAsync(_filePath, ct);
                var store = JsonSerializer.Deserialize<Dictionary<string, ComponentStore>>(json, _serializerOptions);

                if (store?.TryGetValue(id.ToString(), out var componentData) == true)
                {
                    return componentData.Metadata.ToDictionary(
                        x => x.Key,
                        x => (IPropertyMetadata)x.Value);
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

        public async Task SaveWithMetadataAsync(Guid id, IDictionary<string, object> properties,
            IDictionary<string, IPropertyMetadata>? metadata, CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
                Dictionary<string, ComponentStore> store;
                if (File.Exists(_filePath))
                {
                    var json = await File.ReadAllTextAsync(_filePath, ct);
                    store = JsonSerializer.Deserialize<Dictionary<string, ComponentStore>>(json, _serializerOptions)
                        ?? new Dictionary<string, ComponentStore>();
                }
                else
                {
                    store = new Dictionary<string, ComponentStore>();
                }

                var componentStore = new ComponentStore
                {
                    Properties = new Dictionary<string, object>(properties)
                };

                if (metadata != null)
                {
                    foreach (var (key, value) in metadata)
                    {
                        componentStore.Metadata[key] = new PropertyMetadata
                        {
                            IsEditable = value.IsEditable,
                            IsVisible = value.IsVisible,
                            DisplayName = value.DisplayName,
                            Description = value.Description
                        };
                    }
                }

                store[id.ToString()] = componentStore;

                var updatedJson = JsonSerializer.Serialize(store, _serializerOptions);
                await File.WriteAllTextAsync(_filePath, updatedJson, ct);
            }
            finally
            {
                _lock.Release();
            }
        }

        private class ComponentStore
        {
            public Dictionary<string, object> Properties { get; set; } = new();
            public Dictionary<string, PropertyMetadata> Metadata { get; set; } = new();
        }
    }

    // Add the required converters
    public class ComponentPropertiesConverter : JsonConverter<object>
    {
        public override bool CanConvert(Type typeToConvert) => true;

        public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.True:
                    return true;
                case JsonTokenType.False:
                    return false;
                case JsonTokenType.Number:
                    if (reader.TryGetInt64(out long l))
                        return l;
                    return reader.GetDouble();
                case JsonTokenType.String:
                    var str = reader.GetString();
                    if (DateTime.TryParse(str, out var dt))
                        return dt;
                    if (Guid.TryParse(str, out var guid))
                        return guid;
                    if (str?.StartsWith("Type:") == true)
                        return Type.GetType(str.Substring(5));
                    return str;
                case JsonTokenType.Null:
                    return null;
                default:
                    throw new JsonException($"Unexpected token type: {reader.TokenType}");
            }
        }

        public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
        {
            switch (value)
            {
                case bool b:
                    writer.WriteBooleanValue(b);
                    break;
                case int i:
                    writer.WriteNumberValue(i);
                    break;
                case long l:
                    writer.WriteNumberValue(l);
                    break;
                case double d:
                    writer.WriteNumberValue(d);
                    break;
                case DateTime dt:
                    writer.WriteStringValue(dt.ToString("O"));
                    break;
                case Guid g:
                    writer.WriteStringValue(g.ToString());
                    break;
                case Type t:
                    writer.WriteStringValue($"Type:{t.AssemblyQualifiedName}");
                    break;
                case Enum e:
                    writer.WriteStringValue(e.ToString());
                    break;
                default:
                    writer.WriteStringValue(value.ToString());
                    break;
            }
        }
    }

    public class PropertyMetadataConverter : JsonConverter<PropertyMetadata>
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

            if (value.DisplayName != null)
                writer.WriteString("DisplayName", value.DisplayName);
            else
                writer.WriteNull("DisplayName");

            if (value.Description != null)
                writer.WriteString("Description", value.Description);
            else
                writer.WriteNull("Description");

            writer.WriteEndObject();
        }
    }
}