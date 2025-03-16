using HydroGarden.Foundation.Abstractions.Interfaces.Events;

namespace HydroGarden.Foundation.Common.Events.Pipeline
{
    /// <summary>
    /// Represents the result of processing an event through the event pipeline.
    /// </summary>
    public class EventProcessingResult : IEventProcessingResult
    {
        /// <inheritdoc/>
        public IEvent Event { get; }

        /// <inheritdoc/>
        public IEvent ProcessedEvent { get; }

        /// <inheritdoc/>
        public bool IsSuccess { get; }

        /// <inheritdoc/>
        [Obsolete("ShouldRetry is deprecated and will be removed in a future version.")]
        public bool ShouldRetry { get; }

        /// <inheritdoc/>
        public Exception? Exception { get; }

        /// <inheritdoc/>
        [Obsolete("RetryCount is deprecated and will be removed in a future version.")]
        public int RetryCount { get; }

        /// <inheritdoc/>
        [Obsolete("RetryDelay is deprecated and will be removed in a future version.")]
        public TimeSpan RetryDelay { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="EventProcessingResult"/> class.
        /// </summary>
        /// <param name="event">The event that was processed.</param>
        /// <param name="isSuccess">Whether the processing was successful.</param>
        /// <param name="shouldRetry">Whether the event should be retried.</param>
        /// <param name="exception">The exception that occurred during processing, if any.</param>
        /// <param name="retryCount">The number of retry attempts that have been made.</param>
        /// <param name="retryDelay">The delay before the next retry attempt.</param>
        public EventProcessingResult(
            IEvent @event,
            bool isSuccess,
            bool shouldRetry,
            Exception? exception,
            int retryCount = 0,
            TimeSpan? retryDelay = null)
        {
            Event = @event ?? throw new ArgumentNullException(nameof(@event));
            ProcessedEvent = @event; // By default, the processed event is the same as the original
            IsSuccess = isSuccess;
            ShouldRetry = shouldRetry;
            Exception = exception;
            RetryCount = retryCount;
            RetryDelay = retryDelay ?? TimeSpan.Zero;
        }

        /// <summary>
        /// Creates a successful result.
        /// </summary>
        /// <param name="event">The event that was processed.</param>
        /// <returns>A successful result.</returns>
        public static IEventProcessingResult Success(IEvent @event)
        {
            return new EventProcessingResult(@event, true, false, null);
        }

        /// <summary>
        /// Creates a failure result.
        /// </summary>
        /// <param name="event">The event that was processed.</param>
        /// <param name="exception">The exception that occurred.</param>
        /// <param name="shouldRetry">Whether the event should be retried.</param>
        /// <returns>A failure result.</returns>
        public static IEventProcessingResult Failure(IEvent @event, Exception? exception, bool shouldRetry = false)
        {
            return new EventProcessingResult(@event, false, shouldRetry, exception);
        }

        /// <summary>
        /// Creates a result indicating the event should be retried.
        /// [DEPRECATED] Retry functionality is deprecated and will be removed in a future version.
        /// </summary>
        /// <param name="event">The event that was processed.</param>
        /// <param name="exception">The exception that occurred, if any.</param>
        /// <param name="retryCount">The number of retry attempts that have been made.</param>
        /// <param name="retryDelay">The delay before the next retry attempt.</param>
        /// <returns>A retry result.</returns>
        [Obsolete("Retry functionality is deprecated and will be removed in a future version.")]
        public static IEventProcessingResult Retry(
            IEvent @event,
            Exception? exception = null,
            int retryCount = 1,
            TimeSpan? retryDelay = null)
        {
            return new EventProcessingResult(
                @event,
                false,
                true,
                exception,
                retryCount,
                retryDelay ?? TimeSpan.FromSeconds(1));
        }
    }
}