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

        /// <summary>
        /// Adds correlation propagation to an HTTP client.
        /// </summary>
        /// <param name="builder">The HTTP client builder.</param>
        /// <returns>The HTTP client builder.</returns>
        public static IHttpClientBuilder AddCorrelationPropagation(this IHttpClientBuilder builder)
        {
            return builder.AddHttpMessageHandler<CorrelationPropagationHandler>();
        }

        /// <summary>
        /// Configures all HTTP clients to use correlation propagation.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection.</returns>
        public static IServiceCollection ConfigureHttpClientsWithCorrelation(this IServiceCollection services)
        {
            // Configure the default HttpClient factory to use correlation propagation
            services.ConfigureAll<Microsoft.Extensions.Http.HttpClientFactoryOptions>(options =>
            {
                options.HttpMessageHandlerBuilderActions.Add(builder =>
                {
                    var correlationHandler = builder.Services.GetService<CorrelationPropagationHandler>();
                    if (correlationHandler != null)
                    {
                        builder.AdditionalHandlers.Add(correlationHandler);
                    }
                });
            });

            return services;
        }
    }
}