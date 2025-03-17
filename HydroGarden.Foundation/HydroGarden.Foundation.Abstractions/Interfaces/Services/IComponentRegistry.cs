using HydroGarden.Foundation.Abstractions.Interfaces.Components;

namespace HydroGarden.Foundation.Abstractions.Interfaces.Services
{
    /// <summary>
    /// Service responsible for registering and managing IoT device components
    /// </summary>
    public interface IComponentRegistry
    {
        /// <summary>
        /// Registers or updates a IIoTDevice component in the registry.
        /// Ensures component properties are loaded and stored efficiently.
        /// </summary>
        /// <typeparam name="T">The type of the IIoTDevice component (must implement <see cref="IIoTDevice"/>).</typeparam>
        /// <param name="component">The component to add or update.</param>
        /// <param name="ct">Cancellation token for the operation.</param>
        Task AddOrUpdateAsync<T>(T? component, CancellationToken ct = default) where T : IIoTDevice;

        /// <summary>
        /// Retrieves all stored components and their properties.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A list of tuples containing device ID, name, properties, and metadata.</returns>
        Task<List<(Guid Id, string Name, IDictionary<string, object> Properties, IDictionary<string, IPropertyMetadata> Metadata)>> 
            GetAllStoredDevicesAsync(CancellationToken ct = default);
    }
}