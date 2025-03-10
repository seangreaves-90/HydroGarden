namespace HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling
{
    /// <summary>
    /// Interface for services that can recover components
    /// </summary>
    public interface IComponentRecoveryService
    {
        /// <summary>
        /// Attempts to recover a component by its ID
        /// </summary>
        /// <param name="deviceId">The ID of the component to recover</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>True if recovery was successful, false otherwise</returns>
        Task<bool> RecoverDeviceAsync(Guid deviceId, CancellationToken ct = default);
    }
}