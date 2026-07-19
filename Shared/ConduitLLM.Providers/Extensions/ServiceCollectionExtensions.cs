using ConduitLLM.Core.Configuration;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Services;

using Microsoft.Extensions.DependencyInjection;

namespace ConduitLLM.Providers.Extensions
{
    /// <summary>
    /// Extension methods for configuring provider services with dependency injection.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds provider services to the service collection.
        /// </summary>
        /// <param name="services">The service collection to add services to.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddProviderServices(this IServiceCollection services)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            // Register LLM client factory
            services.AddScoped<ILLMClientFactory, DatabaseAwareLLMClientFactory>();

            // Every host that creates provider clients needs the named HttpClients with the
            // resilience pipeline — registering here (idempotently) means no host can forget
            services.AddLLMProviderHttpClients();

            // In-request failover (default off; enable via CONDUIT_FAILOVER_ENABLED or
            // Conduit:Failover:Enabled). The attribution accessor is request-scoped so gateway
            // middleware can read which candidate actually served the request.
            services.AddOptions<FailoverOptions>()
                .BindConfiguration(FailoverOptions.SectionName)
                .ValidateDataAnnotations();
            services.PostConfigure<FailoverOptions>(options =>
            {
                if (bool.TryParse(Environment.GetEnvironmentVariable("CONDUIT_FAILOVER_ENABLED"), out var enabled))
                {
                    options.Enabled = enabled;
                }
                if (bool.TryParse(Environment.GetEnvironmentVariable("CONDUIT_PROVIDER_FAILOVER_ENABLED"), out var providerEnabled))
                {
                    options.ProviderFailoverEnabled = providerEnabled;
                }
            });
            services.AddScoped<IFailoverAttributionAccessor, FailoverAttributionAccessor>();

            // OBSOLETE: External model discovery is no longer used. 
            // The ProviderModelsController now returns models from the local database.
            // services.AddScoped<ModelListService>();

            // Ensure memory cache is registered
            services.AddMemoryCache();

            return services;
        }
    }
}
