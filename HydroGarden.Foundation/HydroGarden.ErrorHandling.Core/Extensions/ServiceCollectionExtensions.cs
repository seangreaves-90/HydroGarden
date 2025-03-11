using HydroGarden.ErrorHandling.Core.RecoveryStrategy;
using HydroGarden.ErrorHandling.Core.Services;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling.RecoveryStrategy;
using Microsoft.Extensions.DependencyInjection;

namespace HydroGarden.ErrorHandling.Core.Extensions
{
    /// <summary>
    /// Extension methods for registering error handling and recovery services with dependency injection.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds the Recovery Orchestration Service and its dependencies to the service collection.
        /// </summary>
        /// <param name="services">The service collection to add services to.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddRecoveryOrchestration(this IServiceCollection services)
        {
            // Register the main recovery orchestration service
            services.AddSingleton<IRecoveryOrchestrationService, RecoveryOrchestrationService>();
            
            // Register recovery strategies
            services.AddSingleton<IRecoveryStrategy, RestartComponentStrategy>();
            services.AddSingleton<IRecoveryStrategy, ReinitializeConfigurationStrategy>();
            services.AddSingleton<IRecoveryStrategy, CommunicationRecoveryStrategy>();
            services.AddSingleton<IRecoveryStrategy, CircuitBreakerRecoveryStrategy>();
            
            return services;
        }
        
        /// <summary>
        /// Adds a custom recovery strategy to the service collection.
        /// </summary>
        /// <typeparam name="TStrategy">The type of the strategy to add.</typeparam>
        /// <param name="services">The service collection to add the strategy to.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddRecoveryStrategy<TStrategy>(this IServiceCollection services)
            where TStrategy : class, IRecoveryStrategy
        {
            services.AddSingleton<IRecoveryStrategy, TStrategy>();
            return services;
        }
    }
}
