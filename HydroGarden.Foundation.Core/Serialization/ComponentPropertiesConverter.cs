using System.Text.Json.Serialization;
using System.Text.Json;

namespace HydroGarden.Foundation.Core.Serialization
{
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
            // Handle null value
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }

            switch (value)
            {
                case Type t:
                    writer.WriteStringValue($"Type:{t.AssemblyQualifiedName}");
                    break;
                case string s:
                    writer.WriteStringValue(s.Trim());
                    break;
                default:
                    // Create new options without the custom converter to prevent recursive calls
                    var newOptions = new JsonSerializerOptions(options);

                    // Remove all converters of the same type to prevent recursion
                    for (int i = newOptions.Converters.Count - 1; i >= 0; i--)
                    {
                        if (newOptions.Converters[i] is ComponentPropertiesConverter)
                        {
                            newOptions.Converters.RemoveAt(i);
                        }
                    }

                    // Handle circular references
                    newOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;

                    // Use these new options when serializing
                    JsonSerializer.Serialize(writer, value, newOptions);
                    break;
            }
        }
    }
}