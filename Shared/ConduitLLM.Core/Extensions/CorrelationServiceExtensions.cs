using ConduitLLM.Core.Http;
using ConduitLLM.Core.Services;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ConduitLLM.Core.Extensions
{
    /// <summary>
    /// Extension methods for configuring correlation services.
    /// </summary>
    public static class CorrelationServiceExtensions
    {
        /// <summary>
        /// Adds correlation context services to the service collection.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection.</returns>
        public static IServiceCollection AddCorrelationContext(this IServiceCollection services)
        {
            // Add HTTP context accessor if not already registered
            services.TryAddSingleton<Microsoft.AspNetCore.Http.IHttpContextAccessor, Microsoft.AspNetCore.Http.HttpContextAccessor>();
            
            // Add correlation context service
            services.TryAddScoped<ICorrelationContextService, CorrelationContextService>();
            
            // Add correlation propagation handler
            services.TryAddTransient<CorrelationPropagationHandler>();

            return services;
        }
    }
}