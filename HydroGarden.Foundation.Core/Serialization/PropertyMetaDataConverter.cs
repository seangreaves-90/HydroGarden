using HydroGarden.Foundation.Common.PropertyMetadata;
using System.Text.Json.Serialization;
using System.Text.Json;
using HydroGarden.Foundation.Abstractions.Interfaces;

namespace HydroGarden.Foundation.Core.Serialization
{
    /// <summary>
    /// Custom JSON converter for serializing and deserializing property metadata.
    /// </summary>
    public class PropertyMetadataConverter : JsonConverter<IPropertyMetadata>
    {
        /// <inheritdoc/>
        public override IPropertyMetadata Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return JsonSerializer.Deserialize<PropertyMetadata>(ref reader, options)!;
        }

        /// <inheritdoc/>
        public override void Write(Utf8JsonWriter writer, IPropertyMetadata value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, (PropertyMetadata)value, options);
        }
    }
}
