namespace HydroGarden.Foundation.Abstractions.Interfaces
{
    public interface IPropertyCache : IAsyncDisposable
    {
        ValueTask SetAsync<T>(string key, T value, CancellationToken ct = default);
        ValueTask<bool> TryGetAsync<T>(string key, CancellationToken ct);
        ValueTask<T?> GetValueOrDefaultAsync<T>(string key, CancellationToken ct = default);
        ValueTask RemoveAsync(string key, CancellationToken ct = default);
    }


}
