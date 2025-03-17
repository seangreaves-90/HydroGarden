using HydroGarden.Foundation.Abstractions.Interfaces;
using HydroGarden.Foundation.Abstractions.Interfaces.Events;

namespace HydroGarden.Foundation.Common.Events
{
    /// <summary>
    /// Default implementation of the publish result.
    /// </summary>
    public class PublishResult : IPublishResult
    {
        private readonly List<Exception> _errors;

        /// <summary>
        /// Initializes a new instance of the <see cref="PublishResult"/> class.
        /// </summary>
        public PublishResult()
        {
            _errors = new List<Exception>();
            HandlerTasks = new List<Task>();
        }

        /// <summary>
        /// Gets a value indicating whether the operation had errors.
        /// </summary>
        public bool HasErrors => _errors.Count > 0;

        /// <summary>
        /// Gets the errors that occurred during publishing.
        /// </summary>
        public IReadOnlyList<Exception> Errors => _errors.AsReadOnly();

        /// <summary>
        /// Gets the ID of the event that was published.
        /// </summary>
        public Guid EventId { get; set; }

        /// <summary>
        /// Gets the number of handlers the event was delivered to.
        /// </summary>
        public int HandlerCount { get; set; }

        /// <summary>
        /// Gets the number of handlers that successfully processed the event.
        /// </summary>
        public int SuccessCount { get; set; }

        /// <summary>
        /// Gets a value indicating whether the operation timed out.
        /// </summary>
        public bool TimedOut { get; set; }

        /// <summary>
        /// Gets the handler tasks that are processing the event.
        /// </summary>
        public List<Task> HandlerTasks { get; }

        /// <summary>
        /// Creates a failed publish result.
        /// </summary>
        /// <param name="eventId">The ID of the event.</param>
        /// <param name="exception">The exception that caused the failure.</param>
        /// <returns>The publish result indicating failure.</returns>
        public static PublishResult Failure(Guid eventId, Exception exception)
        {
            var result = new PublishResult
            {
                EventId = eventId,
                HandlerCount = 1,
                SuccessCount = 0
            };

            result._errors.Add(exception);
            return result;
        }

        /// <summary>
        /// Creates a successful publish result.
        /// </summary>
        /// <param name="eventId">The ID of the event.</param>
        /// <param name="handlerCount">The number of handlers the event was delivered to.</param>
        /// <returns>The publish result indicating success.</returns>
        public static PublishResult Success(Guid eventId, int handlerCount)
        {
            return new PublishResult
            {
                EventId = eventId,
                HandlerCount = handlerCount,
                SuccessCount = handlerCount
            };
        }

        public void AddError(Exception exception)
        {
            _errors.Add(exception);
        }
    }
}