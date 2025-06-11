using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.DTOs.IpFilter;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConduitLLM.Admin.Controllers;

/// <summary>
/// Controller for managing IP filters
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "MasterKeyPolicy")]
public class IpFilterController : ControllerBase
{
    private readonly IAdminIpFilterService _ipFilterService;
    private readonly ILogger<IpFilterController> _logger;

    /// <summary>
    /// Initializes a new instance of the IpFilterController
    /// </summary>
    /// <param name="ipFilterService">The IP filter service</param>
    /// <param name="logger">The logger</param>
    public IpFilterController(
        IAdminIpFilterService ipFilterService,
        ILogger<IpFilterController> logger)
    {
        _ipFilterService = ipFilterService ?? throw new ArgumentNullException(nameof(ipFilterService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets all IP filters
    /// </summary>
    /// <returns>List of all IP filters</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<IpFilterDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetAllFilters()
    {
        try
        {
            var filters = await _ipFilterService.GetAllFiltersAsync();
            return Ok(filters);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all IP filters");
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }

    /// <summary>
    /// Gets all enabled IP filters
    /// </summary>
    /// <returns>List of all enabled IP filters</returns>
    [HttpGet("enabled")]
    [ProducesResponseType(typeof(IEnumerable<IpFilterDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetEnabledFilters()
    {
        try
        {
            var filters = await _ipFilterService.GetEnabledFiltersAsync();
            return Ok(filters);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting enabled IP filters");
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }

    /// <summary>
    /// Gets an IP filter by ID
    /// </summary>
    /// <param name="id">The ID of the filter to get</param>
    /// <returns>The IP filter</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(IpFilterDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetFilterById(int id)
    {
        try
        {
            var filter = await _ipFilterService.GetFilterByIdAsync(id);

            if (filter == null)
            {
                return NotFound($"IP filter with ID {id} not found");
            }

            return Ok(filter);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting IP filter with ID {Id}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }

    /// <summary>
    /// Creates a new IP filter
    /// </summary>
    /// <param name="filter">The filter to create</param>
    /// <returns>The created filter</returns>
    [HttpPost]
    [Authorize(Policy = "MasterKeyPolicy")]
    [ProducesResponseType(typeof(IpFilterDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateFilter([FromBody] CreateIpFilterDto filter)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var (success, errorMessage, createdFilter) = await _ipFilterService.CreateFilterAsync(filter);

            if (!success)
            {
                return BadRequest(errorMessage);
            }

            return CreatedAtAction(nameof(GetFilterById), new { id = createdFilter!.Id }, createdFilter);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating IP filter");
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }

    /// <summary>
    /// Updates an existing IP filter
    /// </summary>
    /// <param name="id">The ID of the filter to update</param>
    /// <param name="filter">The updated filter data</param>
    /// <returns>No content if successful</returns>
    [HttpPut("{id}")]
    [Authorize(Policy = "MasterKeyPolicy")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateFilter(int id, [FromBody] UpdateIpFilterDto filter)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // Ensure ID in route matches ID in body
        if (id != filter.Id)
        {
            return BadRequest("ID in route must match ID in body");
        }

        try
        {
            var (success, errorMessage) = await _ipFilterService.UpdateFilterAsync(filter);

            if (!success)
            {
                if (errorMessage?.Contains("not found") == true)
                {
                    return NotFound(errorMessage);
                }

                return BadRequest(errorMessage);
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating IP filter with ID {Id}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }

    /// <summary>
    /// Deletes an IP filter
    /// </summary>
    /// <param name="id">The ID of the filter to delete</param>
    /// <returns>No content if successful</returns>
    [HttpDelete("{id}")]
    [Authorize(Policy = "MasterKeyPolicy")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteFilter(int id)
    {
        try
        {
            var (success, errorMessage) = await _ipFilterService.DeleteFilterAsync(id);

            if (!success)
            {
                if (errorMessage?.Contains("not found") == true)
                {
                    return NotFound(errorMessage);
                }

                return BadRequest(errorMessage);
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting IP filter with ID {Id}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }

    /// <summary>
    /// Gets the current IP filter settings
    /// </summary>
    /// <returns>The current IP filter settings</returns>
    [HttpGet("settings")]
    [ProducesResponseType(typeof(IpFilterSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetSettings()
    {
        try
        {
            var settings = await _ipFilterService.GetIpFilterSettingsAsync();
            return Ok(settings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting IP filter settings");
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }

    /// <summary>
    /// Updates the IP filter settings
    /// </summary>
    /// <param name="settings">The new settings</param>
    /// <returns>No content if successful</returns>
    [HttpPut("settings")]
    [Authorize(Policy = "MasterKeyPolicy")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateSettings([FromBody] IpFilterSettingsDto settings)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var (success, errorMessage) = await _ipFilterService.UpdateIpFilterSettingsAsync(settings);

            if (!success)
            {
                return BadRequest(errorMessage);
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating IP filter settings");
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }

    /// <summary>
    /// Checks if an IP address is allowed based on current filter rules
    /// </summary>
    /// <param name="ipAddress">The IP address to check</param>
    /// <returns>Result indicating if the IP is allowed and reason if denied</returns>
    [HttpGet("check/{ipAddress}")]
    [AllowAnonymous] // This needs to be accessible without authentication for performance
    [ProducesResponseType(typeof(IpCheckResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CheckIpAddress(string ipAddress)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
        {
            return BadRequest("IP address must be provided");
        }

        try
        {
            var result = await _ipFilterService.CheckIpAddressAsync(ipAddress);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking IP address {IpAddress}", ipAddress);
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }
}
