using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Services;
using ConduitLLM.Core.Extensions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Services;
using ConduitLLM.Gateway.Services;
using Microsoft.EntityFrameworkCore;

namespace ConduitLLM.Gateway.Extensions;

/// <summary>
/// Extension methods for registering billing and pricing services
/// </summary>
public static class BillingServicesExtensions
{
    /// <summary>
    /// Adds billing and pricing services including cost calculation, billing audit, and pricing rules engine
    /// </summary>
    public static IServiceCollection AddBillingAndPricingServices(this IServiceCollection services)
    {
        // Model costs tracking service with caching decorator pattern
        services.AddScoped<ModelCostService>();
        services.AddScoped<IModelCostService>(provider =>
        {
            var innerService = provider.GetRequiredService<ModelCostService>();
            var cacheManager = provider.GetRequiredService<ICacheManager>();
            var logger = provider.GetRequiredService<ILogger<CachedModelCostService>>();
            return new CachedModelCostService(innerService, cacheManager, logger);
        });

        // Cost calculation service
        services.AddScoped<ICostCalculationService, CostCalculationService>();

        // Tool cost calculation service for provider tool billing
        // Singleton: uses IDbContextFactory for database access and optional IProviderToolCache
        services.AddSingleton<IToolCostCalculationService>(sp =>
        {
            var contextFactory = sp.GetRequiredService<IDbContextFactory<ConduitDbContext>>();
            var logger = sp.GetRequiredService<ILogger<ToolCostCalculationService>>();
            var cache = sp.GetService<IProviderToolCache>(); // Optional
            return new ToolCostCalculationService(contextFactory, logger, cache);
        });

        // Ephemeral key service for direct browser-to-API authentication (used for all direct access including SignalR)
        services.AddScoped<IEphemeralKeyService, EphemeralKeyService>();

        // Billing audit service for comprehensive billing event tracking - with leader election
        services.AddSingleton<IBillingAuditService, BillingAuditService>();
        services.AddLeaderElectedHostedService<BillingAuditService>(
            provider => (BillingAuditService)provider.GetRequiredService<IBillingAuditService>(),
            "BillingAuditService");

        services.AddOptions<ConduitLLM.Configuration.Options.BillingReconciliationOptions>()
            .BindConfiguration(ConduitLLM.Configuration.Options.BillingReconciliationOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<BillingReconciliationService>();
        services.AddLeaderElectedHostedService<BillingReconciliationService>(
            provider => provider.GetRequiredService<BillingReconciliationService>(),
            "BillingReconciliationService");

        // Pricing rules engine services for flexible rules-based pricing
        services.AddScoped<IPricingRulesEvaluator, PricingRulesEvaluator>();
        services.AddScoped<IPricingRulesValidator, PricingRulesValidator>();

        // Cached pricing rules service for parsed configuration caching (uses ICacheManager)
        services.AddSingleton<ICachedPricingRulesService, CachedPricingRulesService>();

        // Pricing audit service for rules-based pricing evaluation tracking - with leader election
        services.AddSingleton<IPricingAuditService, PricingAuditService>();
        services.AddLeaderElectedHostedService<PricingAuditService>(
            provider => (PricingAuditService)provider.GetRequiredService<IPricingAuditService>(),
            "PricingAuditService");

        return services;
    }
}
