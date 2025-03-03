namespace HydroGarden.Foundation.Abstractions.Interfaces.Events
{
    /// <summary>
    /// Represents an event triggered when a property of a HydroGarden component changes.
    /// </summary>
    public interface IHydroGardenPropertyChangedEvent : IHydroGardenEvent
    {
        /// <summary>
        /// The name of the property that changed
        /// </summary>
        string PropertyName { get; }

        /// <summary>
        /// The type of the property
        /// </summary>
        Type PropertyType { get; }

        /// <summary>
        /// The previous value of the property
        /// </summary>
        object? OldValue { get; }

        /// <summary>
        /// The new value of the property
        /// </summary>
        object? NewValue { get; }

        /// <summary>
        /// Metadata associated with the property
        /// </summary>
        IPropertyMetadata Metadata { get; }
    }

    /// <summary>
    /// Defines an event handler that processes property change events in HydroGarden components.
    /// </summary>
    public interface IHydroGardenPropertyChangedEventHandler : IHydroGardenEventHandler 
    {
    }
}
