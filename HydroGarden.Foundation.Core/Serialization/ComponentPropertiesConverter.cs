using System.Text.Json;
using System.Text.Json.Serialization;

namespace HydroGarden.Foundation.Core.Serialization
{
    public class ComponentPropertiesConverter : JsonConverter<object>
    {
        public override bool CanConvert(Type typeToConvert) => true;

        public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.String:
                    var str = reader.GetString();
                    if (str != null && str.StartsWith("Type:", StringComparison.OrdinalIgnoreCase))
                        return Type.GetType(str.Substring(5), throwOnError: false);
                    return str;

                case JsonTokenType.Number:
                    if (reader.TryGetInt64(out long longVal))
                        return longVal;
                    return reader.GetDouble();

                case JsonTokenType.True:
                case JsonTokenType.False:
                    return reader.GetBoolean();

                case JsonTokenType.StartObject:
                    return ReadJsonObject(ref reader, options); // Fix infinite recursion

                case JsonTokenType.Null:
                    return null;

                default:
                    throw new JsonException($"Unexpected token type: {reader.TokenType}");
            }
        }

        private object ReadJsonObject(ref Utf8JsonReader reader, JsonSerializerOptions options)
        {
            using (JsonDocument doc = JsonDocument.ParseValue(ref reader))
            {
                return doc.RootElement.Clone();
            }
        }

        public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }

            switch (value)
            {
                case Type type:
                    writer.WriteStringValue($"Type:{type.AssemblyQualifiedName}");
                    break;

                case string str:
                    writer.WriteStringValue(str.Trim());
                    break;

                case JsonElement jsonElement:
                    jsonElement.WriteTo(writer);
                    break;

                case IDictionary<string, object> dictionary:
                    writer.WriteStartObject();
                    foreach (var kvp in dictionary)
                    {
                        writer.WritePropertyName(kvp.Key);
                        JsonSerializer.Serialize(writer, kvp.Value, options);
                    }
                    writer.WriteEndObject();
                    break;

                case IEnumerable<object> list:
                    writer.WriteStartArray();
                    foreach (var item in list)
                    {
                        JsonSerializer.Serialize(writer, item, options);
                    }
                    writer.WriteEndArray();
                    break;

                default:
                    JsonSerializer.Serialize(writer, value, options);
                    break;
            }
        }
    }
}
