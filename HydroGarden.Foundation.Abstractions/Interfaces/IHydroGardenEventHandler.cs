namespace HydroGarden.Foundation.Abstractions.Interfaces
{
    /// <summary>
    /// Represents an event triggered when a property of a HydroGarden component changes.
    /// </summary>
    public interface IHydroGardenPropertyChangedEvent
    {
        /// <summary>
        /// Gets the unique identifier of the device associated with the event.
        /// </summary>
        Guid DeviceId { get; }

        /// <summary>
        /// Gets the name of the property that changed.
        /// </summary>
        string PropertyName { get; }

        /// <summary>
        /// Gets the data type of the property.
        /// </summary>
        Type PropertyType { get; }

        /// <summary>
        /// Gets the old value of the property before the change.
        /// </summary>
        object? OldValue { get; }

        /// <summary>
        /// Gets the new value of the property after the change.
        /// </summary>
        object? NewValue { get; }

        /// <summary>
        /// Gets the metadata associated with the property.
        /// </summary>
        IPropertyMetadata Metadata { get; }
    }

    /// <summary>
    /// Defines an event handler that processes property change events in HydroGarden components.
    /// </summary>
    public interface IHydroGardenEventHandler
    {
        /// <summary>
        /// Handles an event asynchronously when a property of a HydroGarden component changes.
        /// </summary>
        /// <param name="sender">The source object of the event.</param>
        /// <param name="e">The event details containing property change information.</param>
        /// <param name="ct">An optional cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task HandleEventAsync(object sender, IHydroGardenPropertyChangedEvent e, CancellationToken ct = default);
    }
}
