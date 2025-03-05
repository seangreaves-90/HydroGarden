using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using System.Collections.Concurrent;


namespace HydroGarden.Foundation.Common.Events.Stores
{
    /// <summary>
    /// Stores failed events for later retrieval and reprocessing.
    /// </summary>
    public class DeadLetterEventStore : IEventStore
    {
        private readonly ConcurrentQueue<IEvent> _failedEvents = new();

        public Task PersistEventAsync(IEvent evt)
        {
            _failedEvents.Enqueue(evt);
            return Task.CompletedTask;
        }

        public Task<IEvent?> RetrieveFailedEventAsync()
        {
            return Task.FromResult(_failedEvents.TryDequeue(out var evt) ? evt : null);
        }
    }
}
