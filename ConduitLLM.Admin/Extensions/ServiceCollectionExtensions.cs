using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Admin.Security;
using ConduitLLM.Admin.Services;
using ConduitLLM.Core.Interfaces; // For IVirtualKeyCache
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
