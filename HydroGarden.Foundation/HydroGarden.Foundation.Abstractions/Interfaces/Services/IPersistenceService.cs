﻿using HydroGarden.Foundation.Abstractions.Interfaces.Components;

namespace HydroGarden.Foundation.Abstractions.Interfaces.Services
{
    public interface IPersistenceService : IAsyncDisposable
    {
        /// <summary>
        /// Registers or updates a IIoTDevice component in the persistence layer.
        /// Ensures component properties are loaded and stored efficiently.
        /// </summary>
        /// <typeparam name="T">The type of the IIoTDevice component (must implement <see cref="IIoTDevice"/>).</typeparam>
        /// <param name="component">The component to add or update.</param>
        /// <param name="ct">Cancellation token for the operation.</param>
        public Task AddOrUpdateAsync<T>(T component, CancellationToken ct = default) where T : IIoTDevice;

        /// <summary>
        /// Manually triggers batch processing of pending events (for testing or manual execution).
        /// </summary>
        Task ProcessPendingEventsAsync();

        /// <summary>
        /// Retrieves a stored property value for a given device.
        /// </summary>
        Task<T?> GetPropertyAsync<T>(Guid deviceId, string propertyName, CancellationToken ct = default);

    }
}
