using HydroGarden.Foundation.Abstractions.Interfaces.Events;

namespace HydroGarden.Foundation.Common.Events.Handlers
{
    public class HydroGardenLifecycleChangedEvent : IHydroGardenLifecycleEventHandler
    {
        public ValueTask DisposeAsync()
        {
            throw new NotImplementedException();
        }

        Task IHydroGardenEventHandler.HandleEventAsync<T>(object sender, T evt, CancellationToken ct)
        {
            throw new NotImplementedException();
        }
    }
}
