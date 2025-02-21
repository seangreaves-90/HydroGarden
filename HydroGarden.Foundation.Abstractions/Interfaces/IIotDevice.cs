namespace HydroGarden.Foundation.Abstractions.Interfaces
{
    public interface IIoTDevice : IDisposable
    {
        Guid Id { get; }
        string DisplayName { get; }
        string DeviceType { get; }
        DeviceState State { get; }

        Task InitializeAsync(CancellationToken ct = default);
        Task ExecuteCoreAsync(CancellationToken ct = default);
        Task SaveAsync(CancellationToken ct = default);
    }

    public enum DeviceState
    {
        Created,
        Initializing,
        Ready,
        Error,
        Disposed
    }

}
