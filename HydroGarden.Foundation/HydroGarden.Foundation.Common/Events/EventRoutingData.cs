using HydroGarden.Foundation.Abstractions.Interfaces.Events;

namespace HydroGarden.Foundation.Common.Events
{
    /// <summary>
    /// Contains routing information for events
    /// </summary>
    public class EventRoutingData : IEventRoutingData
    {
        /// <inheritdoc />
        public Guid[] TargetIds { get; set; } = Array.Empty<Guid>();

        /// <inheritdoc />
        public bool Persist { get; set; } = false;

        /// <inheritdoc />
        public EventPriority Priority { get; set; } = EventPriority.Normal;

        /// <inheritdoc />
        public bool RequiresAcknowledgment { get; set; } = false;

        /// <inheritdoc />
        public TimeSpan? Timeout { get; set; }

        /// <summary>
        /// Creates a new instance of EventRoutingData with default values.
        /// </summary>
        public EventRoutingData()
        {
            TargetIds = Array.Empty<Guid>();
            Persist = false;
            Priority = EventPriority.Normal;
            RequiresAcknowledgment = false;
            Timeout = null;
        }

        /// <summary>
        /// Creates a new instance of EventRoutingData with the specified target IDs.
        /// </summary>
        public EventRoutingData(params Guid[] targetIds)
        {
            TargetIds = targetIds;
            Persist = false;
            Priority = EventPriority.Normal;
            RequiresAcknowledgment = false;
            Timeout = null;
        }

        /// <summary>
        /// Creates a builder for fluent configuration of event routing data.
        /// </summary>
        public static EventRoutingDataBuilder CreateBuilder()
        {
            return new EventRoutingDataBuilder();
        }
    }

    /// <summary>
    /// Builder class for fluent configuration of event routing data.
    /// </summary>
    public class EventRoutingDataBuilder
    {
        private readonly EventRoutingData _data = new();

        /// <summary>
        /// Configures the specific target component IDs that should receive this event.
        /// </summary>
        public EventRoutingDataBuilder WithTargets(params Guid[] targetIds)
        {
            _data.TargetIds = targetIds;
            return this;
        }

        /// <summary>
        /// Configures whether the event should be persisted.
        /// </summary>
        public EventRoutingDataBuilder WithPersistence(bool persist = true)
        {
            _data.Persist = persist;
            return this;
        }

        /// <summary>
        /// Configures the priority of the event which affects the order of processing.
        /// </summary>
        public EventRoutingDataBuilder WithPriority(EventPriority priority)
        {
            _data.Priority = priority;
            return this;
        }

        /// <summary>
        /// Configures the event as critical priority.
        /// </summary>
        public EventRoutingDataBuilder AsCritical()
        {
            _data.Priority = EventPriority.Critical;
            return this;
        }

        /// <summary>
        /// Configures the event as high priority.
        /// </summary>
        public EventRoutingDataBuilder AsHighPriority()
        {
            _data.Priority = EventPriority.High;
            return this;
        }

        /// <summary>
        /// Configures the event as low priority.
        /// </summary>
        public EventRoutingDataBuilder AsLowPriority()
        {
            _data.Priority = EventPriority.Low;
            return this;
        }

        /// <summary>
        /// Configures whether the publisher requires acknowledgment of event delivery.
        /// </summary>
        public EventRoutingDataBuilder WithAcknowledgment(bool requiresAcknowledgment = true)
        {
            _data.RequiresAcknowledgment = requiresAcknowledgment;
            return this;
        }

        /// <summary>
        /// Configures the maximum time to wait for event processing to complete when
        /// RequiresAcknowledgment is true.
        /// </summary>
        public EventRoutingDataBuilder WithTimeout(TimeSpan timeout)
        {
            _data.Timeout = timeout;
            return this;
        }

        /// <summary>
        /// Builds and returns the configured event routing data.
        /// </summary>
        public EventRoutingData Build()
        {
            return _data;
        }
    }
    }
