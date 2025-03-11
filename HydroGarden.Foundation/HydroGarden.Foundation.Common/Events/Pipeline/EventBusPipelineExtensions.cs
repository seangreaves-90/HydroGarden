using HydroGarden.Foundation.Abstractions.Interfaces.Events;
using HydroGarden.Logger.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace HydroGarden.Foundation.Common.Events.Pipeline
{
    /// <summary>
    /// Extension methods for integrating the event processing pipeline with the event bus.
    /// </summary>
    public static class EventBusPipelineExtensions
    {
        /// <summary>
        /// Configures and attaches an event processing pipeline to the event bus.
        /// </summary>
        /// <param name="eventBus">The event bus.</param>
        /// <param name="logger">The logger to use.</param>
        /// <param name="configureAction">An optional action to configure the pipeline.</param>
        /// <returns>The configured event processing pipeline.</returns>
        public static IEventProcessingPipeline UsePipeline(
            this IEventBus eventBus,
            ILogger logger,
            Action<EventPipelineBuilder>? configureAction = null)
        {
            ArgumentNullException.ThrowIfNull(eventBus);

            ArgumentNullException.ThrowIfNull(logger);

            var builder = new EventPipelineBuilder(logger);
            
            // Apply default configuration if no custom configuration is provided
            if (configureAction == null)
            {
                builder.UseStandardPipeline();
            }
            else
            {
                configureAction(builder);
            }

            var pipeline = builder.Build();
            
            //// Attach the pipeline to the EventBus
            //if (eventBus is EventBus bus)
            //{
            //    bus.SetEventProcessingPipeline(pipeline);
            //}
            //else
            //{
            //    logger.Log($"Warning: Cannot attach pipeline to EventBus of type {eventBus.GetType().Name}. The EventBus must be an instance of {nameof(EventBus)}.");
            //}

            return pipeline;
        }

        /// <summary>
        /// Registers event processing pipeline services with the dependency injection container.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configureAction">An optional action to configure the pipeline.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddEventProcessingPipeline(
            this IServiceCollection services,
            Action<EventPipelineBuilder>? configureAction = null)
        {
            // Register the pipeline builder
            services.AddSingleton<EventPipelineBuilder>();
            
            // Register the pipeline
            services.AddSingleton<IEventProcessingPipeline>(provider =>
            {
                var builder = provider.GetRequiredService<EventPipelineBuilder>();
                
                if (configureAction != null)
                {
                    configureAction(builder);
                }
                else
                {
                    builder.UseStandardPipeline();
                }
                
                return builder.Build();
            });

            return services;
        }
    }
}