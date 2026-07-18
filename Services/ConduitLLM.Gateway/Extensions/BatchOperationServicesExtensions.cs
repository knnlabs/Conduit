using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Services;
using ConduitLLM.Core.Services.BatchOperations;
using ConduitLLM.Gateway.Services;

namespace ConduitLLM.Gateway.Extensions;

/// <summary>
/// Extension methods for registering batch operation services
/// </summary>
public static class BatchOperationServicesExtensions
{
    /// <summary>
    /// Adds batch operation services including TaskHub, history, notification, and batch operations
    /// </summary>
    public static IServiceCollection AddBatchOperationServices(this IServiceCollection services)
    {
        // Register TaskHub Service for ITaskHub interface
        services.AddSingleton<ITaskHub, TaskHubService>();

        // Register Batch Operation Services
        services.AddScoped<IBatchOperationHistoryRepository, BatchOperationHistoryRepository>();
        services.AddScoped<IBatchOperationHistoryService, BatchOperationHistoryService>();
        services.AddSingleton<IBatchOperationNotificationService, BatchOperationNotificationService>();
        services.AddScoped<IBatchOperationService, BatchOperationService>();

        // Register Batch Operation Idempotency Service (Redis-based)
        services.AddSingleton<IBatchOperationIdempotencyService, BatchOperationIdempotencyService>();

        // Register batch operations
        services.AddScoped<IBatchVirtualKeyUpdateOperation, BatchVirtualKeyUpdateOperation>();
        services.AddScoped<IBatchWebhookSendOperation, BatchWebhookSendOperation>();

        // Register spend update batch operation
        services.AddScoped<BatchSpendUpdateOperation>();

        return services;
    }
}
