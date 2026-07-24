using ConduitLLM.Admin.Auditing;
using ConduitLLM.Admin.DTOs;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Admin.Services;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Core.Extensions;
using ConduitLLM.Core.Events;
using ConduitLLM.Configuration.Messaging;

using Microsoft.AspNetCore.Mvc;

namespace ConduitLLM.Admin.Endpoints;

public static class GlobalSettingsEndpoints
{
    public static IEndpointRouteBuilder MapGlobalSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/admin/global-settings")
            .RequireAuthorization("MasterKeyPolicy")
            .AddEndpointFilter<OperationLoggingEndpointFilter>()
            .WithTags("GlobalSettings");
        group.MapGet("/", List).WithName("GlobalSettings_List").Produces<IEnumerable<GlobalSettingDto>>();
        group.MapGet("/definitions", GetDefinitions)
            .WithName("GlobalSettings_GetDefinitions")
            .WithSummary("List typed global setting definitions")
            .Produces<IReadOnlyList<GlobalSettingDefinitionDto>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status429TooManyRequests);
        group.MapGet("/{id:int}", GetById).WithName("GlobalSettings_GetById")
            .Produces<GlobalSettingDto>().Produces<AdminProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json");
        group.MapGet("/by-key/{key}", GetByKey).WithName("GlobalSettings_GetByKey")
            .Produces<GlobalSettingDto>().Produces<AdminProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json");
        group.MapPost("/", Create).WithName("GlobalSettings_Create")
            .Produces<GlobalSettingDto>(StatusCodes.Status201Created)
            .Produces<AdminProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json");
        group.MapPatch("/{id:int}", Update).WithName("GlobalSettings_Update")
            .Produces(StatusCodes.Status204NoContent).Produces<AdminProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")
            .Produces<AdminProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json");
        group.MapPut("/by-key", UpdateByKey).WithName("GlobalSettings_UpdateByKey")
            .Produces(StatusCodes.Status204NoContent).Produces<AdminProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json");
        group.MapDelete("/{id:int}", Delete).WithName("GlobalSettings_Delete")
            .Produces(StatusCodes.Status204NoContent).Produces<AdminProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json");
        group.MapDelete("/by-key/{key}", DeleteByKey).WithName("GlobalSettings_DeleteByKey")
            .Produces(StatusCodes.Status204NoContent).Produces<AdminProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json");
        group.MapGet("/cache/stats", GetCacheStats).WithName("GlobalSettings_GetCacheStats")
            .WithMetadata(new ObsoleteAttribute(
                "Use conduit cache metrics in the Infrastructure Grafana dashboard."))
            .Produces<GlobalSettingCacheStatsDto>();
        group.MapPost("/cache/reload", ReloadCache).WithName("GlobalSettings_ReloadCache")
            .Produces<GlobalSettingsReloadAcceptedResponse>(StatusCodes.Status202Accepted);
        group.MapPost("/cache/invalidate/{key}", InvalidateCache).WithName("GlobalSettings_InvalidateCache")
            .Produces(StatusCodes.Status204NoContent);
        return app;
    }

    private static async Task<IResult> List([FromServices] IAdminGlobalSettingService service) =>
        Results.Ok(await service.GetAllSettingsAsync());

    private static IResult GetDefinitions() =>
        Results.Ok(GlobalSettingDefinitionRegistry.All);

    private static async Task<IResult> GetById(int id, [FromServices] IAdminGlobalSettingService service)
    {
        var setting = await service.GetSettingByIdAsync(id);
        return setting is null ? AdminResults.NotFoundEntity("Global setting", id) : Results.Ok(setting);
    }

    private static async Task<IResult> GetByKey(string key, [FromServices] IAdminGlobalSettingService service)
    {
        var setting = await service.GetSettingByKeyAsync(key);
        return setting is null ? AdminResults.NotFoundEntity("Global setting", key) : Results.Ok(setting);
    }

    private static async Task<IResult> Create(
        [FromBody] CreateGlobalSettingDto setting,
        [FromServices] IAdminGlobalSettingService service,
        HttpContext context,
        ILoggerFactory loggerFactory)
    {
        var created = await service.CreateSettingAsync(setting);
        AdminAudit.Log(context, Logger(loggerFactory), "Created", "GlobalSetting", created.Id,
            $"Key: {LoggingSanitizer.S(setting.Key)}");
        AdminOperationsMetricsService.RecordConfigurationChange("globalsetting", "create");
        return Results.Created($"/v1/admin/global-settings/{created.Id}", created);
    }

    private static async Task<IResult> Update(
        int id,
        [FromBody] UpdateGlobalSettingDto setting,
        [FromServices] IAdminGlobalSettingService service,
        HttpContext context,
        ILoggerFactory loggerFactory)
    {
        var preState = await service.GetSettingByIdAsync(id) ?? throw new KeyNotFoundException();
        if (!await service.UpdateSettingAsync(id, setting))
        {
            throw new KeyNotFoundException();
        }
        var changes = new List<(string Property, string? OldValue, string? NewValue)>();
        if (setting.Value is not null && preState.Value != setting.Value)
            changes.Add(("Value", preState.Value, setting.Value));
        if (setting.Description is not null && preState.Description != setting.Description)
            changes.Add(("Description", preState.Description, setting.Description));
        if (changes.Count > 0)
        {
            AdminAudit.LogWithChanges(context, Logger(loggerFactory), "GlobalSetting", id, changes,
                $"Key: {LoggingSanitizer.S(preState.Key)}");
        }
        else
        {
            AdminAudit.Log(context, Logger(loggerFactory), "Updated", "GlobalSetting", id);
        }
        AdminOperationsMetricsService.RecordConfigurationChange("globalsetting", "update");
        return Results.Ok(await service.GetSettingByIdAsync(id) ?? throw new KeyNotFoundException());
    }

    private static async Task<IResult> UpdateByKey(
        [FromBody] UpdateGlobalSettingByKeyDto setting,
        [FromServices] IAdminGlobalSettingService service,
        HttpContext context,
        ILoggerFactory loggerFactory)
    {
        if (!await service.UpdateSettingByKeyAsync(setting))
            throw new InvalidOperationException("Failed to update or create global setting");
        AdminAudit.Log(context, Logger(loggerFactory), "Updated", "GlobalSetting",
            detail: $"Key: {LoggingSanitizer.S(setting.Key)}");
        AdminOperationsMetricsService.RecordConfigurationChange("globalsetting", "update");
        return Results.NoContent();
    }

    private static async Task<IResult> Delete(
        int id,
        [FromServices] IAdminGlobalSettingService service,
        HttpContext context,
        ILoggerFactory loggerFactory)
    {
        if (!await service.DeleteSettingAsync(id)) throw new KeyNotFoundException();
        AdminAudit.Log(context, Logger(loggerFactory), "Deleted", "GlobalSetting", id);
        AdminOperationsMetricsService.RecordConfigurationChange("globalsetting", "delete");
        return Results.NoContent();
    }

    private static async Task<IResult> DeleteByKey(
        string key,
        [FromServices] IAdminGlobalSettingService service,
        HttpContext context,
        ILoggerFactory loggerFactory)
    {
        if (!await service.DeleteSettingByKeyAsync(key)) throw new KeyNotFoundException();
        AdminAudit.Log(context, Logger(loggerFactory), "Deleted", "GlobalSetting",
            detail: $"Key: {LoggingSanitizer.S(key)}");
        AdminOperationsMetricsService.RecordConfigurationChange("globalsetting", "delete");
        return Results.NoContent();
    }

    private static async Task<IResult> GetCacheStats([FromServices] IGlobalSettingsCacheService cacheService)
    {
        var stats = await cacheService.GetCacheStatsAsync();
        return Results.Ok(new GlobalSettingCacheStatsDto
        {
            CacheSize = (int)stats["CacheSize"],
            CacheHits = (long)stats["CacheHits"],
            CacheMisses = (long)stats["CacheMisses"],
            Invalidations = (long)stats["Invalidations"],
            HitRate = (double)stats["HitRate"],
            LastLoadTime = (DateTime)stats["LastLoadTime"],
            CachedKeys = (List<string>)stats["CachedKeys"]
        });
    }

    private static async Task<IResult> ReloadCache(
        [FromServices] IEventBus eventBus,
        HttpContext context,
        ILoggerFactory loggerFactory)
    {
        var requestId = Guid.NewGuid().ToString();
        await eventBus.PublishAsync(new GlobalSettingsReloadRequested
        {
            RequestedBy = "Admin User",
            CorrelationId = requestId
        });
        AdminAudit.Log(context, Logger(loggerFactory), "Reloaded", "GlobalSettingsCache");
        return Results.Accepted(value: new GlobalSettingsReloadAcceptedResponse(
            "Global settings cache reload was accepted for all Admin and Gateway instances.",
            requestId,
            DateTime.UtcNow));
    }

    private static async Task<IResult> InvalidateCache(
        string key,
        [FromServices] IGlobalSettingsCacheService cacheService,
        HttpContext context,
        ILoggerFactory loggerFactory)
    {
        await cacheService.InvalidateSettingAsync(key);
        AdminAudit.Log(context, Logger(loggerFactory), "Invalidated", "GlobalSettingsCache",
            detail: $"Key: {LoggingSanitizer.S(key)}");
        return Results.NoContent();
    }

    private static ILogger Logger(ILoggerFactory factory) =>
        factory.CreateLogger("ConduitLLM.Admin.Endpoints.GlobalSettings");
}
