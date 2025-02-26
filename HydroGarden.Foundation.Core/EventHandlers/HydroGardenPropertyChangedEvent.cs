using HydroGarden.Foundation.Abstractions.Interfaces;

namespace HydroGarden.Foundation.Core.EventHandlers
{
    /// <summary>
    /// Represents an event that occurs when a property in a HydroGarden component changes.
    /// </summary>
    /// <param name="DeviceId">The unique identifier of the device that triggered the event.</param>
    /// <param name="PropertyName">The name of the property that changed.</param>
    /// <param name="PropertyType">The type of the property.</param>
    /// <param name="OldValue">The previous value of the property.</param>
    /// <param name="NewValue">The updated value of the property.</param>
    /// <param name="Metadata">The metadata associated with the property.</param>
    public record HydroGardenPropertyChangedEvent(
        Guid DeviceId,
        string PropertyName,
        Type PropertyType,
        object? OldValue,
        object? NewValue,
        IPropertyMetadata Metadata
    ) : IHydroGardenPropertyChangedEvent;
}
