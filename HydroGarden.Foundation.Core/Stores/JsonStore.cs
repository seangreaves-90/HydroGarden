using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using HydroGarden.Foundation.Abstractions.Interfaces;
using HydroGarden.Foundation.Common.PropertyMetadata;

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
                    new PropertyMetadataConverter()
                }
            };
        }

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
                return null; // Handle corrupted JSON case
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

                string json = await File.ReadAllTextAsync(_filePath, ct);
                var store = JsonSerializer.Deserialize<Dictionary<string, ComponentStore>>(json, _serializerOptions);

                return store?.GetValueOrDefault(id.ToString())?.Metadata?
                    .ToDictionary(kvp => kvp.Key, kvp => (IPropertyMetadata)kvp.Value)
                    ?? new Dictionary<string, IPropertyMetadata>();
            }
            catch (JsonException)
            {
                return null; // Handle corrupted JSON case
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

                string tempFile = $"{_filePath}.tmp";
                await File.WriteAllTextAsync(tempFile, JsonSerializer.Serialize(store, _serializerOptions), ct);
                File.Move(tempFile, _filePath, true);
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
}
