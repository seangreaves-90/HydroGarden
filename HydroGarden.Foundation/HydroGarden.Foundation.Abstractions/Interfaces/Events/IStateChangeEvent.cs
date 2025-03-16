using HydroGarden.Foundation.Abstractions.Interfaces.Components;

namespace HydroGarden.Foundation.Abstractions.Interfaces.Events
{
    /// <summary>
    /// Represents an event triggered when a component changes state.
    /// </summary>
    public interface IStateChangeEvent : IEvent
    {
        /// <summary>
        /// Gets the old state of the component.
        /// </summary>
        ComponentState OldState { get; }

        /// <summary>
        /// Gets the new state of the component.
        /// </summary>
        ComponentState NewState { get; }
    }

    /// <summary>
    /// Defines an event handler that processes state change events in HydroGarden components.
    /// </summary>
    public interface IStateChangeEventHandler : IEventHandler 
    {
    }
}
