using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HydroGarden.Foundation.Abstractions.Interfaces
{
    public interface IHydroGardenPropertyChangedEvent
    {
        Guid DeviceId { get; }
        string PropertyName { get; }
        Type PropertyType { get; }
        object? OldValue { get; }
        object? NewValue { get; }
        IPropertyMetadata Metadata { get; }
    }

    public interface IHydroGardenEventHandler
    {
        Task HandleEventAsync(object sender, IHydroGardenPropertyChangedEvent e, CancellationToken ct = default);
    }
}
