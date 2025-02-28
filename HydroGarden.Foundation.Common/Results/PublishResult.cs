

using HydroGarden.Foundation.Abstractions.Interfaces;

namespace HydroGarden.Foundation.Common.Results
{
    /// <summary>
    /// Result of an event publish operation
    /// </summary>
    public class PublishResult : IPublishResult
    {
        /// <inheritdoc />
        public Guid EventId { get; set; }

        /// <inheritdoc />
        public int HandlerCount { get; set; }

        /// <inheritdoc />
        public int SuccessCount { get; set; }

        /// <inheritdoc />
        public bool IsComplete => SuccessCount == HandlerCount && !TimedOut;

        /// <inheritdoc />
        public bool TimedOut { get; set; }

        /// <inheritdoc />
        public List<Exception> Errors { get; set; } = new();

        /// <inheritdoc />
        public bool HasErrors => Errors.Count > 0;
    }
}
