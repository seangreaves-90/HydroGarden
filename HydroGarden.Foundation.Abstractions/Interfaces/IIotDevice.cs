namespace HydroGarden.Foundation.Abstractions.Interfaces
{
    public interface IIoTDevice : IHydroGardenComponent
    {
        Task InitializeAsync(CancellationToken ct = default);
        Task StartAsync(CancellationToken ct = default);
        Task StopAsync(CancellationToken ct = default);
    }
}
