using ConduitLLM.Core.Extensions;
using ConduitLLM.Admin.Extensions;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Admin.Services;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.DTOs.VirtualKey;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConduitLLM.Admin.Endpoints;

/// <summary>
/// Controller for managing virtual keys
/// </summary>
public class VirtualKeysEndpoints : AdminEndpointHandlerBase
{
    private readonly IAdminVirtualKeyService _virtualKeyService;

    /// <summary>
    /// Initializes the Virtual Keys endpoint handler.
    /// </summary>
    /// <param name="virtualKeyService">The virtual key service</param>
    /// <param name="httpContextAccessor">Accessor for the current request context</param>
    /// <param name="logger">The logger</param>
    public VirtualKeysEndpoints(
        IAdminVirtualKeyService virtualKeyService,
        IHttpContextAccessor httpContextAccessor,
        ILogger<VirtualKeysEndpoints> logger)
        : base(null, httpContextAccessor, logger)
    {
        _virtualKeyService = virtualKeyService ?? throw new ArgumentNullException(nameof(virtualKeyService));
    }

    public static IEndpointRouteBuilder MapVirtualKeysEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/VirtualKeys")
            .AddEndpointFilter<ValidationEndpointFilter>()
            .AddEndpointFilter<OperationLoggingEndpointFilter>()
            .WithTags("Virtual Keys");

