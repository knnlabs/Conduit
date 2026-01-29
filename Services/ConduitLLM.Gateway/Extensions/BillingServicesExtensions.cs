using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Services;
using ConduitLLM.Core.Extensions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Services;
using ConduitLLM.Gateway.Services;

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
        // Model costs tracking service
        services.AddScoped<IModelCostService, ModelCostService>();

        // Cost calculation service
        services.AddScoped<ICostCalculationService, CostCalculationService>();

        // Tool cost calculation service for provider tool billing
        services.AddScoped<IToolCostCalculationService, ToolCostCalculationService>();

        // Ephemeral key service for direct browser-to-API authentication (used for all direct access including SignalR)
        services.AddScoped<IEphemeralKeyService, EphemeralKeyService>();

        // Virtual key service (Configuration layer - used by RealtimeUsageTracker)
        services.AddScoped<ConduitLLM.Configuration.Interfaces.IVirtualKeyService, ConduitLLM.Configuration.Services.VirtualKeyService>();

        // Billing audit service for comprehensive billing event tracking - with leader election
        services.AddSingleton<IBillingAuditService, BillingAuditService>();
        services.AddLeaderElectedHostedService<BillingAuditService>(
            provider => (BillingAuditService)provider.GetRequiredService<IBillingAuditService>(),
            "BillingAuditService");

        // Pricing rules engine services for flexible rules-based pricing
        services.AddScoped<IPricingRulesEvaluator, PricingRulesEvaluator>();
        services.AddScoped<IPricingRulesValidator, PricingRulesValidator>();

        // Cached pricing rules service for parsed configuration caching
        services.AddSingleton<ICachedPricingRulesService, CachedPricingRulesService>();

        // Pricing audit service for rules-based pricing evaluation tracking - with leader election
        services.AddSingleton<IPricingAuditService, PricingAuditService>();
        services.AddLeaderElectedHostedService<PricingAuditService>(
            provider => (PricingAuditService)provider.GetRequiredService<IPricingAuditService>(),
            "PricingAuditService");

        return services;
    }
}
