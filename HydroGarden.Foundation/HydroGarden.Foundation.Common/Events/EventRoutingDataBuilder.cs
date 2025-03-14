using HydroGarden.Foundation.Abstractions.Interfaces.Events;

namespace HydroGarden.Foundation.Common.Events
{
    /// <summary>
    /// Builder for creating EventRoutingData using a fluent interface.
    /// </summary>
    public class EventRoutingDataBuilder
    {
        private readonly List<Guid> _targetIds = new();
        private bool _persist = false;
        private EventPriority _priority = EventPriority.Normal;
        private bool _requiresAcknowledgment = false;
        private TimeSpan? _timeout = null;

        /// <summary>
        /// Adds a target ID to the routing data.
        /// </summary>
        /// <param name="targetId">The target ID to add.</param>
        /// <returns>This builder, for method chaining.</returns>
        public EventRoutingDataBuilder AddTarget(Guid targetId)
        {
            _targetIds.Add(targetId);
            return this;
        }

        /// <summary>
        /// Adds multiple target IDs to the routing data.
        /// </summary>
        /// <param name="targetIds">The target IDs to add.</param>
        /// <returns>This builder, for method chaining.</returns>
        public EventRoutingDataBuilder AddTargets(IEnumerable<Guid> targetIds)
        {
            _targetIds.AddRange(targetIds);
            return this;
        }

        /// <summary>
        /// Sets whether the event should be persisted.
        /// </summary>
        /// <param name="persist">Whether to persist the event.</param>
        /// <returns>This builder, for method chaining.</returns>
        public EventRoutingDataBuilder WithPersistence(bool persist = true)
        {
            _persist = persist;
            return this;
        }

        /// <summary>
        /// Sets the priority of the event.
        /// </summary>
        /// <param name="priority">The priority.</param>
        /// <returns>This builder, for method chaining.</returns>
        public EventRoutingDataBuilder WithPriority(EventPriority priority)
        {
            _priority = priority;
            return this;
        }

        /// <summary>
        /// Sets the event priority to High.
        /// </summary>
        /// <returns>This builder, for method chaining.</returns>
        public EventRoutingDataBuilder AsHighPriority()
        {
            _priority = EventPriority.High;
            return this;
        }

        /// <summary>
        /// Sets the event priority to Critical.
        /// </summary>
        /// <returns>This builder, for method chaining.</returns>
        public EventRoutingDataBuilder AsCriticalPriority()
        {
            _priority = EventPriority.Critical;
            return this;
        }

        /// <summary>
        /// Sets whether the event requires acknowledgment.
        /// </summary>
        /// <param name="requiresAcknowledgment">Whether acknowledgment is required.</param>
        /// <returns>This builder, for method chaining.</returns>
        public EventRoutingDataBuilder RequiresAcknowledgment(bool requiresAcknowledgment = true)
        {
            _requiresAcknowledgment = requiresAcknowledgment;
            return this;
        }

        /// <summary>
        /// Sets the timeout for processing the event.
        /// </summary>
        /// <param name="timeout">The timeout.</param>
        /// <returns>This builder, for method chaining.</returns>
        public EventRoutingDataBuilder WithTimeout(TimeSpan timeout)
        {
            _timeout = timeout;
            return this;
        }

        /// <summary>
        /// Builds the EventRoutingData from the builder.
        /// </summary>
        /// <returns>The built EventRoutingData.</returns>
        public EventRoutingData Build()
        {
            return new EventRoutingData(
                _targetIds.ToArray(),
                _persist,
                _priority,
                _requiresAcknowledgment,
                _timeout);
        }
    }
}