using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Services;
using ConduitLLM.Functions.Interfaces;
using ConduitLLM.Functions.Services;

namespace ConduitLLM.Gateway.Extensions;

/// <summary>
/// Extension methods for registering function services
/// </summary>
public static class FunctionServicesExtensions
{
    /// <summary>
    /// Adds function services including repositories, execution, cost calculation, and agentic orchestration
    /// </summary>
    public static IServiceCollection AddFunctionServices(this IServiceCollection services)
    {
        // Register Function repositories
        services.AddScoped<IFunctionConfigurationRepository, FunctionConfigurationRepository>();

        // Register Function services
        services.AddScoped<IFunctionCostService, FunctionCostService>();
        services.AddScoped<IFunctionCostCalculationService, FunctionCostCalculationService>();
        services.AddScoped<IFunctionClientFactory, FunctionClientFactory>();
        services.AddScoped<IFunctionExecutionService, FunctionExecutionService>();
        services.AddScoped<FunctionParameterValidationService>();

        // Register Agentic Function Calling services
        services.AddScoped<IFunctionDiscoveryService, FunctionDiscoveryService>();
        services.AddScoped<IAgenticOrchestrationService, AgenticOrchestrationService>();

        return services;
    }
}
