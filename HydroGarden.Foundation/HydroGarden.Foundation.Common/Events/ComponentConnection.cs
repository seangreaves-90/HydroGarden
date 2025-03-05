namespace HydroGarden.Foundation.Common.Events
{
    /// <summary>
    /// Implementation of a connection between components
    /// </summary>
    public class ComponentConnection : IComponentConnection
    {
        /// <inheritdoc />
        public Guid ConnectionId { get; set; }

        /// <inheritdoc />
        public Guid SourceId { get; set; }

        /// <inheritdoc />
        public Guid TargetId { get; set; }

        /// <inheritdoc />
        public string ConnectionType { get; set; } = "Default";

        /// <inheritdoc />
        public bool IsEnabled { get; set; } = true;

        /// <inheritdoc />
        public string? Condition { get; set; }

        /// <inheritdoc />
        public IDictionary<string, object>? Metadata { get; set; }
    }
}
