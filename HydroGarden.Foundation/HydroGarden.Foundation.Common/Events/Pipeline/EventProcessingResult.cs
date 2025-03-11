using HydroGarden.Foundation.Abstractions.Interfaces.Events;

namespace HydroGarden.Foundation.Common.Events.Pipeline
{
    /// <summary>
    /// Represents the result of processing an event through the pipeline.
    /// </summary>
    public class EventProcessingResult(
        bool isSuccess,
        Exception? exception,
        IEvent processedEvent,
        bool shouldRetry,
        int retryCount,
        TimeSpan retryDelay)
        : IEventProcessingResult
    {
        /// <summary>
        /// Creates a successful result.
        /// </summary>
        /// <param name="processedEvent">The processed event.</param>
        /// <returns>A successful result.</returns>
        public static IEventProcessingResult Success(IEvent processedEvent)
        {
            return new EventProcessingResult(
                isSuccess: true,
                exception: null,
                processedEvent: processedEvent,
                shouldRetry: false,
                retryCount: 0,
                retryDelay: TimeSpan.Zero
            );
        }

        /// <summary>
        /// Creates a failed result.
        /// </summary>
        /// <param name="processedEvent">The processed event.</param>
        /// <param name="exception">The exception that caused the failure.</param>
        /// <param name="shouldRetry">Whether the event should be retried.</param>
        /// <param name="retryCount">The current retry count.</param>
        /// <param name="retryDelay">The suggested delay before the next retry.</param>
        /// <returns>A failed result.</returns>
        public static IEventProcessingResult Failure(
            IEvent processedEvent,
            Exception? exception,
            bool shouldRetry = false,
            int retryCount = 0,
            TimeSpan retryDelay = default)
        {
            return new EventProcessingResult(
                isSuccess: false,
                exception: exception,
                processedEvent: processedEvent,
                shouldRetry: shouldRetry,
                retryCount: retryCount,
                retryDelay: retryDelay == default ? TimeSpan.FromSeconds(Math.Pow(2, retryCount)) : retryDelay
            );
        }

        /// <summary>
        /// Creates a retry result.
        /// </summary>
        /// <param name="processedEvent">The processed event.</param>
        /// <param name="exception">The exception that caused the retry.</param>
        /// <param name="retryCount">The current retry count.</param>
        /// <param name="retryDelay">The suggested delay before the next retry.</param>
        /// <returns>A retry result.</returns>
        public static IEventProcessingResult Retry(
            IEvent processedEvent,
            Exception? exception,
            int retryCount,
            TimeSpan retryDelay = default)
        {
            return new EventProcessingResult(
                isSuccess: false,
                exception: exception,
                processedEvent: processedEvent,
                shouldRetry: true,
                retryCount: retryCount,
                retryDelay: retryDelay == default ? TimeSpan.FromSeconds(Math.Pow(2, retryCount)) : retryDelay
            );
        }

        /// <inheritdoc />
        public bool IsSuccess { get; } = isSuccess;

        /// <inheritdoc />
        public Exception? Exception { get; } = exception;

        /// <inheritdoc />
        public IEvent ProcessedEvent { get; } = processedEvent;

        /// <inheritdoc />
        public bool ShouldRetry { get; } = shouldRetry;

        /// <inheritdoc />
        public int RetryCount { get; } = retryCount;

        /// <inheritdoc />
        public TimeSpan RetryDelay { get; } = retryDelay;
    }
}