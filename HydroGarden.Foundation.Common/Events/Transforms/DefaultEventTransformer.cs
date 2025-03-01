using HydroGarden.Foundation.Abstractions.Interfaces.Events;

namespace HydroGarden.Foundation.Common.Events.Transforms
{
    /// <summary>
    /// Default implementation of event transformation. Can be extended to modify event data.
    /// </summary>
    public class DefaultEventTransformer : IEventTransformer
    {
        public IHydroGardenEvent Transform(IHydroGardenEvent evt)
        {
            return evt; // No transformation, but extendable for logging/enrichment
        }
    }
}
