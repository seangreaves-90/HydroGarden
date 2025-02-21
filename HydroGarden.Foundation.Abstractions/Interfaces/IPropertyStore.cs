namespace HydroGarden.Foundation.Abstractions.Interfaces
{
    public interface IPropertyStore
    {
        Task<IDictionary<string, object>> LoadAsync(string id, CancellationToken ct = default);
        Task SaveAsync(string id, IDictionary<string, object> updates, CancellationToken ct = default);
    }
}
