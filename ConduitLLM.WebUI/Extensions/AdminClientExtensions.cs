using System;

using ConduitLLM.WebUI.Interfaces;
using ConduitLLM.WebUI.Options;
using ConduitLLM.WebUI.Services;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ConduitLLM.WebUI.Extensions
{
    /// <summary>
    /// Extension methods for registering Admin API client services.
    /// </summary>
    public static class AdminClientExtensions
    {
        /// <summary>
        /// Adds the Admin API client to the service collection.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">The configuration.</param>
        /// <returns>The service collection.</returns>
        public static IServiceCollection AddAdminApiClient(this IServiceCollection services, IConfiguration configuration)
        {
            // Configure Admin API options
            services.Configure<AdminApiOptions>(options =>
            {
                var section = configuration.GetSection("AdminApi");
                section.Bind(options);

                // Allow environment variable override
                var baseUrl = configuration["CONDUIT_ADMIN_API_BASE_URL"] ?? configuration["CONDUIT_ADMIN_API_URL"];
                if (!string.IsNullOrEmpty(baseUrl))
                {
                    options.BaseUrl = baseUrl;
                    Console.WriteLine($"[AdminClient] Admin API URL set to: {baseUrl}");
                }

                var masterKey = configuration["CONDUIT_MASTER_KEY"] ?? configuration["AdminApi__MasterKey"];
                if (!string.IsNullOrEmpty(masterKey))
                {
                    options.MasterKey = masterKey;
                    Console.WriteLine($"[AdminClient] Using master key with length: {masterKey.Length}");
                }
                else
                {
                    Console.WriteLine("[AdminClient] WARNING: No master key found in configuration!");
                }

                var useAdminApi = configuration["CONDUIT_USE_ADMIN_API"];
                if (!string.IsNullOrEmpty(useAdminApi) && bool.TryParse(useAdminApi, out var useAdminApiValue))
                {
                    options.UseAdminApi = useAdminApiValue;
                }
            });

            // Register HTTP client for AdminApiClient with resilience policies
            services.AddHttpClient<AdminApiClient>()
                .AddAdminApiResiliencePolicies();

            // Register as interfaces
            services.AddScoped<IAdminApiClient>(sp => sp.GetRequiredService<AdminApiClient>());
            services.AddScoped<IProviderCredentialService>(sp => sp.GetRequiredService<AdminApiClient>());

            return services;
        }

    }
}
