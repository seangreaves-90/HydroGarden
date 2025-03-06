using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Foundation.Abstractions.Interfaces.Logging;
using HydroGarden.Foundation.Common.Events;
using System.Collections.Concurrent;


namespace HydroGarden.Foundation.Common.QueueProcessor
{
    /// <summary>
    /// Processes event queues based on priority levels and manages event execution asynchronously.
    /// </summary>
    public class EventQueueProcessor
    {
        private readonly ConcurrentDictionary<EventPriority, ConcurrentQueue<EventQueueItem>> _eventQueues;
        private readonly ILogger _logger;
        private readonly CancellationTokenSource _cancellationSource = new();
        private readonly List<Task> _processingTasks = new();
        private readonly int _maxConcurrentProcessingPerPriority;

        /// <summary>
        /// Initializes a new instance of the EventQueueProcessor class.
        /// </summary>
        /// <param name="logger">Logger instance for tracking events.</param>
        /// <param name="maxConcurrentProcessing">Maximum concurrent tasks for processing each priority queue.</param>
        public EventQueueProcessor(ILogger logger, int maxConcurrentProcessing = 4)
        {
            _eventQueues = new ConcurrentDictionary<EventPriority, ConcurrentQueue<EventQueueItem>>();
            _logger = logger;
            _maxConcurrentProcessingPerPriority = maxConcurrentProcessing;

            // Initialize the event queues for each priority level.
            foreach (EventPriority priority in Enum.GetValues(typeof(EventPriority)))
            {
                _eventQueues[priority] = new ConcurrentQueue<EventQueueItem>();
            }

            StartProcessingTasks();
        }

        /// <summary>
        /// Enqueues an event item into the appropriate priority queue.
        /// </summary>
        /// <param name="item">The event queue item to enqueue.</param>
        public void Enqueue(EventQueueItem item)
        {
            _eventQueues[item.Event.RoutingData?.Priority ?? EventPriority.Normal].Enqueue(item);
        }

        /// <summary>
        /// Starts background tasks to process event queues based on priority levels.
        /// </summary>
        private void StartProcessingTasks()
        {
            foreach (var priority in _eventQueues.Keys.OrderByDescending(p => p))
            {
                for (int i = 0; i < _maxConcurrentProcessingPerPriority; i++)
                {
                    _processingTasks.Add(Task.Run(() => ProcessQueue(priority, _cancellationSource.Token)));
                }
            }
        }

        /// <summary>
        /// Processes events in the queue for a specific priority level.
        /// </summary>
        /// <param name="priority">The event priority level.</param>
        /// <param name="ct">Cancellation token for task management.</param>
        private async Task ProcessQueue(EventPriority priority, CancellationToken ct)
        {
            var queue = _eventQueues[priority];

            while (!ct.IsCancellationRequested)
            {
                if (queue.TryDequeue(out var queueItem))
                {
                    try
                    {
                        if (queueItem.Event is IPropertyChangedEvent propChangedEvent)
                        {
                            await queueItem.Subscription.Handler.HandleEventAsync(queueItem.Sender, propChangedEvent, ct);
                            queueItem.Result.SuccessCount++;
                        }
                    }
                    catch (Exception? ex)
                    {
                        _logger.Log(ex, $"[EventQueueProcessor] Error processing event {queueItem.Event.EventId}");
                        queueItem.Result.Errors.Add(ex);
                    }
                    finally
                    {
                        queueItem.CompletionSource.TrySetResult(true);
                    }
                }
                else
                {
                    await Task.Delay(10, ct); // Delay to prevent excessive CPU usage.
                }
            }
        }

        /// <summary>
        /// Disposes resources and stops event queue processing.
        /// </summary>
        public void Dispose()
        {
            _cancellationSource.Cancel();
            try
            {
                Task.WaitAll(_processingTasks.ToArray(), TimeSpan.FromSeconds(5));
            }
            catch (AggregateException ex) when (ex.InnerExceptions.All(e => e is TaskCanceledException))
            {
                // Task cancellations are expected during disposal when we cancel the token
                // We can safely ignore these exceptions
            }
            _cancellationSource.Dispose();
        }
    }
}
