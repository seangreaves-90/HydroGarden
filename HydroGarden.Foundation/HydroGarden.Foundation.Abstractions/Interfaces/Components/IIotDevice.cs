namespace HydroGarden.Foundation.Abstractions.Interfaces.Components
{
    /// <summary>
    /// Represents an IoT device within the HydroGarden system.
    /// Inherits from <see cref="IHydroGardenComponent"/> for common component functionality.
    /// </summary>
    public interface IIoTDevice : IHydroGardenComponent
    {
        /// <summary>
        /// Initializes the IoT device asynchronously.
        /// </summary>
        /// <param name="ct">An optional cancellation token.</param>
        /// <returns>A task representing the asynchronous initialization process.</returns>
        Task InitializeAsync(CancellationToken ct = default);

        /// <summary>
        /// Starts the IoT device asynchronously.
        /// </summary>
        /// <param name="ct">An optional cancellation token.</param>
        /// <returns>A task representing the asynchronous start process.</returns>
        Task StartAsync(CancellationToken ct = default);

        /// <summary>
        /// Stops the IoT device asynchronously.
        /// </summary>
        /// <param name="ct">An optional cancellation token.</param>
        /// <returns>A task representing the asynchronous stop process.</returns>
        Task StopAsync(CancellationToken ct = default);
    }
}
