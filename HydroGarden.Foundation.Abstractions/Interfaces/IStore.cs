// HydroGarden.Foundation.Abstractions.Interfaces/IStore.cs
namespace HydroGarden.Foundation.Abstractions.Interfaces
{
    public interface IStore
    {
        Task<IDictionary<string, object>?> LoadAsync(Guid id, CancellationToken ct = default);
        Task<IDictionary<string, IPropertyMetadata>?> LoadMetadataAsync(Guid id, CancellationToken ct = default);
        Task SaveAsync(Guid id, IDictionary<string, object> properties, CancellationToken ct = default);
        Task SaveWithMetadataAsync(Guid id, IDictionary<string, object> properties,
            IDictionary<string, IPropertyMetadata>? metadata, CancellationToken ct = default);
    }
}