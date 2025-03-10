using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Logger.Abstractions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace HydroGarden.Foundation.Common.Events.Pipeline.Middleware
{
    /// <summary>
    /// Dead letter queue entry containing a failed event and metadata.
    /// </summary>
    public class DeadLetterEntry
    {
        /// <summary>
        /// Gets or sets the unique identifier for this entry.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the event that failed.
        /// </summary>
        public IEvent Event { get; set; }

        /// <summary>
        /// Gets or sets the original event as JSON, for diagnostics.
        /// </summary>
        public string EventJson { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the event was added to the dead letter queue.
        /// </summary>
        public DateTimeOffset Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the exception that caused the failure.
        /// </summary>
        public Exception Exception { get; set; }

        /// <summary>
        /// Gets or sets the error message.
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets the number of processing attempts.
        /// </summary>
        public int ProcessingAttempts { get; set; }

        /// <summary>
        /// Gets or sets additional metadata about the failure.
        /// </summary>
        public Dictionary<string, string> Metadata { get; set; }
    }

    /// <summary>
    /// Middleware that captures failed events and places them in a dead letter queue.
    /// </summary>
    public class DeadLetterQueueMiddleware : IEventMiddleware, IDisposable
    {
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<Guid, DeadLetterEntry> _deadLetterQueue = new();
        private readonly int _queueCapacity;
        private readonly Timer _cleanupTimer;
        private readonly object _cleanupLock = new();
        private readonly TimeSpan _retentionPeriod;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="DeadLetterQueueMiddleware"/> class.
        /// </summary>
        /// <param name="logger">The logger to use.</param>
        /// <param name="queueCapacity">The maximum capacity of the queue.</param>
        /// <param name="retentionPeriod">The period to retain dead letter entries.</param>
        public DeadLetterQueueMiddleware(
            ILogger logger,
            int queueCapacity = 1000,
            TimeSpan? retentionPeriod = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _queueCapacity = queueCapacity > 0 ? queueCapacity : throw new ArgumentOutOfRangeException(nameof(queueCapacity), "Must be greater than 0");
            _retentionPeriod = retentionPeriod ?? TimeSpan.FromDays(7);
            
            // Create a timer to periodically clean up old entries
            _cleanupTimer = new Timer(CleanupExpiredEntries, null, TimeSpan.FromHours(1), TimeSpan.FromHours(1));
            
            Id = Guid.NewGuid();
            Name = "Dead Letter Queue Middleware";
            Order = 10000; // Run at the end of the pipeline
        }

        /// <inheritdoc />
        public Guid Id { get; }

        /// <inheritdoc />
        public string Name { get; }

        /// <inheritdoc />
        public int Order { get; }

        /// <summary>
        /// Gets all entries in the dead letter queue.
        /// </summary>
        /// <returns>All dead letter entries.</returns>
        public IReadOnlyList<DeadLetterEntry> GetAllEntries()
        {
            return _deadLetterQueue.Values.OrderByDescending(e => e.Timestamp).ToList();
        }

        /// <summary>
        /// Gets a specific entry from the dead letter queue.
        /// </summary>
        /// <param name="entryId">The entry ID.</param>
        /// <returns>The dead letter entry if found, null otherwise.</returns>
        public DeadLetterEntry GetEntry(Guid entryId)
        {
            _deadLetterQueue.TryGetValue(entryId, out var entry);
            return entry;
        }

        /// <summary>
        /// Removes an entry from the dead letter queue.
        /// </summary>
        /// <param name="entryId">The entry ID.</param>
        /// <returns>True if the entry was found and removed, false otherwise.</returns>
        public bool RemoveEntry(Guid entryId)
        {
            return _deadLetterQueue.TryRemove(entryId, out _);
        }

        /// <summary>
        /// Clears all entries from the dead letter queue.
        /// </summary>
        public void ClearAll()
        {
            _deadLetterQueue.Clear();
        }

        /// <inheritdoc />
        public async Task<IEventProcessingResult> ProcessAsync(
            object sender,
            IEvent @event,
            Func<object, IEvent, CancellationToken, Task<IEventProcessingResult>> next,
            CancellationToken cancellationToken = default)
        {
            // Process the event through the rest of the pipeline
            var result = await next(sender, @event, cancellationToken);

            // If successful, just return the result
            if (result.IsSuccess)
            {
                return result;
            }

            // If we should retry, let the retry middleware handle it
            if (result.ShouldRetry)
            {
                return result;
            }

            // Failed and not retrying, add to dead letter queue
            try
            {
                var entry = new DeadLetterEntry
                {
                    Id = Guid.NewGuid(),
                    Event = result.ProcessedEvent,
                    EventJson = JsonSerializer.Serialize(result.ProcessedEvent, new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        MaxDepth = 10
                    }),
                    Timestamp = DateTimeOffset.UtcNow,
                    Exception = result.Exception,
                    ErrorMessage = result.Exception?.Message ?? "Unknown error",
                    ProcessingAttempts = result.RetryCount + 1,
                    Metadata = new Dictionary<string, string>
                    {
                        ["EventId"] = result.ProcessedEvent.EventId.ToString(),
                        ["EventType"] = result.ProcessedEvent.EventType.ToString(),
                        ["SourceId"] = result.ProcessedEvent.SourceId.ToString(),
                        ["Timestamp"] = result.ProcessedEvent.Timestamp.ToString("o")
                    }
                };

                // Ensure we don't exceed queue capacity
                if (_deadLetterQueue.Count >= _queueCapacity)
                {
                    EnsureQueueCapacity();
                }

                _deadLetterQueue[entry.Id] = entry;

                _logger.Log($"Added event {result.ProcessedEvent.EventId} to dead letter queue with entry ID {entry.Id}");
            }
            catch (Exception ex)
            {
                _logger.Log(ex, $"Failed to add event {result.ProcessedEvent.EventId} to dead letter queue");
            }

            return result;
        }

        /// <inheritdoc />
        public bool ShouldApply(IEvent @event)
        {
            // Apply to all events
            return true;
        }

        private void EnsureQueueCapacity()
        {
            // Remove oldest entries to ensure capacity
            if (_deadLetterQueue.Count >= _queueCapacity)
            {
                var oldestEntries = _deadLetterQueue.Values
                    .OrderBy(e => e.Timestamp)
                    .Take(_deadLetterQueue.Count - _queueCapacity + 1)
                    .ToList();

                foreach (var entry in oldestEntries)
                {
                    _deadLetterQueue.TryRemove(entry.Id, out _);
                    _logger.Log($"Removed oldest entry {entry.Id} from dead letter queue to maintain capacity");
                }
            }
        }

        private void CleanupExpiredEntries(object state)
        {
            // Avoid running the cleanup multiple times concurrently
            if (!Monitor.TryEnter(_cleanupLock))
            {
                return;
            }

            try
            {
                var cutoffTime = DateTimeOffset.UtcNow.Subtract(_retentionPeriod);
                var expiredEntries = _deadLetterQueue.Values
                    .Where(e => e.Timestamp < cutoffTime)
                    .ToList();

                foreach (var entry in expiredEntries)
                {
                    _deadLetterQueue.TryRemove(entry.Id, out _);
                }

                if (expiredEntries.Count > 0)
                {
                    _logger.Log($"Removed {expiredEntries.Count} expired entries from dead letter queue");
                }
            }
            finally
            {
                Monitor.Exit(_cleanupLock);
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _cleanupTimer.Dispose();
            _deadLetterQueue.Clear();

            GC.SuppressFinalize(this);
        }
    }
}