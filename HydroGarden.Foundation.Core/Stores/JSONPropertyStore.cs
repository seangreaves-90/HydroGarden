using System.Text.Json;
using HydroGarden.Foundation.Abstractions.Interfaces;

namespace HydroGarden.Foundation.Core.Stores
{
    public class JsonPropertyStore : IPropertyStore
    {
        private readonly string _basePath;
        private readonly ILogger? _logger;
        private readonly JsonSerializerOptions _serializerOptions;

        public JsonPropertyStore(string basePath, ILogger? logger = null)
        {
            _basePath = Path.GetFullPath(basePath ?? throw new ArgumentNullException(nameof(basePath)));
            _logger = logger;
            _serializerOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true
            };

            // Ensure directory exists
            Directory.CreateDirectory(_basePath);
        }

        private string GetFilePath(string id)
        {
            // Sanitize ID for file system
            var sanitizedId = string.Join("_", id.Split(Path.GetInvalidFileNameChars()));
            return Path.Combine(_basePath, $"{sanitizedId}.json");
        }

        public async Task<IDictionary<string, object>> LoadAsync(string id, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            try
            {
                var filePath = GetFilePath(id);
                if (!File.Exists(filePath))
                {
                    return new Dictionary<string, object>();
                }

                var jsonText = await File.ReadAllTextAsync(filePath, ct);
                var jsonData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonText, _serializerOptions);

                var result = new Dictionary<string, object>();
                if (jsonData != null)
                {
                    foreach (var kvp in jsonData)
                    {
                        result[kvp.Key] = ConvertJsonElement(kvp.Value);
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Failed to load properties for {id}");
                throw;
            }
        }

        public async Task SaveAsync(string id, IDictionary<string, object> updates, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));
            if (updates == null) throw new ArgumentNullException(nameof(updates));

            try
            {
                var filePath = GetFilePath(id);
                var jsonText = JsonSerializer.Serialize(updates, _serializerOptions);
                await File.WriteAllTextAsync(filePath, jsonText, ct);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Failed to save properties for {id}");
                throw;
            }
        }

        private object ConvertJsonElement(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString()!,
                JsonValueKind.Number => element.TryGetInt64(out var longVal) ? longVal : element.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null!,
                JsonValueKind.Array => element.EnumerateArray()
                    .Select(e => ConvertJsonElement(e))
                    .ToArray(),
                JsonValueKind.Object => element.EnumerateObject()
                    .ToDictionary(p => p.Name, p => ConvertJsonElement(p.Value)),
                _ => throw new JsonException($"Unsupported JSON value kind: {element.ValueKind}")
            };
        }
    }
}