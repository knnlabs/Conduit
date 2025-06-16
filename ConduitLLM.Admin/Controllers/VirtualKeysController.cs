using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.DTOs.VirtualKey;
using ConduitLLM.Core.Extensions;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using static ConduitLLM.Core.Extensions.LoggingSanitizer;

namespace ConduitLLM.Admin.Controllers;

/// <summary>
/// Controller for managing virtual keys
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class VirtualKeysController : ControllerBase
{
    private readonly IAdminVirtualKeyService _virtualKeyService;
    private readonly ILogger<VirtualKeysController> _logger;

    /// <summary>
    /// Initializes a new instance of the VirtualKeysController
    /// </summary>
    /// <param name="virtualKeyService">The virtual key service</param>
    /// <param name="logger">The logger</param>
    public VirtualKeysController(
        IAdminVirtualKeyService virtualKeyService,
        ILogger<VirtualKeysController> logger)
    {
        _virtualKeyService = virtualKeyService ?? throw new ArgumentNullException(nameof(virtualKeyService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
    public async Task<IActionResult> GenerateKey([FromBody] CreateVirtualKeyRequestDto request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var response = await _virtualKeyService.GenerateVirtualKeyAsync(request);
            return CreatedAtAction(nameof(GetKeyById), new { id = response.KeyInfo.Id }, response);
        }
        catch (DbUpdateException dbEx)
        {
            _logger.LogError(dbEx, "Database update error creating virtual key named {KeyName}. Check for constraint violations.", request.KeyName.Replace(Environment.NewLine, ""));
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while saving the key. It might violate a unique constraint (e.g., duplicate name)." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating virtual key for '{KeyName}'", request.KeyName.Replace(Environment.NewLine, ""));
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }

    /// <summary>
    /// Retrieves a list of all virtual keys
    /// </summary>
    /// <returns>List of all virtual keys</returns>
    [HttpGet]
    [Authorize(Policy = "MasterKeyPolicy")]
    [ProducesResponseType(typeof(List<VirtualKeyDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ListKeys()
    {
        try
        {
            var keys = await _virtualKeyService.ListVirtualKeysAsync();
            return Ok(keys);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing virtual keys.");
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
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
    public async Task<IActionResult> GetKeyById(int id)
    {
        try
        {
            var key = await _virtualKeyService.GetVirtualKeyInfoAsync(id);
            if (key == null)
            {
                return NotFound();
            }
            return Ok(key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting virtual key with ID {KeyId}.", id);
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
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
    public async Task<IActionResult> UpdateKey(int id, [FromBody] UpdateVirtualKeyRequestDto request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var success = await _virtualKeyService.UpdateVirtualKeyAsync(id, request);
            if (!success)
            {
                return NotFound();
            }
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating virtual key with ID {KeyId}.", id);
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
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
    public async Task<IActionResult> DeleteKey(int id)
    {
        try
        {
            var success = await _virtualKeyService.DeleteVirtualKeyAsync(id);
            if (!success)
            {
                return NotFound();
            }
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting virtual key with ID {KeyId}.", id);
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }

    /// <summary>
    /// Resets the spend for a virtual key
    /// </summary>
    /// <param name="id">The ID of the key to reset</param>
    /// <returns>No content if successful</returns>
    [HttpPost("{id}/reset-spend")]
    [Authorize(Policy = "MasterKeyPolicy")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ResetKeySpend(int id)
    {
        try
        {
            var success = await _virtualKeyService.ResetSpendAsync(id);
            if (!success)
            {
                return NotFound();
            }
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting spend for virtual key with ID {KeyId}.", id);
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
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
    public async Task<IActionResult> ValidateKey([FromBody] ValidateVirtualKeyRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var result = await _virtualKeyService.ValidateVirtualKeyAsync(request.Key, request.RequestedModel);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating virtual key");
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }

    /// <summary>
    /// Updates the spend amount for a virtual key
    /// </summary>
    /// <param name="id">The ID of the virtual key</param>
    /// <param name="request">The request containing the cost to add</param>
    /// <returns>No content if successful</returns>
    [HttpPost("{id}/spend")]
    [Authorize(Policy = "MasterKeyPolicy")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateSpend(int id, [FromBody] UpdateSpendRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var success = await _virtualKeyService.UpdateSpendAsync(id, request.Cost);
            if (!success)
            {
                return NotFound();
            }
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating spend for virtual key with ID {KeyId}.", id);
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }

    /// <summary>
    /// Checks if the budget period has expired and resets if needed
    /// </summary>
    /// <param name="id">The ID of the virtual key</param>
    /// <returns>The result of the budget check</returns>
    [HttpPost("{id}/check-budget")]
    [Authorize(Policy = "MasterKeyPolicy")]
    [ProducesResponseType(typeof(BudgetCheckResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CheckBudget(int id)
    {
        try
        {
            var key = await _virtualKeyService.GetVirtualKeyInfoAsync(id);
            if (key == null)
            {
                return NotFound();
            }

            var result = await _virtualKeyService.CheckBudgetAsync(id);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking budget for virtual key with ID {KeyId}.", id);
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
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
    public async Task<IActionResult> GetValidationInfo(int id)
    {
        try
        {
            var info = await _virtualKeyService.GetValidationInfoAsync(id);
            if (info == null)
            {
                return NotFound();
            }
            return Ok(info);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting validation info for virtual key with ID {KeyId}.", id);
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }

    /// <summary>
    /// Performs maintenance tasks on all virtual keys
    /// </summary>
    /// <remarks>
    /// This endpoint performs the following maintenance tasks:
    /// - Resets budgets for keys with expired budget periods (daily/monthly)
    /// - Disables keys that have passed their expiration date
    /// This is typically called by a background service.
    /// </remarks>
    /// <returns>No content if successful</returns>
    [HttpPost("maintenance")]
    [Authorize(Policy = "MasterKeyPolicy")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> PerformMaintenance()
    {
        try
        {
            await _virtualKeyService.PerformMaintenanceAsync();
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing virtual key maintenance");
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred during maintenance.");
        }
    }
}
