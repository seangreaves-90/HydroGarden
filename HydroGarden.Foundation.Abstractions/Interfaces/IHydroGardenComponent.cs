
namespace HydroGarden.Foundation.Abstractions.Interfaces
{
    public enum ComponentState
    {
        Created,
        Initializing,
        Ready,
        Running,
        Stopping,
        Error,
        Disposed
    }

    public interface IHydroGardenComponent : IDisposable
    {
        Guid Id { get; }
        string Name { get; }
        Type AssemblyType { get; }
        ComponentState State { get; }

        Task SetPropertyAsync(string name, object value, bool isEditable = true, bool isVisible = true, string? displayName = null, string? description = null);
        Task<T?> GetPropertyAsync<T>(string name);
        IPropertyMetadata? GetPropertyMetadata(string name);
        IDictionary<string, object> GetProperties();
        IDictionary<string, IPropertyMetadata> GetAllPropertyMetadata();
        Task LoadPropertiesAsync(IDictionary<string, object> properties, IDictionary<string, IPropertyMetadata>? metadata = null);
        void SetEventHandler(IHydroGardenEventHandler handler);
    }
}
