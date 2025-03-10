using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Logger.Abstractions;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace HydroGarden.Foundation.Common.Events.Pipeline.Middleware
{
    /// <summary>
    /// Middleware that implements retry logic for failed events.
    /// </summary>
    public class RetryMiddleware : IEventMiddleware
    {
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<Guid, RetryState> _retryStates = new();
        private readonly int _maxRetries;
        private readonly TimeSpan _initialDelay;
        private readonly double _backoffMultiplier;
        private readonly bool _useJitter;
        private readonly Random _random = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="RetryMiddleware"/> class.
        /// </summary>
        /// <param name="logger">The logger to use.</param>
        /// <param name="maxRetries">The maximum number of retries.</param>
        /// <param name="initialDelay">The initial delay before the first retry.</param>
        /// <param name="backoffMultiplier">The multiplier for exponential backoff.</param>
        /// <param name="useJitter">Whether to add random jitter to retry delays to prevent thundering herd.</param>
        public RetryMiddleware(
            ILogger logger,
            int maxRetries = 3,
            TimeSpan? initialDelay = null,
            double backoffMultiplier = 2.0,
            bool useJitter = true)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _maxRetries = maxRetries > 0 ? maxRetries : throw new ArgumentOutOfRangeException(nameof(maxRetries), "Must be greater than 0");
            _initialDelay = initialDelay ?? TimeSpan.FromSeconds(1);
            _backoffMultiplier = backoffMultiplier > 1.0 ? backoffMultiplier : throw new ArgumentOutOfRangeException(nameof(backoffMultiplier), "Must be greater than 1.0");
            _useJitter = useJitter;
            Id = Guid.NewGuid();
            Name = "Retry Middleware";
            Order = 500; // Run after error handling
        }

        /// <inheritdoc />
        public Guid Id { get; }

        /// <inheritdoc />
        public string Name { get; }

        /// <inheritdoc />
        public int Order { get; }

        /// <inheritdoc />
        public async Task<IEventProcessingResult> ProcessAsync(
            object sender,
            IEvent @event,
            Func<object, IEvent, CancellationToken, Task<IEventProcessingResult>> next,
            CancellationToken cancellationToken = default)
        {
            // Get the current retry state for this event (or create a new one)
            var retryState = _retryStates.GetOrAdd(@event.EventId, _ => new RetryState());

            // Process the event through the rest of the pipeline
            var result = await next(sender, @event, cancellationToken);

            // If successful, clean up retry state and return
            if (result.IsSuccess)
            {
                _retryStates.TryRemove(@event.EventId, out _);
                return result;
            }

            // If the result indicates we should retry
            if (result.ShouldRetry)
            {
                // Check if we've exceeded max retries
                if (retryState.RetryCount >= _maxRetries)
                {
                    _logger.Log($"Max retries ({_maxRetries}) exceeded for event {@event.EventId}");
                    _retryStates.TryRemove(@event.EventId, out _);
                    return EventProcessingResult.Failure(
                        result.ProcessedEvent,
                        result.Exception,
                        shouldRetry: false);
                }

                // Increment retry count
                retryState.RetryCount++;

                // Calculate delay with exponential backoff
                var delay = CalculateRetryDelay(retryState.RetryCount);

                _logger.Log($"Retry {retryState.RetryCount}/{_maxRetries} for event {@event.EventId} scheduled in {delay.TotalMilliseconds}ms");

                // Return a result indicating retry
                return EventProcessingResult.Retry(
                    result.ProcessedEvent,
                    result.Exception,
                    retryState.RetryCount,
                    delay);
            }

            // If we're not retrying, clean up state
            _retryStates.TryRemove(@event.EventId, out _);
            return result;
        }

        /// <inheritdoc />
        public bool ShouldApply(IEvent @event)
        {
            // Apply to all events
            return true;
        }

        private TimeSpan CalculateRetryDelay(int retryCount)
        {
            // Calculate exponential backoff
            var delay = TimeSpan.FromTicks((long)(_initialDelay.Ticks * Math.Pow(_backoffMultiplier, retryCount - 1)));

            // Add jitter if enabled
            if (_useJitter)
            {
                // Add up to 25% random jitter
                var jitterFactor = 1.0 + (_random.NextDouble() * 0.25);
                delay = TimeSpan.FromTicks((long)(delay.Ticks * jitterFactor));
            }

            return delay;
        }

        private class RetryState
        {
            public int RetryCount { get; set; }
        }
    }
}