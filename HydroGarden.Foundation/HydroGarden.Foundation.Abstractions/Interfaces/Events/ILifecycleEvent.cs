using HydroGarden.Foundation.Abstractions.Interfaces.Components;

namespace HydroGarden.Foundation.Abstractions.Interfaces.Events
{
    /// <summary>
    /// Interface for lifecycle events
    /// </summary>
    public interface ILifecycleEvent : IEvent
    {
        /// <summary>
        /// The new state of the component
        /// </summary>
        ComponentState State { get; }

        /// <summary>
        /// Additional information about the lifecycle change
        /// </summary>
        string? Details { get; }
    }

    /// <summary>
    /// Specialized handler for lifecycle events
    /// </summary>
    public interface ILifecycleEventHandler : IEventHandler
    {
    }
}
