using ConduitLLM.Admin.Auditing;
using ConduitLLM.Admin.DTOs;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Core.Interfaces;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
            .Produces<MediaCleanupResponseDto>()
            .Produces<AdminProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json");
        media.MapPost("/cleanup/orphaned", CleanupOrphanedMedia).WithName("Media_CleanupOrphaned")
            .Produces<MediaCleanupResponseDto>()
            .Produces<AdminProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json");
        media.MapPost("/cleanup/prune/preview", PreviewPruneMedia).WithName("Media_PreviewPrune")
            .WithSummary("Preview the files and bytes matched by a prune operation")
            .Produces<MediaCleanupPreviewDto>()
            .Produces<AdminProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")
            .Produces<AdminProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")
            .Produces<AdminProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")
            .Produces<AdminProblemDetails>(StatusCodes.Status429TooManyRequests, "application/problem+json");
        media.MapPost("/cleanup/prune", PruneOldMedia).WithName("Media_Prune")
            .Produces<MediaCleanupResponseDto>()
            .Produces<AdminProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")
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
        [FromServices] IConfigurationDbContext configurationContext,
        [FromServices] IMediaDeletionEngine deletionEngine,
        [FromServices] IDistributedLockService lockService,
        [FromServices] IMediaCleanupStatusService statusService,
        [FromServices] ILogger<MediaEndpointLog> logger,
        [FromQuery] bool force = false,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteManualCleanupAsync(
            MediaCleanupTypes.Expiration,
            "expired media",
            force,
            context,
            deletionEngine,
            lockService,
            statusService,
            logger,
            () => configurationContext.MediaRecords
                .AsNoTracking()
                .Where(media => media.ExpiresAt != null && media.ExpiresAt <= DateTime.UtcNow)
                .ToListAsync(cancellationToken),
            cancellationToken);
    }

    private static async Task<IResult> CleanupOrphanedMedia(
        HttpContext context,
        [FromServices] IMediaRecordRepository mediaRepository,
        [FromServices] IMediaDeletionEngine deletionEngine,
        [FromServices] IDistributedLockService lockService,
        [FromServices] IMediaCleanupStatusService statusService,
        [FromServices] ILogger<MediaEndpointLog> logger,
        [FromQuery] bool force = false,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteManualCleanupAsync(
            MediaCleanupTypes.Orphan,
            "orphaned media",
            force,
            context,
            deletionEngine,
            lockService,
            statusService,
            logger,
            () => mediaRepository.GetOrphanedMediaAsync(cancellationToken),
            cancellationToken);
    }

    private static async Task<IResult> PreviewPruneMedia(
        PruneMediaRequest request,
        [FromServices] IConfigurationDbContext configurationContext,
        [FromServices] IMediaDeletionEngine deletionEngine,
        CancellationToken cancellationToken = default)
    {
        if (request.DaysToKeep is null or <= 0)
            return AdminResults.BadRequest("DaysToKeep must be a positive number");

        var candidates = await QueryPruneCandidatesAsync(
            configurationContext,
            request.DaysToKeep.Value,
            cancellationToken);
        var preview = deletionEngine.Preview(candidates);
        return Results.Ok(new MediaCleanupPreviewDto
        {
            FileCount = preview.FileCount,
            SizeBytes = preview.SizeBytes,
            ConfirmationPhrase = $"DELETE {preview.FileCount}"
        });
    }

    private static async Task<IResult> PruneOldMedia(
        PruneMediaRequest request,
        HttpContext context,
        [FromServices] IConfigurationDbContext configurationContext,
        [FromServices] IMediaDeletionEngine deletionEngine,
        [FromServices] IDistributedLockService lockService,
        [FromServices] IMediaCleanupStatusService statusService,
        [FromServices] ILogger<MediaEndpointLog> logger,
        CancellationToken cancellationToken = default)
    {
        if (request.DaysToKeep is null or <= 0)
            return AdminResults.BadRequest("DaysToKeep must be a positive number");

        return await ExecuteManualCleanupAsync(
            MediaCleanupTypes.Retention,
            $"media files older than {request.DaysToKeep} days",
            request.Force,
            context,
            deletionEngine,
            lockService,
            statusService,
            logger,
            () => QueryPruneCandidatesAsync(
                configurationContext,
                request.DaysToKeep.Value,
                cancellationToken),
            cancellationToken);
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

    private static async Task<IResult> ExecuteManualCleanupAsync(
        string cleanupType,
        string description,
        bool force,
        HttpContext context,
        IMediaDeletionEngine deletionEngine,
        IDistributedLockService lockService,
        IMediaCleanupStatusService statusService,
        ILogger<MediaEndpointLog> logger,
        Func<Task<List<MediaRecord>>> getCandidates,
        CancellationToken cancellationToken)
    {
        await using var lockHandle = await lockService.AcquireLockAsync(
            MediaCleanupLock.Key,
            MediaCleanupLock.Duration,
            cancellationToken);
        if (lockHandle == null)
        {
            return AdminResults.Conflict(
                "Media cleanup is already running; retry after the current run completes.",
                "media_cleanup_in_progress");
        }

        var instanceId = $"manual:{context.TraceIdentifier}";
        var operation = new MediaDeletionOperationContext(
            cleanupType,
            "manual",
            instanceId,
            force);
        var result = await deletionEngine.ExecuteOperationAsync(
            operation,
            async () =>
            {
                var candidates = await getCandidates();
                return await deletionEngine.DeleteAsync(
                    new MediaDeletionRequest(candidates, operation),
                    cancellationToken);
            },
            cancellationToken);

        await statusService.RecordRunCompletionAsync(
            result.FilesDeleted,
            result.BytesFreed,
            result.DurationSeconds,
            result.OperationStatus ?? "Completed",
            instanceId,
            "manual",
            cancellationToken);
        AdminAudit.Log(
            context,
            logger,
            force ? "ForceCleanup" : "Cleanup",
            "Media",
            detail:
                $"Type: {cleanupType}, Force: {force}, DeletedCount: {result.FilesDeleted}, " +
                $"FailedCount: {result.Failures}, WouldDeleteCount: {result.WouldDeleteCount}");

        return Results.Ok(ToCleanupResponse(description, result));
    }

    private static Task<List<MediaRecord>> QueryPruneCandidatesAsync(
        IConfigurationDbContext context,
        int daysToKeep,
        CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow.AddDays(-daysToKeep);
        var recentAccessCutoff = DateTime.UtcNow.AddDays(-30);
        return context.MediaRecords
            .AsNoTracking()
            .Where(media => media.CreatedAt < cutoff)
            .Where(media =>
                media.LastAccessedAt == null ||
                media.LastAccessedAt < recentAccessCutoff)
            .ToListAsync(cancellationToken);
    }

    private static MediaCleanupResponseDto ToCleanupResponse(
        string description,
        MediaDeletionEngineResult result) => new()
    {
        Message = result.IsDryRun
            ? $"Dry run matched {result.WouldDeleteCount} {description}"
            : result.Failures == 0
                ? $"Deleted {result.FilesDeleted} {description}"
                : $"Deleted {result.FilesDeleted} {description}; {result.Failures} failed and remain tracked for retry",
        DeletedCount = result.FilesDeleted,
        FailedCount = result.Failures,
        IsDryRun = result.IsDryRun,
        WouldDeleteCount = result.WouldDeleteCount,
        BytesWouldFree = result.BytesWouldFree,
        TriggeredBy = "manual"
    };

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
