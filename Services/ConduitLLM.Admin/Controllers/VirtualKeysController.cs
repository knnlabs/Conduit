using ConduitLLM.Core.Extensions;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.DTOs.VirtualKey;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConduitLLM.Admin.Controllers;

/// <summary>
/// Controller for managing virtual keys
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class VirtualKeysController : AdminControllerBase
{
    private readonly IAdminVirtualKeyService _virtualKeyService;

    /// <summary>
    /// Initializes a new instance of the VirtualKeysController
    /// </summary>
    /// <param name="virtualKeyService">The virtual key service</param>
    /// <param name="logger">The logger</param>
    public VirtualKeysController(
        IAdminVirtualKeyService virtualKeyService,
        ILogger<VirtualKeysController> logger)
        : base(logger)
    {
        _virtualKeyService = virtualKeyService ?? throw new ArgumentNullException(nameof(virtualKeyService));
    }

    /// <summary>
    /// Generates a new virtual API key
    /// </summary>
    /// <param name="request">Details for the key to be created</param>
    /// <returns>The generated key details or an error response</returns>
    [HttpPost]
    [Authorize(Policy = "MasterKeyPolicy")]
    [ProducesResponseType(typeof(CreateVirtualKeyResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public Task<IActionResult> GenerateKey([FromBody] CreateVirtualKeyRequestDto request)
    {
        return ExecuteAsync(
            () => _virtualKeyService.GenerateVirtualKeyAsync(request),
            response =>
            {
                LogAdminAudit("Created", "VirtualKey", response.KeyInfo.Id, $"Name: {LoggingSanitizer.S(request.KeyName)}");
                return CreatedAtAction(nameof(GetKeyById), new { id = response.KeyInfo.Id }, response);
            },
            "GenerateKey",
            new { KeyName = LoggingSanitizer.S(request.KeyName) });
    }

    /// <summary>
    /// Retrieves a list of all virtual keys
    /// </summary>
    /// <param name="virtualKeyGroupId">Optional filter by virtual key group ID</param>
    /// <returns>List of all virtual keys</returns>
    [HttpGet]
    [Authorize(Policy = "MasterKeyPolicy")]
    [ProducesResponseType(typeof(List<VirtualKeyDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public Task<IActionResult> ListKeys([FromQuery] int? virtualKeyGroupId = null)
    {
        return ExecuteAsync(
            () => _virtualKeyService.ListVirtualKeysAsync(virtualKeyGroupId),
            Ok,
            "ListKeys");
    }

    /// <summary>
    /// Retrieves details for a specific virtual key by ID
    /// </summary>
    /// <param name="id">The ID of the key to retrieve</param>
    /// <returns>The virtual key details</returns>
    [HttpGet("{id}", Name = "GetKeyById")]
    [Authorize(Policy = "MasterKeyPolicy")]
    [ProducesResponseType(typeof(VirtualKeyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public Task<IActionResult> GetKeyById(int id)
    {
        return ExecuteWithNotFoundAsync(
            () => _virtualKeyService.GetVirtualKeyInfoAsync(id),
            Ok,
            "Virtual key",
            id,
            "GetKeyById");
    }

    /// <summary>
    /// Updates an existing virtual key
    /// </summary>
    /// <param name="id">The ID of the key to update</param>
    /// <param name="request">The updated key details</param>
    /// <returns>No content if successful</returns>
    [HttpPut("{id}")]
    [Authorize(Policy = "MasterKeyPolicy")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public Task<IActionResult> UpdateKey(int id, [FromBody] UpdateVirtualKeyRequestDto request)
    {
        return ExecuteAsync(
            async () =>
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
            },
            NoContent(),
            "UpdateKey",
            new { Id = id });
    }

    /// <summary>
    /// Deletes a virtual key by ID
    /// </summary>
    /// <param name="id">The ID of the key to delete</param>
    /// <returns>No content if successful</returns>
    [HttpDelete("{id}")]
    [Authorize(Policy = "MasterKeyPolicy")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public Task<IActionResult> DeleteKey(int id)
    {
        return ExecuteAsync(
            async () =>
            {
                if (!await _virtualKeyService.DeleteVirtualKeyAsync(id))
                    throw new KeyNotFoundException();
                LogAdminAudit("Deleted", "VirtualKey", id);
            },
            NoContent(),
            "DeleteKey",
            new { Id = id });
    }


    /// <summary>
    /// Validates a virtual key
    /// </summary>
    /// <param name="request">The validation request containing the key and optional model</param>
    /// <returns>The validation result</returns>
    [HttpPost("validate")]
    [ProducesResponseType(typeof(VirtualKeyValidationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    // lgtm [cs/web/missing-function-level-access-control]
    public Task<IActionResult> ValidateKey([FromBody] ValidateVirtualKeyRequest request)
    {
        return ExecuteAsync(
            () => _virtualKeyService.ValidateVirtualKeyAsync(request.Key, request.RequestedModel),
            Ok,
            "ValidateKey");
    }




    /// <summary>
    /// Gets detailed information about a virtual key for validation purposes
    /// </summary>
    /// <param name="id">The ID of the virtual key</param>
    /// <returns>The virtual key validation information</returns>
    [HttpGet("{id}/validation-info")]
    [Authorize(Policy = "MasterKeyPolicy")]
    [ProducesResponseType(typeof(VirtualKeyValidationInfoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public Task<IActionResult> GetValidationInfo(int id)
    {
        return ExecuteWithNotFoundAsync(
            () => _virtualKeyService.GetValidationInfoAsync(id),
            Ok,
            "Virtual key",
            id,
            "GetValidationInfo");
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
    [HttpPost("maintenance")]
    [Authorize(Policy = "MasterKeyPolicy")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public Task<IActionResult> PerformMaintenance()
    {
        return ExecuteAsync(
            () => _virtualKeyService.PerformMaintenanceAsync(),
            NoContent(),
            "PerformMaintenance");
    }

    /// <summary>
    /// Previews the discovery results for a virtual key
    /// </summary>
    /// <param name="id">The ID of the virtual key</param>
    /// <param name="capability">Optional capability filter (e.g. "chat", "vision", "audio_transcription")</param>
    /// <returns>The discovery results as the virtual key would see them</returns>
    [HttpGet("{id}/discovery-preview")]
    [Authorize(Policy = "MasterKeyPolicy")]
    [ProducesResponseType(typeof(VirtualKeyDiscoveryPreviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public Task<IActionResult> PreviewDiscovery(int id, [FromQuery] string? capability = null)
    {
        return ExecuteWithNotFoundAsync(
            () => _virtualKeyService.PreviewDiscoveryAsync(id, capability),
            Ok,
            "Virtual key",
            id,
            "PreviewDiscovery");
    }

    /// <summary>
    /// Get the virtual key group for a specific key
    /// </summary>
    /// <param name="id">The ID of the virtual key</param>
    /// <returns>The virtual key group information</returns>
    [HttpGet("{id}/group")]
    [Authorize(Policy = "MasterKeyPolicy")]
    [ProducesResponseType(typeof(VirtualKeyGroupDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public Task<IActionResult> GetKeyGroup(int id)
    {
        return ExecuteAsync(
            async () =>
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

                return groupInfo;
            },
            Ok,
            "GetKeyGroup",
            new { Id = id });
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
    [HttpGet("usage/by-key/{key}")]
    [Authorize(Policy = "MasterKeyPolicy")]
    [ProducesResponseType(typeof(VirtualKeyUsageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public Task<IActionResult> GetUsageByKey(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return Task.FromResult<IActionResult>(BadRequest(new { message = "Key value is required" }));
        }

        return ExecuteWithNotFoundAsync(
            () => _virtualKeyService.GetUsageByKeyAsync(key),
            Ok,
            "Virtual key",
            null,
            "GetUsageByKey");
    }
}
