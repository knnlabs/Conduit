using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Admin.Security;
using ConduitLLM.Admin.Services;
using ConduitLLM.Core.Interfaces; // For IVirtualKeyCache and ILLMClientFactory
using ConduitLLM.Configuration.Repositories; // For repository interfaces
using ConduitLLM.Configuration.Options;

using MassTransit; // For IPublishEndpoint
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
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

        // Add authentication with a custom scheme
        services.AddAuthentication("MasterKey")
            .AddScheme<MasterKeyAuthenticationSchemeOptions, MasterKeyAuthenticationHandler>("MasterKey", null);

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
            var modelProviderMappingRepository = serviceProvider.GetRequiredService<IModelProviderMappingRepository>();
            var modelCapabilityService = serviceProvider.GetRequiredService<IModelCapabilityService>();
            var mediaLifecycleService = serviceProvider.GetService<IMediaLifecycleService>(); // Optional - null if not configured
            
            return new AdminVirtualKeyService(virtualKeyRepository, spendHistoryRepository, cache, publishEndpoint, logger, modelProviderMappingRepository, modelCapabilityService, mediaLifecycleService);
        });
        // Register AdminModelProviderMappingService with optional event publishing dependency
        services.AddScoped<IAdminModelProviderMappingService>(serviceProvider =>
        {
            var mappingRepository = serviceProvider.GetRequiredService<IModelProviderMappingRepository>();
            var credentialRepository = serviceProvider.GetRequiredService<IProviderCredentialRepository>();
            var publishEndpoint = serviceProvider.GetService<IPublishEndpoint>(); // Optional - null if MassTransit not configured
            var logger = serviceProvider.GetRequiredService<ILogger<AdminModelProviderMappingService>>();
            
            return new AdminModelProviderMappingService(mappingRepository, credentialRepository, publishEndpoint, logger);
        });
        services.AddScoped<IAdminRouterService, AdminRouterService>();
        services.AddScoped<IAdminLogService, AdminLogService>();
        // Register AdminIpFilterService with optional event publishing dependency
        services.AddScoped<IAdminIpFilterService>(serviceProvider =>
        {
            var ipFilterRepository = serviceProvider.GetRequiredService<IIpFilterRepository>();
            var ipFilterOptions = serviceProvider.GetRequiredService<IOptionsMonitor<IpFilterOptions>>();
            var publishEndpoint = serviceProvider.GetService<IPublishEndpoint>(); // Optional - null if MassTransit not configured
            var logger = serviceProvider.GetRequiredService<ILogger<AdminIpFilterService>>();
            
            return new AdminIpFilterService(ipFilterRepository, ipFilterOptions, publishEndpoint, logger);
        });
        services.AddScoped<IAdminCostDashboardService, AdminCostDashboardService>();
        services.AddScoped<IAdminDatabaseBackupService, AdminDatabaseBackupService>();
        services.AddScoped<IAdminSystemInfoService, AdminSystemInfoService>();
        services.AddScoped<IAdminNotificationService, AdminNotificationService>();
        // Register AdminGlobalSettingService with optional event publishing dependency
        services.AddScoped<IAdminGlobalSettingService>(serviceProvider =>
        {
            var globalSettingRepository = serviceProvider.GetRequiredService<IGlobalSettingRepository>();
            var publishEndpoint = serviceProvider.GetService<IPublishEndpoint>(); // Optional - null if MassTransit not configured
            var logger = serviceProvider.GetRequiredService<ILogger<AdminGlobalSettingService>>();
            
            return new AdminGlobalSettingService(globalSettingRepository, publishEndpoint, logger);
        });
        services.AddScoped<IAdminProviderHealthService, AdminProviderHealthService>();
        // Register AdminModelCostService with optional event publishing dependency
        services.AddScoped<IAdminModelCostService>(serviceProvider =>
        {
            var modelCostRepository = serviceProvider.GetRequiredService<IModelCostRepository>();
            var requestLogRepository = serviceProvider.GetRequiredService<IRequestLogRepository>();
            var publishEndpoint = serviceProvider.GetService<IPublishEndpoint>(); // Optional - null if MassTransit not configured
            var logger = serviceProvider.GetRequiredService<ILogger<AdminModelCostService>>();
            
            return new AdminModelCostService(modelCostRepository, requestLogRepository, publishEndpoint, logger);
        });
        // Register AdminProviderCredentialService with optional event publishing dependency
        services.AddScoped<IAdminProviderCredentialService>(serviceProvider =>
        {
            var providerCredentialRepository = serviceProvider.GetRequiredService<IProviderCredentialRepository>();
            var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
            var configProviderCredentialService = serviceProvider.GetRequiredService<ConduitLLM.Configuration.IProviderCredentialService>();
            var llmClientFactory = serviceProvider.GetRequiredService<ConduitLLM.Core.Interfaces.ILLMClientFactory>();
            var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
            var publishEndpoint = serviceProvider.GetService<IPublishEndpoint>(); // Optional - null if MassTransit not configured
            var logger = serviceProvider.GetRequiredService<ILogger<AdminProviderCredentialService>>();
            
            return new AdminProviderCredentialService(providerCredentialRepository, httpClientFactory, configProviderCredentialService, llmClientFactory, loggerFactory, publishEndpoint, logger);
        });

        // Register Error Queue monitoring services
        services.AddSingleton<IRabbitMQManagementClient, RabbitMQManagementClient>();
        services.AddScoped<IErrorQueueService, ErrorQueueService>();

        // Register audio-related services
        services.AddScoped<IAdminAudioProviderService, AdminAudioProviderService>();
        services.AddScoped<IAdminAudioCostService, AdminAudioCostService>();
        services.AddScoped<IAdminAudioUsageService, AdminAudioUsageService>();

        // Register media management service (requires IMediaLifecycleService to be registered)
        services.AddScoped<IAdminMediaService>(serviceProvider =>
        {
            var mediaRepository = serviceProvider.GetRequiredService<IMediaRecordRepository>();
            var mediaLifecycleService = serviceProvider.GetService<IMediaLifecycleService>();
            var logger = serviceProvider.GetRequiredService<ILogger<AdminMediaService>>();
            
            // Only register if media lifecycle service is available
            if (mediaLifecycleService == null)
            {
                throw new InvalidOperationException("IMediaLifecycleService must be registered to use AdminMediaService");
            }
            
            return new AdminMediaService(mediaRepository, mediaLifecycleService, logger);
        });

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
                var credential = credentialService.GetCredentialByProviderTypeAsync(ConduitLLM.Configuration.ProviderType.OpenRouter).GetAwaiter().GetResult();
                var apiKey = credential?.ProviderKeyCredentials?.FirstOrDefault(k => k.IsPrimary && k.IsEnabled)?.ApiKey ??
                            credential?.ProviderKeyCredentials?.FirstOrDefault(k => k.IsEnabled)?.ApiKey;
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
                var credential = credentialService.GetCredentialByProviderTypeAsync(ConduitLLM.Configuration.ProviderType.Anthropic).GetAwaiter().GetResult();
                var apiKey = credential?.ProviderKeyCredentials?.FirstOrDefault(k => k.IsPrimary && k.IsEnabled)?.ApiKey ??
                            credential?.ProviderKeyCredentials?.FirstOrDefault(k => k.IsEnabled)?.ApiKey;
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

        // Register Media Lifecycle Service (optional - for virtual key cleanup)
        // Only register if we have a media storage service configured
        var storageProvider = configuration.GetValue<string>("ConduitLLM:Storage:Provider");
        if (!string.IsNullOrEmpty(storageProvider))
        {
            services.Configure<ConduitLLM.Core.Services.MediaManagementOptions>(
                configuration.GetSection("ConduitLLM:MediaManagement"));
            
            // Register media storage service based on configuration
            if (storageProvider.Equals("S3", StringComparison.OrdinalIgnoreCase))
            {
                services.Configure<ConduitLLM.Core.Options.S3StorageOptions>(
                    configuration.GetSection(ConduitLLM.Core.Options.S3StorageOptions.SectionName));
                services.AddSingleton<IMediaStorageService, ConduitLLM.Core.Services.S3MediaStorageService>();
            }
            
            services.AddScoped<IMediaLifecycleService, ConduitLLM.Core.Services.MediaLifecycleService>();
        }

        // Register provider health monitoring background service
        services.Configure<ProviderHealthOptions>(configuration.GetSection(ProviderHealthOptions.SectionName));
        services.AddHostedService<ProviderHealthMonitoringService>();

        // Register SignalR admin notification service
        services.AddScoped<ConduitLLM.Admin.Hubs.AdminNotificationService>();

        // Register cache management service
        services.AddScoped<ICacheManagementService, CacheManagementService>();

        // Configure CORS for the Admin API
        services.AddCors(options =>
        {
            options.AddPolicy("AdminCorsPolicy", policy =>
            {
                policy.WithOrigins(
                        configuration.GetSection("AdminApi:AllowedOrigins").Get<string[]>() ??
                        new[] { "http://localhost:5000", "https://localhost:5001" })
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials(); // Required for SignalR
            });
        });

        return services;
    }
}
