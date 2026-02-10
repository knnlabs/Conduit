using ConduitLLM.Core.Extensions;
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
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public Task<IActionResult> GetAllFilters()
    {
        return ExecuteAsync(
            () => _ipFilterService.GetAllFiltersAsync(),
            Ok,
            "GetAllFilters");
    }

    /// <summary>
    /// Gets all enabled IP filters
    /// </summary>
    /// <returns>List of all enabled IP filters</returns>
    [HttpGet("enabled")]
    [ProducesResponseType(typeof(IEnumerable<IpFilterDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public Task<IActionResult> GetEnabledFilters()
    {
        return ExecuteAsync(
            () => _ipFilterService.GetEnabledFiltersAsync(),
            Ok,
            "GetEnabledFilters");
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
    public Task<IActionResult> GetFilterById(int id)
    {
        return ExecuteWithNotFoundAsync(
            () => _ipFilterService.GetFilterByIdAsync(id),
            Ok,
            "IP filter",
            id,
            "GetFilterById");
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
    public Task<IActionResult> CreateFilter([FromBody] CreateIpFilterDto filter)
    {
        if (!ModelState.IsValid)
        {
            return Task.FromResult<IActionResult>(BadRequest(ModelState));
        }

        return ExecuteAsync(
            async () =>
            {
                var (success, errorMessage, createdFilter) = await _ipFilterService.CreateFilterAsync(filter);

                if (!success)
                {
                    throw new InvalidOperationException(errorMessage);
                }

                return createdFilter!;
            },
            createdFilter => CreatedAtAction(nameof(GetFilterById), new { id = createdFilter.Id }, createdFilter),
            "CreateFilter");
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
    public Task<IActionResult> UpdateFilter(int id, [FromBody] UpdateIpFilterDto filter)
    {
        if (!ModelState.IsValid)
        {
            return Task.FromResult<IActionResult>(BadRequest(ModelState));
        }

        // Ensure ID in route matches ID in body
        if (id != filter.Id)
        {
            return Task.FromResult<IActionResult>(BadRequest("ID in route must match ID in body"));
        }

        return ExecuteAsync(
            async () =>
            {
                var (success, errorMessage) = await _ipFilterService.UpdateFilterAsync(filter);

                if (!success)
                {
                    if (errorMessage?.Contains("not found") == true)
                    {
                        throw new KeyNotFoundException(errorMessage);
                    }

                    throw new InvalidOperationException(errorMessage);
                }
            },
            NoContent(),
            "UpdateFilter",
            new { Id = id });
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
    public Task<IActionResult> DeleteFilter(int id)
    {
        return ExecuteAsync(
            async () =>
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
            },
            NoContent(),
            "DeleteFilter",
            new { Id = id });
    }

    /// <summary>
    /// Gets the current IP filter settings
    /// </summary>
    /// <returns>The current IP filter settings</returns>
    [HttpGet("settings")]
    [ProducesResponseType(typeof(IpFilterSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public Task<IActionResult> GetSettings()
    {
        return ExecuteAsync(
            () => _ipFilterService.GetIpFilterSettingsAsync(),
            Ok,
            "GetSettings");
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
    public Task<IActionResult> UpdateSettings([FromBody] IpFilterSettingsDto settings)
    {
        if (!ModelState.IsValid)
        {
            return Task.FromResult<IActionResult>(BadRequest(ModelState));
        }

        return ExecuteAsync(
            async () =>
            {
                var (success, errorMessage) = await _ipFilterService.UpdateIpFilterSettingsAsync(settings);

                if (!success)
                {
                    throw new InvalidOperationException(errorMessage);
                }
            },
            NoContent(),
            "UpdateSettings");
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
    public Task<IActionResult> CheckIpAddress(string ipAddress)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
        {
            return Task.FromResult<IActionResult>(BadRequest("IP address must be provided"));
        }

        return ExecuteAsync(
            () => _ipFilterService.CheckIpAddressAsync(ipAddress),
            Ok,
            "CheckIpAddress",
            new { IpAddress = LoggingSanitizer.S(ipAddress) });
    }
}
