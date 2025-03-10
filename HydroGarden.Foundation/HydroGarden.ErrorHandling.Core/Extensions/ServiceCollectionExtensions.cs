using HydroGarden.Foundation.ErrorHandling.Core.Interfaces;
using HydroGarden.Foundation.ErrorHandling.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace HydroGarden.Foundation.ErrorHandling.Core.Extensions
{
    /// <summary>
    /// Extension methods for registering error handling components with dependency injection.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds the error-event transformation service to the service collection.
        /// </summary>
        /// <param name="services">The service collection to add to.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddErrorEventTransformation(this IServiceCollection services)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            services.AddTransient<IErrorEventTransformationService, ErrorEventTransformationService>();

            return services;
        }
    }
}