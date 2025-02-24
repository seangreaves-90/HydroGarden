namespace HydroGarden.Foundation.Abstractions.Interfaces
{
    public interface IStore
    {
        Task<IDictionary<string, object>?> LoadAsync(Guid id, CancellationToken ct = default);
        Task SaveAsync(Guid id, IDictionary<string, object> data, CancellationToken ct = default);
    }
}
