using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace HydroGarden.Foundation.Abstractions.Interfaces
{
    public interface IPropertyManager : IDisposable
    {
        event EventHandler<IPropertyChangedEventArgs> PropertyChanged;
        Task LoadAsync(CancellationToken ct = default);
        Task SaveAsync(CancellationToken ct = default);
        Task<T?> GetPropertyAsync<T>(string name, CancellationToken ct = default);
        Task SetPropertyAsync<T>(
            string name,
            T? value,
            bool isReadOnly = false,
            IPropertyValidator<IValidationResult, T>? validator = null,
            CancellationToken ct = default);
        bool HasProperty(string name);
    }

}
