using HydroGarden.Foundation.Abstractions.Interfaces.Events;

namespace HydroGarden.Foundation.Common.Events.Handlers
{
    public class HydroGardenLifecycleChangedEvent : IHydroGardenLifecycleEventHandler
    {
        public ValueTask DisposeAsync()
        {
            throw new NotImplementedException();
        }

        public Task HandleEventAsync(object sender, IHydroGardenLifecycleEvent evt, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }
    }
}
