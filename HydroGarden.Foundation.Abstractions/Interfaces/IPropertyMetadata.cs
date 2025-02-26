namespace HydroGarden.Foundation.Abstractions.Interfaces
{
    /// <summary>
    /// Defines metadata for properties in HydroGarden components.
    /// </summary>
    public interface IPropertyMetadata
    {
        /// <summary>
        /// Gets or sets a value indicating whether the property is editable.
        /// </summary>
        bool IsEditable { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the property is visible.
        /// </summary>
        bool IsVisible { get; set; }

        /// <summary>
        /// Gets or sets the display name of the property.
        /// </summary>
        string? DisplayName { get; set; }

        /// <summary>
        /// Gets or sets the description of the property.
        /// </summary>
        string? Description { get; set; }
    }
}
