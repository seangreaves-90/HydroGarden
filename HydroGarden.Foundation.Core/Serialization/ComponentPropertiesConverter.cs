using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using HydroGarden.Foundation.Core.Components;

namespace HydroGarden.Foundation.Core.Serialization
{
    public class ComponentPropertiesConverter : JsonConverter<object>
    {
        private readonly HashSet<object> _seenObjects = new(); // Track seen objects to prevent recursion

        public override bool CanConvert(Type typeToConvert) => typeof(HydroGardenComponentBase).IsAssignableFrom(typeToConvert);

        public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException("Expected StartObject token");

            using (JsonDocument doc = JsonDocument.ParseValue(ref reader))
            {
                var jsonObj = doc.RootElement;

                // Get the component type from JSON
                if (!jsonObj.TryGetProperty("_Type", out var typeElement))
                    throw new JsonException("Missing _Type property for component deserialization");

                var typeName = typeElement.GetString();
                if (string.IsNullOrWhiteSpace(typeName))
                    throw new JsonException("Invalid component type string");

                Type? componentType = Type.GetType(typeName);
                if (componentType == null || !typeof(HydroGardenComponentBase).IsAssignableFrom(componentType))
                    throw new JsonException($"Unknown or invalid component type: {typeName}");

                // Create an instance of the component
                var component = (HydroGardenComponentBase?)Activator.CreateInstance(componentType, Guid.NewGuid(), "Restored Component");

                if (component == null)
                    throw new JsonException($"Failed to create instance of {typeName}");

                // Deserialize properties
                if (jsonObj.TryGetProperty("Properties", out var propertiesElement))
                {
                    var properties = JsonSerializer.Deserialize<Dictionary<string, object>>(propertiesElement.GetRawText(), options);
                    if (properties != null)
                        component.LoadPropertiesAsync(properties).Wait();
                }

                return component;
            }
        }

        public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }

            // Prevent infinite recursion
            if (_seenObjects.Contains(value))
            {
                writer.WriteStringValue("[RecursiveReference]");
                return;
            }
            _seenObjects.Add(value);

            switch (value)
            {
                case Type type:
                    writer.WriteStringValue(type.AssemblyQualifiedName ?? type.FullName ?? "UnknownType");
                    break;

                case string str:
                    writer.WriteStringValue(str.Trim());
                    break;

                case int or long or double or bool:
                    JsonSerializer.Serialize(writer, value, options);
                    break;

                case Dictionary<string, object> dictionary:
                    writer.WriteStartObject();
                    foreach (var kvp in dictionary)
                    {
                        writer.WritePropertyName(kvp.Key);
                        if (ReferenceEquals(kvp.Value, dictionary))
                        {
                            writer.WriteStringValue("[RecursiveReference]");
                        }
                        else
                        {
                            Write(writer, kvp.Value, options); // Recursively serialize safely
                        }
                    }
                    writer.WriteEndObject();
                    break;

                default:
                    try
                    {
                        JsonSerializer.Serialize(writer, value, options);
                    }
                    catch (Exception ex)
                    {
                        writer.WriteStringValue($"[SerializationError: {ex.Message}]");
                    }
                    break;
            }

            _seenObjects.Remove(value);
        }

    }
}
