namespace HydroGarden.Foundation.Abstractions.Interfaces.Services
{
    /// <summary>
    /// Service that provides read-only access to component properties
    /// </summary>
    public interface IPropertyAccessService
    {
        /// <summary>
        /// Retrieves a stored property value for a given device.
        /// </summary>
        /// <typeparam name="T">The type of the property value to retrieve</typeparam>
        /// <param name="deviceId">The ID of the device</param>
        /// <param name="propertyName">The name of the property</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>The property value, or default(T) if not found</returns>
        Task<T?> GetPropertyAsync<T>(Guid deviceId, string propertyName, CancellationToken ct = default);

        /// <summary>
        /// Retrieves all properties for a given device.
        /// </summary>
        /// <param name="deviceId">The ID of the device</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Dictionary of property names and values, or empty dictionary if device not found</returns>
        Task<IDictionary<string, object>> GetAllPropertiesAsync(Guid deviceId, CancellationToken ct = default);
    }
}