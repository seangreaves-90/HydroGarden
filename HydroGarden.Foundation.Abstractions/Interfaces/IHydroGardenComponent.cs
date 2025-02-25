using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HydroGarden.Foundation.Abstractions.Interfaces
{
    public interface IPropertyMetadata
    {
        public bool IsEditable { get; set; }
        public bool IsVisible { get; set; }
    }

    public interface IHydroGardenComponent : IDisposable
    {
        Guid Id { get; }
        string Name { get; }
        Type AssemblyType { get; }
        ComponentState State { get; }

        Task InitializeAsync(CancellationToken ct = default);
        Task StartAsync(CancellationToken ct = default);
        Task StopAsync(CancellationToken ct = default);

        Task SetPropertyAsync(string name, object value, bool isEditable = true, bool isVisible = true);
        Task<T?> GetPropertyAsync<T>(string name);
        IPropertyMetadata? GetPropertyMetadata(string name);
        IDictionary<string, object> GetProperties();
        IDictionary<string, IPropertyMetadata> GetAllPropertyMetadata();
        Task LoadPropertiesAsync(IDictionary<string, object> properties, IDictionary<string, IPropertyMetadata>? metadata = null);

        void SetEventHandler(IHydroGardenEventHandler handler);
    }
}