        group.MapPost("/", ([FromServices] VirtualKeysEndpoints endpoints, CreateVirtualKeyRequestDto request) => endpoints.GenerateKey(request))
            .WithName("VirtualKeys_Generate").Produces<CreateVirtualKeyResponseDto>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status401Unauthorized).Produces(StatusCodes.Status403Forbidden)
            .RequireAuthorization("MasterKeyPolicy");
        group.MapGet("/", ([FromServices] VirtualKeysEndpoints endpoints, int? virtualKeyGroupId = null) => endpoints.ListKeys(virtualKeyGroupId))
            .WithName("VirtualKeys_GetAll").Produces<List<VirtualKeyDto>>().RequireAuthorization("MasterKeyPolicy");
        group.MapGet("/{id}", ([FromServices] VirtualKeysEndpoints endpoints, int id) => endpoints.GetKeyById(id))
            .WithName("VirtualKeys_GetById").Produces<VirtualKeyDto>().Produces(StatusCodes.Status404NotFound).RequireAuthorization("MasterKeyPolicy");
        group.MapPut("/{id}", ([FromServices] VirtualKeysEndpoints endpoints, int id, UpdateVirtualKeyRequestDto request) => endpoints.UpdateKey(id, request))
            .WithName("VirtualKeys_Update").Produces(StatusCodes.Status204NoContent).Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized).Produces(StatusCodes.Status403Forbidden).Produces(StatusCodes.Status404NotFound)
            .RequireAuthorization("MasterKeyPolicy");
        group.MapDelete("/{id}", ([FromServices] VirtualKeysEndpoints endpoints, int id) => endpoints.DeleteKey(id))
            .WithName("VirtualKeys_Delete").Produces(StatusCodes.Status204NoContent).Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden).Produces(StatusCodes.Status404NotFound).RequireAuthorization("MasterKeyPolicy");
        group.MapPost("/validate", ([FromServices] VirtualKeysEndpoints endpoints, ValidateVirtualKeyRequest request) => endpoints.ValidateKey(request))
            .WithName("VirtualKeys_Validate").Produces<VirtualKeyValidationResult>().Produces(StatusCodes.Status400BadRequest).AllowAnonymous();
        group.MapGet("/{id}/validation-info", ([FromServices] VirtualKeysEndpoints endpoints, int id) => endpoints.GetValidationInfo(id))
            .WithName("VirtualKeys_GetValidationInfo").Produces<VirtualKeyValidationInfoDto>().Produces(StatusCodes.Status404NotFound).RequireAuthorization("MasterKeyPolicy");
        group.MapPost("/maintenance", ([FromServices] VirtualKeysEndpoints endpoints) => endpoints.PerformMaintenance())
            .WithName("VirtualKeys_Maintenance").Produces(StatusCodes.Status204NoContent).Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden).RequireAuthorization("MasterKeyPolicy");
        group.MapGet("/{id}/discovery-preview", ([FromServices] VirtualKeysEndpoints endpoints, int id, string? capability = null) => endpoints.PreviewDiscovery(id, capability))
            .WithName("VirtualKeys_PreviewDiscovery").Produces<VirtualKeyDiscoveryPreviewDto>().Produces(StatusCodes.Status404NotFound).RequireAuthorization("MasterKeyPolicy");
        group.MapGet("/{id}/group", ([FromServices] VirtualKeysEndpoints endpoints, int id) => endpoints.GetKeyGroup(id))
            .WithName("VirtualKeys_GetGroup").Produces<VirtualKeyGroupDto>().Produces(StatusCodes.Status404NotFound).RequireAuthorization("MasterKeyPolicy");
        group.MapGet("/usage/by-key/{key}", ([FromServices] VirtualKeysEndpoints endpoints, string key) => endpoints.GetUsageByKey(key))
            .WithName("VirtualKeys_GetUsageByKey").Produces<VirtualKeyUsageDto>().Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status401Unauthorized).Produces(StatusCodes.Status403Forbidden)
            .RequireAuthorization("MasterKeyPolicy");
        return app;
    }

    /// <summary>
    /// Generates a new virtual API key
    /// </summary>
    /// <param name="request">Details for the key to be created</param>
    /// <returns>The generated key details or an error response</returns>
    public async Task<IResult> GenerateKey(CreateVirtualKeyRequestDto request)
    {
        var response = await _virtualKeyService.GenerateVirtualKeyAsync(request);
        LogAdminAudit("Created", "VirtualKey", response.KeyInfo.Id, $"Name: {LoggingSanitizer.S(request.KeyName)}");
        AdminOperationsMetricsService.RecordVirtualKeyOperation("create", "success");
        AdminOperationsMetricsService.RecordConfigurationChange("virtualkey", "create");
        return Results.Created($"/api/VirtualKeys/{response.KeyInfo.Id}", response);
    }

    /// <summary>
    /// Retrieves a list of all virtual keys
    /// </summary>
    /// <param name="virtualKeyGroupId">Optional filter by virtual key group ID</param>
    /// <returns>List of all virtual keys</returns>
    public async Task<IResult> ListKeys(int? virtualKeyGroupId = null)
    {
        var result = await _virtualKeyService.ListVirtualKeysAsync(virtualKeyGroupId);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves details for a specific virtual key by ID
    /// </summary>
    /// <param name="id">The ID of the key to retrieve</param>
    /// <returns>The virtual key details</returns>
    public async Task<IResult> GetKeyById(int id)
    {
        var result = await _virtualKeyService.GetVirtualKeyInfoAsync(id);
        if (result == null)
        {
            return AdminResults.NotFoundEntity("Virtual key", id);
        }
        return Ok(result);
    }

    /// <summary>
    /// Updates an existing virtual key
    /// </summary>
    /// <param name="id">The ID of the key to update</param>
    /// <param name="request">The updated key details</param>
    /// <returns>No content if successful</returns>
    public async Task<IResult> UpdateKey(int id, UpdateVirtualKeyRequestDto request)
    {
        // Fetch pre-state for change tracking
        var preState = await _virtualKeyService.GetVirtualKeyInfoAsync(id);
        if (preState == null)
            throw new KeyNotFoundException();

        if (!await _virtualKeyService.UpdateVirtualKeyAsync(id, request))
            throw new KeyNotFoundException();

        // Build change list from pre-state vs request
        var changes = new List<(string Property, string? OldValue, string? NewValue)>();

        if (request.KeyName != null && preState.KeyName != request.KeyName)
            changes.Add(("KeyName", preState.KeyName, request.KeyName));
        if (request.IsEnabled.HasValue && preState.IsEnabled != request.IsEnabled.Value)
            changes.Add(("IsEnabled", preState.IsEnabled.ToString(), request.IsEnabled.Value.ToString()));
        if (request.AllowedModels != null && preState.AllowedModels != request.AllowedModels)
            changes.Add(("AllowedModels", preState.AllowedModels ?? "null", request.AllowedModels));
        if (request.ExpiresAt.HasValue && preState.ExpiresAt != request.ExpiresAt)
            changes.Add(("ExpiresAt", preState.ExpiresAt?.ToString("o") ?? "null", request.ExpiresAt?.ToString("o") ?? "null"));
        if (request.RateLimitRpm.HasValue && preState.RateLimitRpm != request.RateLimitRpm)
            changes.Add(("RateLimitRpm", preState.RateLimitRpm?.ToString() ?? "null", request.RateLimitRpm?.ToString() ?? "null"));
        if (request.RateLimitRpd.HasValue && preState.RateLimitRpd != request.RateLimitRpd)
            changes.Add(("RateLimitRpd", preState.RateLimitRpd?.ToString() ?? "null", request.RateLimitRpd?.ToString() ?? "null"));
        if (request.VirtualKeyGroupId.HasValue && preState.VirtualKeyGroupId != request.VirtualKeyGroupId.Value)
            changes.Add(("VirtualKeyGroupId", preState.VirtualKeyGroupId.ToString(), request.VirtualKeyGroupId.Value.ToString()));

        if (changes.Count > 0)
        {
            LogAdminAuditWithChanges("VirtualKey", id, changes);
        }
        else
        {
            LogAdminAudit("Updated", "VirtualKey", id, "No changes detected");
        }
        AdminOperationsMetricsService.RecordVirtualKeyOperation("update", "success");
        AdminOperationsMetricsService.RecordConfigurationChange("virtualkey", "update");
        return NoContent();
    }

    /// <summary>
    /// Deletes a virtual key by ID
    /// </summary>
    /// <param name="id">The ID of the key to delete</param>
    /// <returns>No content if successful</returns>
    public async Task<IResult> DeleteKey(int id)
    {
        if (!await _virtualKeyService.DeleteVirtualKeyAsync(id))
            throw new KeyNotFoundException();
        LogAdminAudit("Deleted", "VirtualKey", id);
        AdminOperationsMetricsService.RecordVirtualKeyOperation("delete", "success");
        AdminOperationsMetricsService.RecordConfigurationChange("virtualkey", "delete");
        return NoContent();
    }


    /// <summary>
    /// Validates a virtual key
    /// </summary>
    /// <param name="request">The validation request containing the key and optional model</param>
    /// <returns>The validation result</returns>
    // lgtm [cs/web/missing-function-level-access-control]
    public async Task<IResult> ValidateKey(ValidateVirtualKeyRequest request)
    {
        var result = await _virtualKeyService.ValidateVirtualKeyAsync(request.Key, request.RequestedModel);
        return Ok(result);
    }




    /// <summary>
    /// Gets detailed information about a virtual key for validation purposes
    /// </summary>
    /// <param name="id">The ID of the virtual key</param>
    /// <returns>The virtual key validation information</returns>
    public async Task<IResult> GetValidationInfo(int id)
    {
        var result = await _virtualKeyService.GetValidationInfoAsync(id);
        if (result == null)
        {
            return AdminResults.NotFoundEntity("Virtual key", id);
        }
        return Ok(result);
    }

    /// <summary>
    /// Performs maintenance tasks on all virtual keys
    /// </summary>
    /// <remarks>
    /// This endpoint performs the following maintenance tasks:
    /// - Disables keys that have passed their expiration date
    /// Budget resets are no longer performed in the bank account model.
    /// This is typically called by a background service.
    /// </remarks>
    /// <returns>No content if successful</returns>
    public async Task<IResult> PerformMaintenance()
    {
        await _virtualKeyService.PerformMaintenanceAsync();
        return NoContent();
    }

    /// <summary>
    /// Previews the discovery results for a virtual key
    /// </summary>
    /// <param name="id">The ID of the virtual key</param>
    /// <param name="capability">Optional capability filter (e.g. "chat", "vision", "audio_transcription")</param>
    /// <returns>The discovery results as the virtual key would see them</returns>
    public async Task<IResult> PreviewDiscovery(int id, string? capability = null)
    {
        var result = await _virtualKeyService.PreviewDiscoveryAsync(id, capability);
        if (result == null)
        {
            return AdminResults.NotFoundEntity("Virtual key", id);
        }
        return Ok(result);
    }

    /// <summary>
    /// Get the virtual key group for a specific key
    /// </summary>
    /// <param name="id">The ID of the virtual key</param>
    /// <returns>The virtual key group information</returns>
    public async Task<IResult> GetKeyGroup(int id)
    {
        var key = await _virtualKeyService.GetVirtualKeyByIdAsync(id);
        if (key == null)
        {
            throw new KeyNotFoundException("Virtual key not found");
        }

        var groupInfo = await _virtualKeyService.GetKeyGroupAsync(id);
        if (groupInfo == null)
        {
            throw new KeyNotFoundException("Virtual key group not found");
        }

        return Ok(groupInfo);
    }

    /// <summary>
    /// Get usage information for a virtual key by its key value
    /// </summary>
    /// <param name="key">The virtual key value (with prefix)</param>
    /// <returns>Usage information including balance, spending, and request counts</returns>
    /// <remarks>
    /// This endpoint allows administrators to check the usage and balance of a virtual key
    /// using the actual key value instead of the database ID. This is useful for support
    /// scenarios where users provide their key value.
    /// </remarks>
    public async Task<IResult> GetUsageByKey(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return BadRequest(new { message = "Key value is required" });
        }

        var result = await _virtualKeyService.GetUsageByKeyAsync(key);
        if (result == null)
        {
            return AdminResults.NotFoundEntity("Virtual key", null);
        }
        return Ok(result);
    }
}
