using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        string AssemblyType { get; }
        ComponentState State { get; }

        Task SetPropertyAsync(string name, object value, IPropertyMetadata metadata);
        Task<T?> GetPropertyAsync<T>(string name);
        IPropertyMetadata? GetPropertyMetadata(string name);
        IDictionary<string, object> GetProperties();
        IDictionary<string, IPropertyMetadata> GetAllPropertyMetadata();
        Task LoadPropertiesAsync(IDictionary<string, object> properties, IDictionary<string, IPropertyMetadata>? metadata = null);

        void SetEventHandler(IHydroGardenEventHandler handler);
    }
}
