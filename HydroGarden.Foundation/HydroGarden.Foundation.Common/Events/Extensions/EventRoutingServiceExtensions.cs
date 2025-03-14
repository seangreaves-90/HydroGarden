using HydroGarden.Foundation.Abstractions.Interfaces.Events.Routing;
using HydroGarden.Foundation.Abstractions.Interfaces.Services;
using HydroGarden.Foundation.Common.Events.Routing;
using HydroGarden.Logger.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Collections.Generic;

namespace HydroGarden.Foundation.Common.Events.Extensions
{
    /// <summary>
    /// Extension methods for registering event routing services.
    /// </summary>
    public static class EventRoutingServiceExtensions
    {
        /// <summary>
        /// Adds a direct event router that doesn't use topology information.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection.</returns>
        public static IServiceCollection AddDirectEventRouter(this IServiceCollection services)
        {
            services.TryAddSingleton<IEventRouter>(sp => 
                new DirectEventRouter(sp.GetRequiredService<ILogger>()));
            
            return services;
        }

        /// <summary>
        /// Adds a topology-aware event router that uses the topology service for routing decisions.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection.</returns>
        public static IServiceCollection AddTopologyEventRouter(this IServiceCollection services)
        {
            // First register a direct router as a fallback
            services.TryAddSingleton<DirectEventRouter>(sp => 
                new DirectEventRouter(sp.GetRequiredService<ILogger>()));

            // Then register the topology router as the primary IEventRouter
            services.TryAddSingleton<IEventRouter>(sp => 
                new TopologyEventRouter(
                    sp.GetRequiredService<ILogger>(),
                    sp.GetRequiredService<ITopologyService>(),
                    sp.GetRequiredService<DirectEventRouter>()));
            
            return services;
        }

        /// <summary>
        /// Adds a composite event router that combines multiple routing strategies.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="strategy">The matching strategy to use.</param>
        /// <param name="routerFactories">Factory methods for creating the child routers.</param>
        /// <returns>The service collection.</returns>
        public static IServiceCollection AddCompositeEventRouter(
            this IServiceCollection services,
            CompositeEventRouter.MatchingStrategy strategy = CompositeEventRouter.MatchingStrategy.Any,
            params Func<IServiceProvider, IEventRouter>[] routerFactories)
        {
            if (routerFactories == null || routerFactories.Length == 0)
            {
                throw new ArgumentException("At least one router factory must be provided", nameof(routerFactories));
            }

            services.TryAddSingleton<IEventRouter>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger>();
                var routers = new List<IEventRouter>();

                foreach (var factory in routerFactories)
                {
                    routers.Add(factory(sp));
                }

                return new CompositeEventRouter(logger, routers, strategy);
            });

            return services;
        }

        /// <summary>
        /// Adds all event routing services with a recommended default configuration.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection.</returns>
        public static IServiceCollection AddEventRoutingServices(this IServiceCollection services)
        {
            // Register the DirectEventRouter as a service
            services.TryAddSingleton<DirectEventRouter>(sp => 
                new DirectEventRouter(sp.GetRequiredService<ILogger>()));

            // Check if the topology service is available
            var serviceDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ITopologyService));
            
            if (serviceDescriptor != null)
            {
                // Register the TopologyEventRouter as primary router
                services.TryAddSingleton<IEventRouter>(sp => 
                    new TopologyEventRouter(
                        sp.GetRequiredService<ILogger>(),
                        sp.GetRequiredService<ITopologyService>(),
                        sp.GetRequiredService<DirectEventRouter>()));
            }
            else
            {
                // No topology service, use DirectEventRouter as primary router
                services.TryAddSingleton<IEventRouter>(sp => 
                    sp.GetRequiredService<DirectEventRouter>());
            }
            
            return services;
        }
    }
}