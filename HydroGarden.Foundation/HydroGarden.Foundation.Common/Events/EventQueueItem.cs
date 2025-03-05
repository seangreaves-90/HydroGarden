using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Foundation.Common.Results;

namespace HydroGarden.Foundation.Common.Events
{

    /// <summary>
    /// Represents an event item queued for processing.
    /// </summary>
    public class EventQueueItem
    {
        public required object Sender { get; init; }
        public required IEvent Event { get; init; }
        public required IEventSubscription Subscription { get; init; }
        public required PublishResult Result { get; init; }
        public required TaskCompletionSource<bool> CompletionSource { get; init; }
    }
}
