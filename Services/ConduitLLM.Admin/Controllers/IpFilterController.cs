using ConduitLLM.Core.Extensions;
using ConduitLLM.Admin.Extensions;
using ConduitLLM.Admin.Filters;
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
[ServiceFilter(typeof(OperationLoggingFilter))]
public class IpFilterController : AdminControllerBase
{
    private readonly IAdminIpFilterService _ipFilterService;

    /// <summary>
    /// Initializes a new instance of the IpFilterController
    /// </summary>
    /// <param name="ipFilterService">The IP filter service</param>
    /// <param name="logger">The logger</param>
    public IpFilterController(
        IAdminIpFilterService ipFilterService,
        ILogger<IpFilterController> logger)
        : base(logger)
    {
        _ipFilterService = ipFilterService ?? throw new ArgumentNullException(nameof(ipFilterService));
    }

    /// <summary>
    /// Gets all IP filters
    /// </summary>
    /// <returns>List of all IP filters</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<IpFilterDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllFilters()
    {
        var filters = await _ipFilterService.GetAllFiltersAsync();
        return Ok(filters);
    }

    /// <summary>
    /// Gets all enabled IP filters
    /// </summary>
    /// <returns>List of all enabled IP filters</returns>
    [HttpGet("enabled")]
    [ProducesResponseType(typeof(IEnumerable<IpFilterDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEnabledFilters()
    {
        var filters = await _ipFilterService.GetEnabledFiltersAsync();
        return Ok(filters);
    }

    /// <summary>
    /// Gets the IP filters scoped to a specific virtual key
    /// </summary>
    /// <param name="virtualKeyId">The virtual key ID</param>
    /// <returns>List of the virtual key's IP filters</returns>
    [HttpGet("by-virtual-key/{virtualKeyId}")]
    [ProducesResponseType(typeof(IEnumerable<IpFilterDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFiltersByVirtualKey(int virtualKeyId)
    {
        var filters = await _ipFilterService.GetFiltersByVirtualKeyIdAsync(virtualKeyId);
        return Ok(filters);
    }

    /// <summary>
    /// Gets an IP filter by ID
    /// </summary>
    /// <param name="id">The ID of the filter to get</param>
    /// <returns>The IP filter</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(IpFilterDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFilterById(int id)
    {
        var filter = await _ipFilterService.GetFilterByIdAsync(id);
        if (filter == null)
        {
            return this.NotFoundEntity("IP filter", id);
        }
        return Ok(filter);
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
    public async Task<IActionResult> CreateFilter([FromBody] CreateIpFilterDto filter)
    {
        var (success, errorMessage, createdFilter) = await _ipFilterService.CreateFilterAsync(filter);

        if (!success)
        {
            throw new InvalidOperationException(errorMessage);
        }

        LogAdminAudit("Created", "IpFilter", createdFilter!.Id, $"CIDR: {LoggingSanitizer.S(filter.IpAddressOrCidr)}, Type: {filter.FilterType}");
        return CreatedAtAction(nameof(GetFilterById), new { id = createdFilter.Id }, createdFilter);
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
    public async Task<IActionResult> UpdateFilter(int id, [FromBody] UpdateIpFilterDto filter)
    {
        // Ensure ID in route matches ID in body
        if (id != filter.Id)
        {
            return BadRequest("ID in route must match ID in body");
        }

        var (success, errorMessage) = await _ipFilterService.UpdateFilterAsync(filter);

        if (!success)
        {
            if (errorMessage?.Contains("not found") == true)
            {
                throw new KeyNotFoundException(errorMessage);
            }

            throw new InvalidOperationException(errorMessage);
        }

        LogAdminAudit("Updated", "IpFilter", id, $"CIDR: {LoggingSanitizer.S(filter.IpAddressOrCidr)}, Type: {filter.FilterType}");
        return NoContent();
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
    public async Task<IActionResult> DeleteFilter(int id)
    {
        var (success, errorMessage) = await _ipFilterService.DeleteFilterAsync(id);

        if (!success)
        {
            if (errorMessage?.Contains("not found") == true)
            {
                throw new KeyNotFoundException(errorMessage);
            }

            throw new InvalidOperationException(errorMessage);
        }

        LogAdminAudit("Deleted", "IpFilter", id, $"Id: {id}");
        return NoContent();
    }

    /// <summary>
    /// Gets the current IP filter settings
    /// </summary>
    /// <returns>The current IP filter settings</returns>
    [HttpGet("settings")]
    [ProducesResponseType(typeof(IpFilterSettingsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSettings()
    {
        var settings = await _ipFilterService.GetIpFilterSettingsAsync();
        return Ok(settings);
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
    public async Task<IActionResult> UpdateSettings([FromBody] IpFilterSettingsDto settings)
    {
        var (success, errorMessage) = await _ipFilterService.UpdateIpFilterSettingsAsync(settings);

        if (!success)
        {
            throw new InvalidOperationException(errorMessage);
        }

        LogAdminAudit("Updated", "IpFilterSettings", detail: $"Enabled: {settings.IsEnabled}, DefaultAllow: {settings.DefaultAllow}");
        return NoContent();
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
    public async Task<IActionResult> CheckIpAddress(string ipAddress)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
        {
            return BadRequest("IP address must be provided");
        }

        var result = await _ipFilterService.CheckIpAddressAsync(ipAddress);
        return Ok(result);
    }
}
