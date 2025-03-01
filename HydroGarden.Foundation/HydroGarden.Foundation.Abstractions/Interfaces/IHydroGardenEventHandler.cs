using HydroGarden.Foundation.Abstractions.Interfaces.Events;

namespace HydroGarden.Foundation.Abstractions.Interfaces
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
    public interface IHydroGardenPropertyChangedEventHandler : IAsyncDisposable
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
