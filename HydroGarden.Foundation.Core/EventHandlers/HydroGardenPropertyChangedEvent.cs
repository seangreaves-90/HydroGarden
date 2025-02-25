using HydroGarden.Foundation.Abstractions.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HydroGarden.Foundation.Core.EventHandlers
{
    public record HydroGardenPropertyChangedEvent(Guid DeviceId,string PropertyName,Type PropertyType,object? OldValue,object? NewValue,IPropertyMetadata Metadata) : IHydroGardenPropertyChangedEvent;
}
