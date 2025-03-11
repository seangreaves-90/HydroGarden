//using HydroGarden.Foundation.Abstractions.Interfaces.Events;
//using HydroGarden.Foundation.Common.Events.Pipeline.Middleware;
//using HydroGarden.Logger.Abstractions;
//using Microsoft.Extensions.DependencyInjection;
//using System;

//namespace HydroGarden.Foundation.Common.Events.Pipeline
//{
//    /// <summary>
//    /// Extension methods for registering event pipeline services with dependency injection.
//    /// </summary>
//    public static class EventPipelineServiceExtensions
//    {
//        /// <summary>
//        /// Adds event pipeline services to the service collection.
//        /// </summary>
//        /// <param name="services">The service collection.</param>
//        /// <param name="configureAction">An optional action to configure the pipeline.</param>
//        /// <returns>The service collection for chaining.</returns>
//        public static IServiceCollection AddEventPipeline(
//            this IServiceCollection services,
//            Action<EventPipelineBuilder> configureAction = null)
//        {
//            // Register the pipeline and middleware
//            services.AddSingleton<IEventProcessingPipeline>(provider =>
//            {
//                var logger = provider.GetRequiredService<ILogger>();
//                var builder = new EventPipelineBuilder(logger);
                
//                // Apply custom configuration or use standard pipeline
//                if (configureAction != null)
//                {
//                    configureAction(builder);
//                }
//                else
//                {
//                    builder.UseStandardPipeline();
//                }
                
//                return builder.Build();
//            });

//            // Register individual middleware types that might be requested separately
//            services.AddSingleton<LoggingMiddleware>(provider =>
//                new LoggingMiddleware(provider.GetRequiredService<ILogger>()));
            
//            services.AddSingleton<RetryMiddleware>(provider =>
//                new RetryMiddleware(provider.GetRequiredService<ILogger>()));
            
//            services.AddSingleton<CircuitBreakerMiddleware>(provider =>
//                new CircuitBreakerMiddleware(provider.GetRequiredService<ILogger>()));
            
//            services.AddSingleton<DeadLetterQueueMiddleware>(provider =>
//                new DeadLetterQueueMiddleware(provider.GetRequiredService<ILogger>()));

//            // Configure EventBus to use the pipeline when resolved
//            services.AddOptions<EventBusOptions>().Configure<IEventProcessingPipeline>((options, pipeline) =>
//            {
//                options.EventProcessingPipeline = pipeline;
//            });

//            return services;
//        }

//        /// <summary>
//        /// Integrates the event processing pipeline with the event bus.
//        /// </summary>
//        /// <param name="services">The service collection.</param>
//        /// <returns>The service collection for chaining.</returns>
//        public static IServiceCollection AddEventPipelineIntegration(this IServiceCollection services)
//        {
//            // Configure the EventBus to use the pipeline when created
//            services.AddSingleton<IEventBusConfigurator>(provider =>
//                new EventBusConfigurator(provider.GetRequiredService<IEventProcessingPipeline>()));

//            return services;
//        }

//        /// <summary>
//        /// Options for configuring an EventBus.
//        /// </summary>
//        public class EventBusOptions
//        {
//            /// <summary>
//            /// Gets or sets the event processing pipeline to use.
//            /// </summary>
//            public IEventProcessingPipeline EventProcessingPipeline { get; set; }
//        }

//        /// <summary>
//        /// Configures an EventBus with an event processing pipeline.
//        /// </summary>
//        private class EventBusConfigurator : IEventBusConfigurator
//        {
//            private readonly IEventProcessingPipeline _pipeline;

//            public EventBusConfigurator(IEventProcessingPipeline pipeline)
//            {
//                _pipeline = pipeline;
//            }

//            public void Configure(IEventBus eventBus)
//            {
//                if (eventBus is EventBus bus)
//                {
//                    bus.SetEventProcessingPipeline(_pipeline);
//                }
//            }
//        }

//        /// <summary>
//        /// Interface for configuring an EventBus.
//        /// </summary>
//        public interface IEventBusConfigurator
//        {
//            /// <summary>
//            /// Configures an EventBus.
//            /// </summary>
//            /// <param name="eventBus">The EventBus to configure.</param>
//            void Configure(IEventBus eventBus);
//        }
//    }
//}