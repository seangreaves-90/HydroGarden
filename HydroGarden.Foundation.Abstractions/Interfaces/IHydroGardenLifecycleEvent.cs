namespace HydroGarden.Foundation.Abstractions.Interfaces
{
    /// <summary>
    /// Interface for lifecycle events
    /// </summary>
    public interface IHydroGardenLifecycleEvent : IHydroGardenEvent
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
}
