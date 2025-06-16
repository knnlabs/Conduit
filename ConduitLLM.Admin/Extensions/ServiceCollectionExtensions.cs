using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Admin.Security;
using ConduitLLM.Admin.Services;

using Microsoft.AspNetCore.Authorization;

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
        // Register authorization policy for master key
        services.AddSingleton<IAuthorizationHandler, MasterKeyAuthorizationHandler>();
        services.AddAuthorization(options =>
        {
            options.AddPolicy("MasterKeyPolicy", policy =>
                policy.Requirements.Add(new MasterKeyRequirement()));
        });

        // Register services
        services.AddScoped<IAdminVirtualKeyService, AdminVirtualKeyService>();
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
        services.AddScoped<IAdminProviderCredentialService, AdminProviderCredentialService>();

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
