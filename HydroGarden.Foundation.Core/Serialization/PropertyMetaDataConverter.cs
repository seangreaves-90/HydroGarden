using HydroGarden.Foundation.Common.PropertyMetadata;
using System.Text.Json.Serialization;
using System.Text.Json;
using HydroGarden.Foundation.Abstractions.Interfaces;


namespace HydroGarden.Foundation.Core.Serialization
{
    public class PropertyMetadataConverter : JsonConverter<IPropertyMetadata>
    {
        public override IPropertyMetadata Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return JsonSerializer.Deserialize<PropertyMetadata>(ref reader, options)!;
        }

        public override void Write(Utf8JsonWriter writer, IPropertyMetadata value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, (PropertyMetadata)value, options);
        }
    }
}


