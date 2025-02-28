/// <summary>
/// Represents a connection between two components
/// </summary>
public interface IComponentConnection
{
    /// <summary>
    /// Unique identifier for this connection
    /// </summary>
    Guid ConnectionId { get; }

    /// <summary>
    /// The source component ID
    /// </summary>
    Guid SourceId { get; }

    /// <summary>
    /// The target component ID
    /// </summary>
    Guid TargetId { get; }

    /// <summary>
    /// The type of connection
    /// </summary>
    string ConnectionType { get; }

    /// <summary>
    /// Whether the connection is enabled
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Optional condition that determines when events can flow through this connection
    /// </summary>
    string? Condition { get; }

    /// <summary>
    /// Optional metadata for this connection
    /// </summary>
    IDictionary<string, object>? Metadata { get; }
}