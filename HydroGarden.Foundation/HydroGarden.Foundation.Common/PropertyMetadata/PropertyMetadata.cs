using HydroGarden.Foundation.Abstractions.Interfaces;

namespace HydroGarden.Foundation.Common.PropertyMetadata
{
    /// <summary>
    /// Represents metadata for a property in HydroGarden components.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="PropertyMetadata"/> class.
    /// </remarks>
    /// <param name="isEditable">Indicates if the property is editable.</param>
    /// <param name="isVisible">Indicates if the property is visible.</param>
    /// <param name="displayName">The display name of the property.</param>
    /// <param name="description">The description of the property.</param>
    public class PropertyMetadata(bool isEditable = true, bool isVisible = true, string? displayName = null, string? description = null) : IPropertyMetadata
    {
        /// <summary>
        /// Gets or sets a value indicating whether the property is editable.
        /// </summary>
        public bool IsEditable { get; set; } = isEditable;

        /// <summary>
        /// Gets or sets a value indicating whether the property is visible.
        /// </summary>
        public bool IsVisible { get; set; } = isVisible;

        /// <summary>
        /// Gets or sets the display name of the property.
        /// </summary>
        public string? DisplayName { get; set; } = displayName;

        /// <summary>
        /// Gets or sets the description of the property.
        /// </summary>
        public string? Description { get; set; } = description;
    }
}
