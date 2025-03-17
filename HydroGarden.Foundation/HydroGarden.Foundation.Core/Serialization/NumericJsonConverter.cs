using System.Text.Json;
using System.Text.Json.Serialization;

namespace HydroGarden.Foundation.Core.Serialization
{
    /// <summary>
    /// JSON converter for ensuring numeric values maintain consistent types across serialization boundaries.
    /// Particularly handles numeric property type consistency for properties like FlowRate that should always be double.
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
                    // Keep the implementation simple - preserve numeric types accurately
                    // (Property type-specific handling is done in JsonStore and JsonStoreTransaction)
                    if (reader.TryGetInt64(out long longValue))
                    {
                        // If the value fits in an int, return as int
                        if (longValue >= int.MinValue && longValue <= int.MaxValue)
                        {
                            return (int)longValue;
                        }
                        return longValue;
                    }
                    else if (reader.TryGetDouble(out double doubleValue))
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
            // Keep the implementation simple - write numeric values with their original type
            // (Property type-specific handling is done in JsonStore and JsonStoreTransaction)
            if (value is int intValue)
            {
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
        // Type-specific property handling is done in the JsonStore and JsonStoreTransaction classes
        // This converter just ensures that numeric values are properly preserved during serialization/deserialization
    }
}