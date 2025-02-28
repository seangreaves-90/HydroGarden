using HydroGarden.Foundation.Abstractions.Interfaces;

namespace HydroGarden.Foundation.Common.Extensions
{
    /// <summary>
    /// Extension methods for IHydroGardenEventHandler
    /// </summary>
    public static class EventHandlerExtensions
    {
        /// <summary>
        /// Gets the target ID for this handler if it implements ITargetedEventHandler,
        /// otherwise returns an empty GUID
        /// </summary>
        /// <param name="handler">The event handler</param>
        /// <returns>The target component ID or empty GUID</returns>
        public static Guid GetTargetId(this IHydroGardenEventHandler handler)
        {
            return handler is ITargetedEventHandler targeted
                ? targeted.GetTargetId()
                : Guid.Empty;
        }
    }
}
