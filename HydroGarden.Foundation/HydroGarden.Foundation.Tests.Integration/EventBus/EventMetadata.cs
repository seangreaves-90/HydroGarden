using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using System.Collections;

namespace HydroGarden.Foundation.Tests.Integration.EventBus
{
    /// <summary>
    /// A simple implementation of event metadata for testing purposes
    /// </summary>
    public class EventMetadata : Dictionary<string, object>
    {
        /// <summary>
        /// Creates a new empty event metadata object
        /// </summary>
        public EventMetadata() : base() { }

        /// <summary>
        /// Creates a new event metadata object with initial values
        /// </summary>
        public EventMetadata(IDictionary<string, object> initialValues) : base(initialValues) { }

        /// <summary>
        /// Adds an original event type to the metadata
        /// </summary>
        public EventMetadata WithOriginalEventType(EventType originalType)
        {
            this["OriginalEventType"] = originalType;
            return this;
        }

        /// <summary>
        /// Adds a transformation timestamp to the metadata
        /// </summary>
        public EventMetadata WithTransformationTimestamp()
        {
            this["TransformedAt"] = DateTime.UtcNow;
            return this;
        }

        /// <summary>
        /// Adds a processing node identifier to the metadata
        /// </summary>
        public EventMetadata WithProcessingNode(string nodeName)
        {
            this["ProcessingNode"] = nodeName;
            return this;
        }

        /// <summary>
        /// Adds a version to the metadata
        /// </summary>
        public EventMetadata WithVersion(string version)
        {
            this["Version"] = version;
            return this;
        }
    }
}
