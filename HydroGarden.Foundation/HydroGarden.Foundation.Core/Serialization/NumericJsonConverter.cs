using System.Text.Json;
using System.Text.Json.Serialization;

namespace HydroGarden.Foundation.Core.Serialization
{
    /// <summary>
    /// JSON converter for ensuring numeric values maintain consistent types
    /// </summary>
    public class NumericJsonConverter : JsonConverter<object>
    {
        public override bool CanConvert(Type typeToConvert)
        {
            return typeToConvert == typeof(object);
        }

        public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.Number:
                    // Always parse numbers as doubles for consistency
                    if (reader.TryGetDouble(out double doubleValue))
                    {
                        return doubleValue;
                    }
                    break;
                case JsonTokenType.String:
                    // Handle string values
                    return reader.GetString();
                case JsonTokenType.True:
                    return true;
                case JsonTokenType.False:
                    return false;
                case JsonTokenType.Null:
                    return null;
            }

            // Use default deserialization for other types
            using (JsonDocument document = JsonDocument.ParseValue(ref reader))
            {
                return document.RootElement.Clone();
            }
        }

        public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
        {
            // Ensure known numeric types are handled specially
            if (value is int intValue)
            {
                // Special handling for certain property names
                writer.WriteNumberValue(intValue);
                return;
            }
            else if (value is double doubleValue)
            {
                writer.WriteNumberValue(doubleValue);
                return;
            }
            else if (value is decimal decimalValue)
            {
                writer.WriteNumberValue(decimalValue);
                return;
            }
            
            // For other types, use default serialization
            JsonSerializer.Serialize(writer, value, value.GetType(), options);
        }
    }
}