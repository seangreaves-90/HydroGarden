using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Foundation.ErrorHandling.Core.Adapters;
using HydroGarden.Foundation.ErrorHandling.Core.Interfaces;
using HydroGarden.Logger.Abstractions;
using System;

namespace HydroGarden.Foundation.ErrorHandling.Core.Extensions
{
    /// <summary>
    /// Extension methods for the error monitor to integrate with events.
    /// </summary>
    public static class ErrorMonitorExtensions
    {
        /// <summary>
        /// Connects the error monitor to the event system, enabling bidirectional flow
        /// of error information between the systems.
        /// </summary>
        /// <param name="errorMonitor">The error monitor to connect.</param>
        /// <param name="eventBus">The event bus to connect to.</param>
        /// <param name="transformationService">The service that transforms errors to events and back.</param>
        /// <param name="logger">The logger to use.</param>
        /// <returns>An adapter that manages the connection between the systems.</returns>
        public static ErrorMonitorEventAdapter ConnectToEventSystem(
            this IErrorMonitor errorMonitor,
            IEventBus eventBus,
            IErrorEventTransformationService transformationService,
            ILogger logger)
        {
            if (errorMonitor == null)
                throw new ArgumentNullException(nameof(errorMonitor));
            
            if (eventBus == null)
                throw new ArgumentNullException(nameof(eventBus));
            
            if (transformationService == null)
                throw new ArgumentNullException(nameof(transformationService));
            
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));

            var adapter = new ErrorMonitorEventAdapter(
                errorMonitor,
                transformationService,
                eventBus,
                logger);

            adapter.Initialize();
            
            return adapter;
        }
    }
}