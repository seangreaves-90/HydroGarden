using System.Collections.Concurrent;
using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Foundation.Abstractions.Interfaces.Services;
using HydroGarden.Foundation.Common.Events;
using HydroGarden.Logger.Abstractions;

namespace HydroGarden.Foundation.Core.Services
{
    /// <summary>
    /// Implementation of the topology service that uses the new typed event handlers
    /// </summary>
    public class TypedTopologyService : TopologyService, IEventHandler<PropertyChangedEvent>
    {
        private readonly IEventBus _eventBus;

        /// <summary>
        /// Creates a new typed topology service instance that subscribes to property changed events
        /// </summary>
        /// <param name="logger">Logger for recording events</param>
        /// <param name="propertyAccessService">Service for accessing component properties</param>
        /// <param name="topologyRepository">Repository for managing topology connections</param>
        /// <param name="eventBus">Event bus for subscribing to property change events</param>
        public TypedTopologyService(
            ILogger logger, 
            IPropertyAccessService propertyAccessService,
            ITopologyRepository topologyRepository,
            IEventBus eventBus)
            : base(logger, propertyAccessService, topologyRepository)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            
            // Subscribe to PropertyChangedEvent events
            _eventBus.Subscribe<PropertyChangedEvent>(this);
        }

        /// <summary>
        /// Handles property changed events to update topology connections if needed
        /// </summary>
        /// <param name="event">The property changed event</param>
        /// <param name="ct">Cancellation token</param>
        public async Task HandleAsync(PropertyChangedEvent @event, CancellationToken ct = default)
        {
            // Here we could implement reaction to property changes
            // For example, we could revalidate connections that depend on the changed property
            
            // This is just a placeholder implementation - you would need to add actual
            // logic to handle property changes that affect topology
        }
    }
}