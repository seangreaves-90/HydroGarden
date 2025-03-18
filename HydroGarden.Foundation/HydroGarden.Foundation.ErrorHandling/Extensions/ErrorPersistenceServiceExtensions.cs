using HydroGarden.Foundation.Abstractions.Interfaces.ErrorEventTransformation;
using HydroGarden.Foundation.Abstractions.Interfaces.ErrorHandling;
using HydroGarden.Foundation.ErrorHandling.Repositories;
using HydroGarden.Logger.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HydroGarden.Foundation.ErrorHandling.Extensions
{
    /// <summary>
    /// Extension methods for registering error persistence services.
    /// </summary>
    public static class ErrorPersistenceServiceExtensions
    {
        /// <summary>
        /// Adds an in-memory error repository to the service collection.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection.</returns>
        public static IServiceCollection AddInMemoryErrorRepository(this IServiceCollection services)
        {
            services.TryAddSingleton<IErrorRepository, InMemoryErrorRepository>();
            return services;
        }

        /// <summary>
        /// Adds error persistence support to the error monitoring services.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="useInMemoryRepository">Whether to use an in-memory repository when no other is registered.</param>
        /// <returns>The service collection.</returns>
        public static IServiceCollection AddErrorPersistence(
            this IServiceCollection services,
            bool useInMemoryRepository = true)
        {
            // Check if the error repository is already registered
            var repositoryDescriptor = services.FirstOrDefault(d => 
                d.ServiceType == typeof(IErrorRepository));

            // If not registered and we should use in-memory, add it
            if (repositoryDescriptor == null && useInMemoryRepository)
            {
                services.AddInMemoryErrorRepository();
            }

            // Update the ErrorMonitor registration to use the repository
            var monitorDescriptor = services.FirstOrDefault(d => 
                d.ServiceType == typeof(IErrorMonitor));

            if (monitorDescriptor != null)
            {
                // Remove the existing registration
                services.Remove(monitorDescriptor);
            }

            // Add new registration that includes the repository
            services.TryAddSingleton<IErrorMonitor>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger>();
                var transformationService = sp.GetRequiredService<IErrorEventTransformationService>();
                var repository = sp.GetService<IErrorRepository>(); // Optional

                return new ErrorMonitor(logger, transformationService, repository);
            });

            return services;
        }
    }
}