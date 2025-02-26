using HydroGarden.Foundation.Abstractions.Interfaces;

namespace HydroGarden.Foundation.Common.PropertyMetadata
{
    public class PropertyMetadata : IPropertyMetadata
    {
        public bool IsEditable { get; set; } = true;
        public bool IsVisible { get; set; } = true;
        public string? DisplayName { get; set; }
        public string? Description { get; set; }

        public PropertyMetadata(bool isEditable = true, bool isVisible = true, string? displayName = null, string? description = null)
        {
            IsEditable = isEditable;
            IsVisible = isVisible;
            DisplayName = displayName;
            Description = description;
        }

        // public string? Category { get; set; }
        // public int DisplayOrder { get; set; }
    }
}
