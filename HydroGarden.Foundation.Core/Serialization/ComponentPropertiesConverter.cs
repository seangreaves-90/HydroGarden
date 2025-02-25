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
}