namespace HydroGarden.Foundation.Abstractions.Interfaces
{
    public interface IPropertyCache
    {
        void Set<T>(string key, T value);
        bool TryGet<T>(string key, out T? value);
        void Remove(string key);
    }
}
