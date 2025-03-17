namespace HydroGarden.Foundation.Abstractions.Interfaces.Services
{
    /// <summary>
    /// Service for accessing component properties
    /// </summary>
    public interface IPropertyAccessService
    {
        /// <summary>
        /// Gets a property value from a component asynchronously
        /// </summary>
        /// <typeparam name="T">The type to convert the property value to</typeparam>
        /// <param name="componentId">The component ID</param>
        /// <param name="propertyName">The property name</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>The property value, or default if not found</returns>
        Task<T?> GetPropertyAsync<T>(Guid componentId, string propertyName, CancellationToken ct = default);

        /// <summary>
        /// Sets a property value on a component asynchronously
        /// </summary>
        /// <typeparam name="T">The property value type</typeparam>
        /// <param name="componentId">The component ID</param>
        /// <param name="propertyName">The property name</param>
        /// <param name="value">The value to set</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>True if successful, false otherwise</returns>
        Task<bool> SetPropertyAsync<T>(Guid componentId, string propertyName, T value, CancellationToken ct = default);

        /// <summary>
        /// Checks if a property exists on a component
        /// </summary>
        /// <param name="componentId">The component ID</param>
        /// <param name="propertyName">The property name</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>True if the property exists, false otherwise</returns>
        Task<bool> HasPropertyAsync(Guid componentId, string propertyName, CancellationToken ct = default);
    }
}