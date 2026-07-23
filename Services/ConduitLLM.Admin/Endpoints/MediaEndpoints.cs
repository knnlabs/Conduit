using ConduitLLM.Admin.Auditing;
using ConduitLLM.Admin.DTOs;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Interfaces;

using Microsoft.AspNetCore.Mvc;

namespace ConduitLLM.Admin.Endpoints;

/// <summary>Administrative endpoints for media lifecycle management.</summary>
public static class MediaEndpoints
{
    public static IEndpointRouteBuilder MapMediaEndpoints(this IEndpointRouteBuilder app)
    {
        var media = app.MapGroup("/v1/admin/media-assets")
            .RequireAuthorization("MasterKeyPolicy")
            .AddEndpointFilter<ValidationEndpointFilter>()
            .AddEndpointFilter<OperationLoggingEndpointFilter>()
            .WithTags("Media");

        media.MapGet("/stats", GetOverallStats).WithName("Media_GetOverallStats")
            .Produces<OverallMediaStorageStats>();
        media.MapGet("/stats/virtual-key/{virtualKeyId}", GetStatsByVirtualKey).WithName("Media_GetStatsByVirtualKey")
            .Produces<MediaStorageStats>();
        media.MapGet("/stats/by-provider", GetStatsByProvider).WithName("Media_GetStatsByProvider")
            .Produces<Dictionary<string, long>>();
        media.MapGet("/stats/by-type", GetStatsByMediaType).WithName("Media_GetStatsByMediaType")
            .Produces<Dictionary<string, long>>();
        media.MapGet("/virtual-key/{virtualKeyId}", GetMediaByVirtualKey).WithName("Media_GetByVirtualKey")
            .Produces<List<MediaRecordResponse>>();
        media.MapGet("/search", SearchMedia).WithName("Media_Search")
            .Produces<List<MediaRecordResponse>>()
            .Produces<AdminProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json");
        media.MapDelete("/{mediaId}", DeleteMedia).WithName("Media_Delete")
            .Produces<MediaDeletionResponseDto>()
            .Produces(StatusCodes.Status404NotFound);
        media.MapPost("/cleanup/expired", CleanupExpiredMedia).WithName("Media_CleanupExpired")
            .Produces<MediaCleanupResponseDto>();
        media.MapPost("/cleanup/orphaned", CleanupOrphanedMedia).WithName("Media_CleanupOrphaned")
            .Produces<MediaCleanupResponseDto>();
        media.MapPost("/cleanup/prune", PruneOldMedia).WithName("Media_Prune")
            .Produces<MediaCleanupResponseDto>()
            .Produces<AdminProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json");

        var cleanup = app.MapGroup("/v1/admin/media-cleanup-jobs")
            .RequireAuthorization("MasterKeyPolicy")
            .AddEndpointFilter<ValidationEndpointFilter>()
            .AddEndpointFilter<OperationLoggingEndpointFilter>()
            .WithTags("Media Cleanup");

        cleanup.MapGet("/status", GetCleanupStatus).WithName("MediaCleanup_GetStatus")
            .Produces<MediaCleanupStatusDto>();
        cleanup.MapGet("/enabled", GetCleanupEnabled).WithName("MediaCleanup_GetEnabled")
            .Produces<MediaCleanupEnabledDto>();
        cleanup.MapPost("/enabled", SetCleanupEnabled).WithName("MediaCleanup_SetEnabled")
            .Produces<MediaCleanupEnabledChangedDto>();
        cleanup.MapGet("/simple-retention", GetSimpleRetention).WithName("MediaCleanup_GetSimpleRetention")
            .Produces<SimpleRetentionResponse>();
        cleanup.MapPost("/simple-retention", SetSimpleRetention).WithName("MediaCleanup_SetSimpleRetention")
            .Produces<SimpleRetentionResponse>();
        return app;
    }

    private static async Task<IResult> GetOverallStats(
        [FromServices] IAdminMediaService mediaService,
        [FromQuery] int? virtualKeyGroupId = null) =>
        Results.Ok(await mediaService.GetOverallStorageStatsAsync(virtualKeyGroupId));

    private static async Task<IResult> GetStatsByVirtualKey(
        int virtualKeyId,
        [FromServices] IAdminMediaService mediaService) =>
        Results.Ok(await mediaService.GetStorageStatsByVirtualKeyAsync(virtualKeyId));

    private static async Task<IResult> GetStatsByProvider([FromServices] IAdminMediaService mediaService) =>
        Results.Ok(await mediaService.GetStorageStatsByProviderAsync());

    private static async Task<IResult> GetStatsByMediaType([FromServices] IAdminMediaService mediaService) =>
        Results.Ok(await mediaService.GetStorageStatsByMediaTypeAsync());

    private static async Task<IResult> GetMediaByVirtualKey(
        int virtualKeyId,
        [FromServices] IAdminMediaService mediaService) =>
        Results.Ok((await mediaService.GetMediaByVirtualKeyAsync(virtualKeyId)).Select(ToResponse).ToList());

