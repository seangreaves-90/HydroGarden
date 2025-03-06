namespace HydroGarden.Foundation.Abstractions.Interfaces
{
    /// <summary>
    /// Result of an event publish operation
    /// </summary>
    public interface IPublishResult
    {
        /// <summary>
        /// The ID of the published event
        /// </summary>
        public Guid EventId { get; set; }

        /// <summary>
        /// Number of handlers that were selected for this event
        /// </summary>
        public int HandlerCount { get; set; }

        /// <summary>
        /// Number of handlers that successfully processed the event
        /// </summary>
        public int SuccessCount { get; set; }

        /// <summary>
        /// Whether all handlers successfully processed the event
        /// </summary>
        public bool IsComplete => SuccessCount == HandlerCount && !TimedOut;

        /// <summary>
        /// Whether the event processing timed out
        /// </summary>
        public bool TimedOut { get; set; }

        /// <summary>
        /// List of errors that occurred during event processing
        /// </summary>
        public List<Exception?> Errors { get; set; }

        /// <summary>
        /// Whether any errors occurred during event processing
        /// </summary>
        public bool HasErrors => Errors.Count > 0;

        /// <summary>
        /// Tasks representing the asynchronous processing of the event by handlers.
        /// Used for tracking completion status.
        /// </summary>
        public List<Task> HandlerTasks { get; }
    }
}
