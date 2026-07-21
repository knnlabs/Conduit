using ConduitLLM.Admin.Auditing;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.DTOs.Monitoring;
using ConduitLLM.Configuration.Messaging;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ConduitLLM.Admin.Endpoints;

public static class SystemInfoEndpoints
{
    public static IEndpointRouteBuilder MapSystemInfoEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/SystemInfo")
            .RequireAuthorization("MasterKeyPolicy")
            .AddEndpointFilter<OperationLoggingEndpointFilter>()
            .WithTags("SystemInfo");
        group.MapGet("/info", GetInfo).WithName("SystemInfo_GetInfo")
            .Produces<SystemInfoDto>(StatusCodes.Status200OK);
        group.MapGet("/health", GetHealth).WithName("SystemInfo_GetHealth")
            .Produces<HealthStatusDto>(StatusCodes.Status200OK);
        group.MapPost("/cache/invalidate-discovery", InvalidateDiscovery)
            .WithName("SystemInfo_InvalidateDiscoveryCache").Produces<object>(StatusCodes.Status200OK);
        group.MapGet("/cache/function-discovery/stats", GetFunctionDiscoveryStats)
            .WithName("SystemInfo_GetFunctionDiscoveryCacheStats")
            .Produces<object>(StatusCodes.Status200OK).Produces<object>(StatusCodes.Status404NotFound);
        group.MapPost("/cache/invalidate-function-discovery", InvalidateFunctionDiscovery)
            .WithName("SystemInfo_InvalidateFunctionDiscoveryCache").Produces<object>(StatusCodes.Status200OK);
        return app;
    }

    private static async Task<IResult> GetInfo([FromServices] IAdminSystemInfoService service) =>
        Results.Ok(await service.GetSystemInfoAsync());
    private static async Task<IResult> GetHealth([FromServices] IAdminSystemInfoService service) =>
        Results.Ok(await service.GetHealthStatusAsync());

    private static async Task<IResult> InvalidateDiscovery(
        [FromServices] IEventBus eventBus, HttpContext context, ILoggerFactory loggerFactory)
    {
        await eventBus.PublishAsync(new DiscoveryCacheInvalidationRequested
        {
            Reason = "Manual invalidation via Admin API",
            RequestedBy = "Admin User",
            CorrelationId = Guid.NewGuid().ToString()
        });
        Audit(context, loggerFactory, "DiscoveryCache");
        return Results.Ok(new
        {
            message = "Discovery cache invalidation request published successfully",
            timestamp = DateTime.UtcNow,
            note = "Cache invalidation is being processed asynchronously across all Gateway API instances"
        });
    }

    private static async Task<IResult> GetFunctionDiscoveryStats(IServiceProvider services)
    {
        var cache = services.GetService<IFunctionDiscoveryCacheService>();
        return cache is null
            ? Results.NotFound(new
            {
                message = "Function discovery cache service is not configured",
                note = "The cache service must be registered in the DI container"
            })
            : Results.Ok(await cache.GetStatisticsAsync());
    }

    private static async Task<IResult> InvalidateFunctionDiscovery(
        [FromServices] IEventBus eventBus, HttpContext context, ILoggerFactory loggerFactory)
    {
        await eventBus.PublishAsync(new FunctionDiscoveryCacheInvalidationRequested
        {
            Reason = "Manual invalidation via Admin API",
            RequestedBy = "Admin User",
            CorrelationId = Guid.NewGuid().ToString()
        });
        Audit(context, loggerFactory, "FunctionDiscoveryCache");
        return Results.Ok(new
        {
            message = "Function discovery cache invalidation request published successfully",
            timestamp = DateTime.UtcNow,
            note = "Cache invalidation is being processed asynchronously across all Gateway API instances"
        });
    }

    private static void Audit(HttpContext context, ILoggerFactory factory, string entity) =>
        AdminAudit.Log(context, factory.CreateLogger("ConduitLLM.Admin.Endpoints.SystemInfo"),
            "Invalidated", entity);
}
