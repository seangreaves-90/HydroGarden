using HydroGarden.Foundation.Abstractions.Interfaces;

namespace HydroGarden.Foundation.Common.Events
{
    /// <summary>
    /// Represents the result of publishing an event.
    /// </summary>
    public class PublishResult : IPublishResult
    {
        /// <inheritdoc/>
        public Guid EventId { get; set; }

        /// <inheritdoc/>
        public int HandlerCount { get; set; }

        /// <inheritdoc/>
        public int SuccessCount { get; set; }

        /// <inheritdoc/>
        public bool TimedOut { get; set; }

        /// <inheritdoc/>
        public List<Exception?> Errors { get; set; } = new List<Exception?>();

        /// <inheritdoc/>
        public List<Task> HandlerTasks { get; } = new List<Task>();

        /// <summary>
        /// Gets whether any errors occurred during event publishing.
        /// </summary>
        public bool HasErrors => Errors.Count > 0;

        /// <summary>
        /// Creates a new instance of the <see cref="PublishResult"/> class for a successful publish.
        /// </summary>
        /// <param name="eventId">The ID of the published event.</param>
        /// <returns>A publish result indicating success.</returns>
        public static IPublishResult Success(Guid eventId)
        {
            return new PublishResult
            {
                EventId = eventId,
                HandlerCount = 0,
                SuccessCount = 0,
                TimedOut = false
            };
        }

        /// <summary>
        /// Creates a new instance of the <see cref="PublishResult"/> class for a failed publish.
        /// </summary>
        /// <param name="eventId">The ID of the published event.</param>
        /// <param name="error">The error that occurred.</param>
        /// <returns>A publish result indicating failure.</returns>
        public static IPublishResult Failure(Guid eventId, Exception error)
        {
            return new PublishResult
            {
                EventId = eventId,
                HandlerCount = 0,
                SuccessCount = 0,
                TimedOut = false,
                Errors = new List<Exception?> { error }
            };
        }
    }
}