namespace HydroGarden.Foundation.Abstractions.Interfaces.Events
{
    /// <summary>
    /// Interface for transforming events during processing
    /// </summary>
    public interface IEventTransformer
    {
        /// <summary>
        /// Transforms an event
        /// </summary>
        /// <param name="evt">The event to transform</param>
        /// <returns>The transformed event</returns>
        IEvent Transform(IEvent evt);
    }
}