    private static async Task<IResult> SearchMedia(
        [FromServices] IAdminMediaService mediaService,
        [FromQuery] string? pattern = null)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            return AdminResults.BadRequest("Search pattern is required");
        return Results.Ok((await mediaService.SearchMediaByStorageKeyAsync(pattern)).Select(ToResponse).ToList());
    }

    private static async Task<IResult> DeleteMedia(
        Guid mediaId,
        HttpContext context,
        [FromServices] IAdminMediaService mediaService,
        [FromServices] ILogger<MediaEndpointLog> logger)
    {
        if (!await mediaService.DeleteMediaAsync(mediaId))
            throw new KeyNotFoundException();
        AdminAudit.Log(context, logger, "Deleted", "Media", mediaId);
        return Results.Ok(new MediaDeletionResponseDto { Message = "Media deleted successfully" });
    }

    private static async Task<IResult> CleanupExpiredMedia(
        HttpContext context,
        [FromServices] IAdminMediaService mediaService,
        [FromServices] ILogger<MediaEndpointLog> logger)
    {
        var count = await mediaService.CleanupExpiredMediaAsync();
        AdminAudit.Log(context, logger, "CleanedUpExpired", "Media", detail: $"DeletedCount: {count}");
        return Results.Ok(new MediaCleanupResponseDto { Message = $"Cleaned up {count} expired media files", DeletedCount = count });
    }

    private static async Task<IResult> CleanupOrphanedMedia(
        HttpContext context,
        [FromServices] IAdminMediaService mediaService,
        [FromServices] ILogger<MediaEndpointLog> logger)
    {
        var count = await mediaService.CleanupOrphanedMediaAsync();
        AdminAudit.Log(context, logger, "CleanedUpOrphaned", "Media", detail: $"DeletedCount: {count}");
        return Results.Ok(new MediaCleanupResponseDto { Message = $"Cleaned up {count} orphaned media files", DeletedCount = count });
    }

    private static async Task<IResult> PruneOldMedia(
        PruneMediaRequest request,
        HttpContext context,
        [FromServices] IAdminMediaService mediaService,
        [FromServices] ILogger<MediaEndpointLog> logger)
    {
        if (request.DaysToKeep is null or <= 0)
            return AdminResults.BadRequest("DaysToKeep must be a positive number");
        var count = await mediaService.PruneOldMediaAsync(request.DaysToKeep.Value);
        AdminAudit.Log(context, logger, "Pruned", "Media", detail: $"DaysToKeep: {request.DaysToKeep}, DeletedCount: {count}");
        return Results.Ok(new MediaCleanupResponseDto
        {
            Message = $"Pruned {count} media files older than {request.DaysToKeep} days",
            DeletedCount = count
        });
    }

    private static async Task<IResult> GetCleanupStatus([FromServices] IMediaCleanupStatusService service) =>
        Results.Ok(await service.GetStatusAsync());

    private static async Task<IResult> GetCleanupEnabled([FromServices] IMediaCleanupStatusService service) =>
        Results.Ok(new MediaCleanupEnabledDto { Enabled = await service.IsEnabledAsync() });

    private static async Task<IResult> SetCleanupEnabled(
        UpdateMediaCleanupEnabledRequest request,
        HttpContext context,
        [FromServices] IMediaCleanupStatusService service,
        [FromServices] ILogger<MediaEndpointLog> logger)
    {
        await service.SetEnabledAsync(request.Enabled);
        AdminAudit.Log(context, logger, "SetEnabled", "MediaCleanupService", detail: $"Enabled: {request.Enabled}");
        return Results.Ok(new MediaCleanupEnabledChangedDto
        {
            Enabled = request.Enabled,
            Message = request.Enabled ? "Media cleanup service has been enabled" : "Media cleanup service has been disabled"
        });
    }

    private static async Task<IResult> GetSimpleRetention([FromServices] IMediaCleanupStatusService service)
    {
        var days = await service.GetSimpleRetentionOverrideAsync();
        return Results.Ok(new SimpleRetentionResponse { RetentionDays = days, IsOverrideActive = days.HasValue });
    }

    private static async Task<IResult> SetSimpleRetention(
        UpdateSimpleRetentionRequest request,
        HttpContext context,
        [FromServices] IMediaCleanupStatusService service,
        [FromServices] ILogger<MediaEndpointLog> logger)
    {
        await service.SetSimpleRetentionOverrideAsync(request.RetentionDays);
        var message = request.RetentionDays.HasValue
            ? $"Simple retention override set to {request.RetentionDays} days - all media will be deleted after this period"
            : "Simple retention override cleared - using policy-based retention";
        AdminAudit.Log(context, logger, "SetSimpleRetention", "MediaCleanupService",
            detail: $"RetentionDays: {request.RetentionDays?.ToString() ?? "cleared"}");
        return Results.Ok(new SimpleRetentionResponse
        {
            RetentionDays = request.RetentionDays,
            IsOverrideActive = request.RetentionDays.HasValue,
            Message = message
        });
    }

    private sealed class MediaEndpointLog;

    private static MediaRecordResponse ToResponse(MediaRecord media) => new(
        media.Id,
        media.StorageKey,
        media.VirtualKeyId,
        media.MediaType,
        media.ContentType,
        media.SizeBytes,
        media.ContentHash,
        media.Provider,
        media.Model,
        media.Prompt,
        media.StorageUrl,
        media.PublicUrl,
        media.ExpiresAt,
        media.CreatedAt,
        media.LastAccessedAt,
        media.AccessCount);
}
