namespace HydroGarden.Foundation.Abstractions.Interfaces.ErrorEventTransformation
{
    public interface IRecoveryEvent
    {
        /// <summary>
        /// Gets or sets the unique identifier for this recovery event.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the device ID associated with this recovery.
        /// </summary>
        public Guid DeviceId { get; set; }

        /// <summary>
        /// Gets or sets the error code being recovered.
        /// </summary>
        public string ErrorCode { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the recovery was attempted.
        /// </summary>
        public DateTimeOffset Timestamp { get; set; }

        /// <summary>
        /// Gets or sets whether the recovery was successful.
        /// </summary>
        public bool IsSuccessful { get; set; }

        /// <summary>
        /// Gets or sets a message describing the recovery action or result.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Gets or sets the correlation identifier for tracing.
        /// </summary>
        public Guid CorrelationId { get; set; }

        /// <summary>
        /// Gets or sets additional contextual information about the recovery.
        /// </summary>
        public Dictionary<string, object> Context { get; set; }
    }
}
