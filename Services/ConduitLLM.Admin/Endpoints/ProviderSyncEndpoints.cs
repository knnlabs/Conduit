using ConduitLLM.Admin.Auditing;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Admin.Models.ProviderSync;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Core.Interfaces;

using Microsoft.AspNetCore.Mvc;

namespace ConduitLLM.Admin.Endpoints;

public static class ProviderSyncEndpoints
{
    private const string SyncLockKey = "openrouter:metadata-sync:leader";

    public static IEndpointRouteBuilder MapProviderSyncEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/ProviderSync")
            .RequireAuthorization("MasterKeyPolicy")
            .AddEndpointFilter<OperationLoggingEndpointFilter>()
            .WithTags("ProviderSync");
        group.MapGet("/drift", ListDrift).WithName("ProviderSync_ListDrift").Produces<List<DriftItemDto>>();
        group.MapGet("/drift/{id:int}", GetDrift).WithName("ProviderSync_GetDrift")
            .Produces<DriftItemDto>().Produces<ErrorResponseDto>(StatusCodes.Status404NotFound);
        group.MapPost("/drift/{id:int}/apply", Apply).WithName("ProviderSync_ApplyDrift")
            .Produces<DriftActionResultDto>();
        group.MapPost("/drift/{id:int}/dismiss", Dismiss).WithName("ProviderSync_DismissDrift")
            .Produces<DriftActionResultDto>();
        group.MapPost("/drift/bulk/apply", ApplyBulk).WithName("ProviderSync_ApplyDriftBulk")
            .Produces<BulkDriftActionResponse>().Produces<ErrorResponseDto>(StatusCodes.Status400BadRequest);
        group.MapPost("/drift/bulk/dismiss", DismissBulk).WithName("ProviderSync_DismissDriftBulk")
            .Produces<BulkDriftActionResponse>().Produces<ErrorResponseDto>(StatusCodes.Status400BadRequest);
        group.MapPost("/run", Run).WithName("ProviderSync_Run")
            .Produces<ProviderSyncRunDto>().Produces<ErrorResponseDto>(StatusCodes.Status409Conflict);
        group.MapGet("/runs", ListRuns).WithName("ProviderSync_ListRuns").Produces<List<ProviderSyncRunDto>>();
        return app;
    }

    private static async Task<IResult> ListDrift(
        [FromServices] IAdminProviderSyncService service,
        [FromQuery] string? status,
        [FromQuery] string? driftType,
        [FromQuery] int? providerId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50) =>
        Results.Ok(await service.GetDriftItemsAsync(status, driftType, providerId,
            Math.Max(1, page), Math.Clamp(pageSize, 1, 200)));

    private static async Task<IResult> GetDrift(int id, [FromServices] IAdminProviderSyncService service)
    {
        var item = await service.GetDriftItemAsync(id);
        return item is null ? AdminResults.NotFoundEntity("Drift item", id) : Results.Ok(item);
    }

    private static async Task<IResult> Apply(
        int id,
        [FromServices] IAdminProviderSyncService service,
        HttpContext context,
        ILoggerFactory loggerFactory)
    {
        var result = await service.ApplyAsync(id, CurrentActor(context));
        AdminAudit.Log(context, Logger(loggerFactory), "AppliedDrift", "ProviderMetadataDriftItem", id,
            result.Success ? "applied" : result.Error);
        return Results.Ok(result);
    }

    private static async Task<IResult> Dismiss(
        int id,
        [FromServices] IAdminProviderSyncService service,
        HttpContext context,
        ILoggerFactory loggerFactory)
    {
        var result = await service.DismissAsync(id, CurrentActor(context));
        AdminAudit.Log(context, Logger(loggerFactory), "DismissedDrift", "ProviderMetadataDriftItem", id);
        return Results.Ok(result);
    }

    private static async Task<IResult> ApplyBulk(
        [FromBody] BulkDriftActionRequest request,
        [FromServices] IAdminProviderSyncService service,
        HttpContext context,
        ILoggerFactory loggerFactory)
    {
        if (request.Ids is null || request.Ids.Count == 0)
        {
            return AdminResults.BadRequest("No drift item ids provided.");
        }
        var result = await service.ApplyBulkAsync(request.Ids, CurrentActor(context));
        AdminAudit.LogBulk(context, Logger(loggerFactory), "BulkAppliedDrift", "ProviderMetadataDriftItem",
            result.SucceededCount, result.FailedCount);
        return Results.Ok(result);
    }

    private static async Task<IResult> DismissBulk(
        [FromBody] BulkDriftActionRequest request,
        [FromServices] IAdminProviderSyncService service,
        HttpContext context,
        ILoggerFactory loggerFactory)
    {
        if (request.Ids is null || request.Ids.Count == 0)
        {
            return AdminResults.BadRequest("No drift item ids provided.");
        }
        var result = await service.DismissBulkAsync(request.Ids, CurrentActor(context));
        AdminAudit.LogBulk(context, Logger(loggerFactory), "BulkDismissedDrift", "ProviderMetadataDriftItem",
            result.SucceededCount, result.FailedCount);
        return Results.Ok(result);
    }

    private static async Task<IResult> Run(
        [FromServices] IOpenRouterDriftDetectionService detectionService,
        [FromServices] IDistributedLockService lockService,
        HttpContext context,
        ILoggerFactory loggerFactory)
    {
        using var lockHandle = await lockService.AcquireLockAsync(SyncLockKey, TimeSpan.FromMinutes(15));
        if (lockHandle is null)
        {
            return AdminResults.Conflict("A sync is already in progress.");
        }
        var run = await detectionService.RunSyncAsync("Manual");
        AdminAudit.Log(context, Logger(loggerFactory), "RanSync", "ProviderMetadataSyncRun", run.Id,
            $"Status: {run.Status}");
        return Results.Ok(run);
    }

    private static async Task<IResult> ListRuns(
        [FromServices] IAdminProviderSyncService service,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25) =>
        Results.Ok(await service.GetSyncRunsAsync(Math.Max(1, page), Math.Clamp(pageSize, 1, 100)));

    private static string CurrentActor(HttpContext context) => context.User.Identity?.Name ?? "admin";
    private static ILogger Logger(ILoggerFactory factory) =>
        factory.CreateLogger("ConduitLLM.Admin.Endpoints.ProviderSync");
}
