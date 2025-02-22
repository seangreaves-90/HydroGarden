namespace HydroGarden.Foundation.Abstractions.Interfaces
{
    public interface IPropertyMetadata : IDisposable
    {
        Task<TValue> GetValueAsync<TValue>(CancellationToken ct = default);
    }
}
