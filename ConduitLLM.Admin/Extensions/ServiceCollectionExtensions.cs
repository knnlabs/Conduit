using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Admin.Security;
using ConduitLLM.Admin.Services;
using ConduitLLM.Core.Interfaces; // For IVirtualKeyCache and ILLMClientFactory
using ConduitLLM.Configuration.Repositories; // For repository interfaces

using MassTransit; // For IPublishEndpoint
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;

namespace ConduitLLM.Admin.Extensions;

/// <summary>
/// Extension methods for configuring Admin API services in the dependency injection container
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds all Admin API services to the dependency injection container
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The application configuration</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddAdminServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure security options from environment variables
        services.ConfigureAdminSecurityOptions(configuration);

        // Register security service
        services.AddSingleton<ISecurityService, SecurityService>();

        // Add memory cache if not already registered
        services.AddMemoryCache();

        // Register authorization policy for master key
        services.AddSingleton<IAuthorizationHandler, MasterKeyAuthorizationHandler>();
        services.AddAuthorization(options =>
        {
            options.AddPolicy("MasterKeyPolicy", policy =>
                policy.Requirements.Add(new MasterKeyRequirement()));
        });

        // Register AdminVirtualKeyService with optional cache and event publishing dependencies
        services.AddScoped<IAdminVirtualKeyService>(serviceProvider =>
        {
            var virtualKeyRepository = serviceProvider.GetRequiredService<IVirtualKeyRepository>();
            var spendHistoryRepository = serviceProvider.GetRequiredService<IVirtualKeySpendHistoryRepository>();
            var cache = serviceProvider.GetService<IVirtualKeyCache>(); // Optional - null if not registered
            var publishEndpoint = serviceProvider.GetService<IPublishEndpoint>(); // Optional - null if MassTransit not configured
            var logger = serviceProvider.GetRequiredService<ILogger<AdminVirtualKeyService>>();
            
            return new AdminVirtualKeyService(virtualKeyRepository, spendHistoryRepository, cache, publishEndpoint, logger);
        });
        services.AddScoped<IAdminModelProviderMappingService, AdminModelProviderMappingService>();
        services.AddScoped<IAdminRouterService, AdminRouterService>();
        services.AddScoped<IAdminLogService, AdminLogService>();
        services.AddScoped<IAdminIpFilterService, AdminIpFilterService>();
        services.AddScoped<IAdminCostDashboardService, AdminCostDashboardService>();
        services.AddScoped<IAdminDatabaseBackupService, AdminDatabaseBackupService>();
        services.AddScoped<IAdminSystemInfoService, AdminSystemInfoService>();
        services.AddScoped<IAdminNotificationService, AdminNotificationService>();
        services.AddScoped<IAdminGlobalSettingService, AdminGlobalSettingService>();
        services.AddScoped<IAdminProviderHealthService, AdminProviderHealthService>();
        services.AddScoped<IAdminModelCostService, AdminModelCostService>();
        // Register AdminProviderCredentialService with optional event publishing dependency
        services.AddScoped<IAdminProviderCredentialService>(serviceProvider =>
        {
            var providerCredentialRepository = serviceProvider.GetRequiredService<IProviderCredentialRepository>();
            var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
            var publishEndpoint = serviceProvider.GetService<IPublishEndpoint>(); // Optional - null if MassTransit not configured
            var logger = serviceProvider.GetRequiredService<ILogger<AdminProviderCredentialService>>();
            
            return new AdminProviderCredentialService(providerCredentialRepository, httpClientFactory, publishEndpoint, logger);
        });

        // Register audio-related services
        services.AddScoped<IAdminAudioProviderService, AdminAudioProviderService>();
        services.AddScoped<IAdminAudioCostService, AdminAudioCostService>();
        services.AddScoped<IAdminAudioUsageService, AdminAudioUsageService>();

        // Register database-aware LLM client factory (must be registered before discovery service)
        services.AddScoped<ILLMClientFactory, DatabaseAwareLLMClientFactory>();

        // Register enhanced model discovery providers
        // Configure HttpClients for each discovery provider
        services.AddHttpClient<ConduitLLM.Core.Services.OpenRouterDiscoveryProvider>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("User-Agent", "Conduit-LLM-Admin/1.0");
        });

        services.AddHttpClient<ConduitLLM.Core.Services.AnthropicDiscoveryProvider>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("User-Agent", "Conduit-LLM-Admin/1.0");
        });

        // Register discovery providers as concrete implementations first
        services.AddScoped<ConduitLLM.Core.Services.OpenRouterDiscoveryProvider>(serviceProvider =>
        {
            var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient(nameof(ConduitLLM.Core.Services.OpenRouterDiscoveryProvider));
            var logger = serviceProvider.GetRequiredService<ILogger<ConduitLLM.Core.Services.OpenRouterDiscoveryProvider>>();
            var credentialService = serviceProvider.GetRequiredService<ConduitLLM.Configuration.IProviderCredentialService>();
            
            // Get API key from provider credentials
            try
            {
                var credential = credentialService.GetCredentialByProviderNameAsync("openrouter").GetAwaiter().GetResult();
                var apiKey = credential?.ApiKey;
                return new ConduitLLM.Core.Services.OpenRouterDiscoveryProvider(httpClient, logger, apiKey);
            }
            catch
            {
                // If we can't get credentials, still register the provider (it will fall back to patterns)
                return new ConduitLLM.Core.Services.OpenRouterDiscoveryProvider(httpClient, logger, null);
            }
        });

        services.AddScoped<ConduitLLM.Core.Services.AnthropicDiscoveryProvider>(serviceProvider =>
        {
            var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient(nameof(ConduitLLM.Core.Services.AnthropicDiscoveryProvider));
            var logger = serviceProvider.GetRequiredService<ILogger<ConduitLLM.Core.Services.AnthropicDiscoveryProvider>>();
            var credentialService = serviceProvider.GetRequiredService<ConduitLLM.Configuration.IProviderCredentialService>();
            
            // Get API key from provider credentials
            try
            {
                var credential = credentialService.GetCredentialByProviderNameAsync("anthropic").GetAwaiter().GetResult();
                var apiKey = credential?.ApiKey;
                return new ConduitLLM.Core.Services.AnthropicDiscoveryProvider(httpClient, logger, apiKey);
            }
            catch
            {
                // If we can't get credentials, still register the provider (it will fall back to patterns)
                return new ConduitLLM.Core.Services.AnthropicDiscoveryProvider(httpClient, logger, null);
            }
        });

        // Register the providers as IModelDiscoveryProvider interfaces
        services.AddScoped<ConduitLLM.Core.Interfaces.IModelDiscoveryProvider>(serviceProvider =>
            serviceProvider.GetRequiredService<ConduitLLM.Core.Services.OpenRouterDiscoveryProvider>());

        services.AddScoped<ConduitLLM.Core.Interfaces.IModelDiscoveryProvider>(serviceProvider =>
            serviceProvider.GetRequiredService<ConduitLLM.Core.Services.AnthropicDiscoveryProvider>());

        // Register discovery service
        services.AddScoped<IProviderDiscoveryService, ConduitLLM.Core.Services.ProviderDiscoveryService>();

        // Configure CORS for the Admin API
        services.AddCors(options =>
        {
            options.AddPolicy("AdminCorsPolicy", policy =>
            {
                policy.WithOrigins(
                        configuration.GetSection("AdminApi:AllowedOrigins").Get<string[]>() ??
                        new[] { "http://localhost:5000", "https://localhost:5001" })
                    .AllowAnyMethod()
                    .AllowAnyHeader();
            });
        });

        return services;
    }
}
