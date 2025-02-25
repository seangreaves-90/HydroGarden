using HydroGarden.Foundation.Abstractions.Interfaces;

namespace HydroGarden.Foundation.Common.PropertyMetadata
{
    public class PropertyMetadata : IPropertyMetadata
    {
        public bool IsEditable { get; set; } = true;
        public bool IsVisible { get; set; } = true;
        public string? DisplayName { get; set; }
        public string? Description { get; set; }
        // public string? Category { get; set; }
        // public int DisplayOrder { get; set; }
    }
}
