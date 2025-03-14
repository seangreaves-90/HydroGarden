namespace HydroGarden.Foundation.Abstractions.Interfaces.Services
{
    /// <summary>
    /// Interface for services that are aware of the topology and can use it for their operations.
    /// This provides a clean way to inject topology information without tight coupling.
    /// </summary>
    public interface ITopologyAware
    {
        /// <summary>
        /// Sets the topology service for the component.
        /// </summary>
        /// <param name="topologyService">The topology service to use.</param>
        void SetTopologyService(ITopologyService topologyService);
        
        /// <summary>
        /// Gets the topology service currently associated with this component, if any.
        /// </summary>
        /// <returns>The topology service, or null if none is set.</returns>
        ITopologyService? GetTopologyService();
    }
}