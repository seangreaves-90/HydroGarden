using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;

namespace HydroGarden.Foundation.Abstractions.Interfaces.Components
{
    /// <summary>
    /// Represents an IoT device within the HydroGarden system.
    /// Inherits from <see cref="IComponent"/> for common component functionality.
    /// </summary>
    public interface IIoTDevice : IComponent
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

        /// <summary>
        /// Reports an error from the IoT device.
        /// </summary>
        /// <param name="error">Propagated component error details</param>
        /// <param name="ct">An optional cancellation token.</param>
        /// <returns>A task representing the asynchronous error reporting.</returns>
        Task ReportErrorAsync(IComponentError error, CancellationToken ct = default);

       /// <summary>
       /// Tries to recover the IoT device asynchronously.
       /// </summary>
       /// <param name="ct">An optional cancellation token.</param>
       /// <returns>True if IoT device recovered from error successfully, false otherwise</returns>
        Task<bool> TryRecoverAsync(CancellationToken ct = default);
    }
}
