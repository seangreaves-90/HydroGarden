using HydroGarden.Foundation.Abstractions.Interfaces.Events;

namespace HydroGarden.Foundation.Abstractions.Interfaces.Components
{
    /// <summary>
    /// Defines the possible states of a HydroGarden component.
    /// </summary>
    public enum ComponentState
    {
        Created,
        Initializing,
        Ready,
        Running,
        Stopping,
        Error,
        Disposed
    }

    /// <summary>
    /// Represents a core component in the HydroGarden system.
    /// Provides lifecycle management and property handling.
    /// </summary>
    public interface IComponent : IDisposable
    {
        /// <summary>
        /// Gets the unique identifier of the component.
        /// </summary>
        Guid Id { get; }

        /// <summary>
        /// Gets the name of the component.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the assembly type of the component.
        /// </summary>
        string AssemblyType { get; }

        /// <summary>
        /// Gets the current state of the component.
        /// </summary>
        ComponentState State { get; }

        /// <summary>
        /// Asynchronously sets a property value for the component.
        /// </summary>
        /// <param name="name">The property name.</param>
        /// <param name="value">The property value.</param>
        /// <param name="metadata">Optional metadata for the property.</param>
        Task SetPropertyAsync(string name, object value, IPropertyMetadata metadata);

        /// <summary>
        /// Asynchronously retrieves a property value by name.
        /// </summary>
        /// <typeparam name="T">The expected type of the property value.</typeparam>
        /// <param name="name">The property name.</param>
        /// <returns>The property value if found; otherwise, null.</returns>
        Task<T?> GetPropertyAsync<T>(string name);

        /// <summary>
        /// Retrieves metadata for a specified property.
        /// </summary>
        /// <param name="name">The property name.</param>
        /// <returns>The property metadata, or null if not found.</returns>
        IPropertyMetadata? GetPropertyMetadata(string name);

        /// <summary>
        /// Retrieves metadata for a specified property.
        /// </summary>
        /// <param name="name">The property name.</param>
        /// <param name="isEditable">The property should be editable.</param>
        /// <param name="isVisible">The property should be visible.</param>
        /// <returns>The property metadata, or null if not found.</returns>
        IPropertyMetadata ConstructDefaultPropertyMetadata(string name, bool isEditable, bool isVisible);

        /// <summary>
        /// Retrieves all property values of the component.
        /// </summary>
        /// <returns>A dictionary containing property names and values.</returns>
        IDictionary<string, object> GetProperties();

        /// <summary>
        /// Retrieves metadata for all properties of the component.
        /// </summary>
        /// <returns>A dictionary containing property names and metadata.</returns>
        IDictionary<string, IPropertyMetadata> GetAllPropertyMetadata();

        /// <summary>
        /// Loads multiple properties into the component asynchronously.
        /// </summary>
        /// <param name="properties">A dictionary of property values.</param>
        /// <param name="metadata">Optional dictionary of property metadata.</param>
        Task LoadPropertiesAsync(IDictionary<string, object> properties, IDictionary<string, IPropertyMetadata>? metadata = null);

        /// <summary>
        /// Assigns an event handler to the component.
        /// </summary>
        /// <param name="handler">The event handler to assign.</param>
        void SetEventHandler(IPropertyChangedEventHandler handler);
    }
}
