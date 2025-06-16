using ConduitLLM.Configuration.DTOs.VirtualKey;
using ConduitLLM.Core.Extensions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.WebUI.Interfaces;
using ConduitLLM.WebUI.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using static ConduitLLM.Core.Extensions.LoggingSanitizer;

namespace ConduitLLM.WebUI.Controllers;

[ApiController]
[Route("api/[controller]")] // Route: /api/virtualkeys
public class VirtualKeysController : ControllerBase
{
    private readonly ConduitLLM.WebUI.Interfaces.IVirtualKeyService _virtualKeyService;
    private readonly ILogger<VirtualKeysController> _logger;

    public VirtualKeysController(ConduitLLM.WebUI.Interfaces.IVirtualKeyService virtualKeyService, ILogger<VirtualKeysController> logger)
    {
        _virtualKeyService = virtualKeyService;
        _logger = logger;
    }

    /// <summary>
    /// Generates a new virtual API key.
    /// Requires Master Key authentication.
    /// </summary>
    /// <param name="request">Details for the key to be created.</param>
    /// <returns>The generated key details or an error response.</returns>
    [HttpPost] // POST /api/virtualkeys
    [Authorize(Policy = "MasterKeyPolicy")] // Apply policy
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
            // Log the detailed exception
            _logger.LogErrorSecure(dbEx, "Database update error creating virtual key named {KeyName}. Check for constraint violations.", request.KeyName);
            // Return a more generic error to the client
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while saving the key. It might violate a unique constraint (e.g., duplicate name)." });
        }
        catch (Exception ex)
        {
            _logger.LogErrorSecure(ex, "Error generating virtual key for '{KeyName}'", request.KeyName);
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }

    /// <summary>
    /// Retrieves a list of all virtual keys.
    /// Requires Master Key authentication.
    /// </summary>
    [HttpGet] // GET /api/virtualkeys
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
    /// Retrieves details for a specific virtual key by ID.
    /// Requires Master Key authentication.
    /// </summary>
    [HttpGet("{id}", Name = "GetKeyById")] // GET /api/virtualkeys/{id}
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
            _logger.LogError(ex, "Error getting virtual key with ID {KeyId}.", S(id));
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }

    /// <summary>
    /// Updates an existing virtual key.
    /// Requires Master Key authentication.
    /// </summary>
    [HttpPut("{id}")] // PUT /api/virtualkeys/{id}
    [Authorize(Policy = "MasterKeyPolicy")] // Apply policy
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
                return NotFound(); // Key with the given ID not found
            }
            return NoContent(); // Success
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating virtual key with ID {KeyId}.", S(id));
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }

    /// <summary>
    /// Deletes a virtual key by ID.
    /// Requires Master Key authentication.
    /// </summary>
    [HttpDelete("{id}")] // DELETE /api/virtualkeys/{id}
    [Authorize(Policy = "MasterKeyPolicy")] // Apply policy
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
                return NotFound(); // Key not found
            }
            return NoContent(); // Deletion successful
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting virtual key with ID {KeyId}.", S(id));
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }

    /// <summary>
    /// Resets the spend for a virtual key.
    /// Requires Master Key authentication.
    /// </summary>
    [HttpPost("{id}/reset-spend")] // POST /api/virtualkeys/{id}/reset-spend
    [Authorize(Policy = "MasterKeyPolicy")] // Apply policy
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
                return NotFound(); // Key not found
            }
            return NoContent(); // Reset successful
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting spend for virtual key with ID {KeyId}.", S(id));
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }
}
