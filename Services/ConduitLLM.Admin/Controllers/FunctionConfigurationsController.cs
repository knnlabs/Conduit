using ConduitLLM.Core.Extensions;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Functions.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConduitLLM.Admin.Controllers;

/// <summary>
/// Controller for managing function configurations.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "MasterKeyPolicy")]
public class FunctionConfigurationsController : ControllerBase
{
    private readonly IFunctionConfigurationRepository _configurationRepository;
    private readonly ILogger<FunctionConfigurationsController> _logger;

    /// <summary>
    /// Initializes a new instance of the FunctionConfigurationsController.
    /// </summary>
    public FunctionConfigurationsController(
        IFunctionConfigurationRepository configurationRepository,
        ILogger<FunctionConfigurationsController> logger)
    {
        _configurationRepository = configurationRepository ?? throw new ArgumentNullException(nameof(configurationRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets all function configurations.
    /// </summary>
    /// <returns>List of all function configurations</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetAllConfigurations()
    {
        try
        {
            var configurations = await _configurationRepository.GetAllAsync();
            return Ok(configurations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all function configurations");
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }

    /// <summary>
    /// Gets a function configuration by ID.
    /// </summary>
    /// <param name="id">The ID of the function configuration</param>
    /// <returns>The function configuration</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetConfigurationById(int id)
    {
        try
        {
            var configuration = await _configurationRepository.GetByIdAsync(id);

            if (configuration == null)
            {
                return NotFound(new ErrorResponseDto("Function configuration not found"));
            }

            return Ok(configuration);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting function configuration with ID {Id}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }

    /// <summary>
    /// Gets function configurations by provider type.
    /// </summary>
    /// <param name="providerType">The provider type (e.g., "Exa")</param>
    /// <returns>List of function configurations for the specified provider</returns>
    [HttpGet("provider/{providerType}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetConfigurationsByProvider(string providerType)
    {
        try
        {
            if (!Enum.TryParse<ConduitLLM.Functions.Enums.FunctionProviderType>(providerType, true, out var providerEnum))
            {
                return BadRequest(new ErrorResponseDto($"Invalid provider type: {providerType}"));
            }

            var configurations = await _configurationRepository.GetByProviderTypeAsync(providerEnum);
            return Ok(configurations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting function configurations for provider {ProviderType}", providerType);
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }

    /// <summary>
    /// Gets function configurations by purpose.
    /// </summary>
    /// <param name="purpose">The purpose (e.g., "Search", "Answer", "Enrich")</param>
    /// <returns>List of function configurations for the specified purpose</returns>
    [HttpGet("purpose/{purpose}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetConfigurationsByPurpose(string purpose)
    {
        try
        {
            if (!Enum.TryParse<ConduitLLM.Functions.Enums.FunctionPurpose>(purpose, true, out var purposeEnum))
            {
                return BadRequest(new ErrorResponseDto($"Invalid purpose: {purpose}"));
            }

            var configurations = await _configurationRepository.GetByPurposeAsync(purposeEnum);
            return Ok(configurations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting function configurations for purpose {Purpose}", purpose);
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }

    /// <summary>
    /// Creates a new function configuration.
    /// </summary>
    /// <param name="configuration">The function configuration to create</param>
    /// <returns>The created function configuration</returns>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateConfiguration(
        [FromBody] ConduitLLM.Functions.Entities.FunctionConfiguration configuration)
    {
        try
        {
            if (configuration == null)
            {
                return BadRequest(new ErrorResponseDto("Function configuration data is required"));
            }

            int id = await _configurationRepository.CreateAsync(configuration);

            // Fetch the created entity to return
            var created = await _configurationRepository.GetByIdAsync(id);

            return CreatedAtAction(
                nameof(GetConfigurationById),
                new { id },
                created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating function configuration");
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }

    /// <summary>
    /// Updates an existing function configuration.
    /// </summary>
    /// <param name="id">The ID of the function configuration to update</param>
    /// <param name="configuration">The updated function configuration data</param>
    /// <returns>The updated function configuration</returns>
    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateConfiguration(
        int id,
        [FromBody] ConduitLLM.Functions.Entities.FunctionConfiguration configuration)
    {
        try
        {
            if (configuration == null)
            {
                return BadRequest(new ErrorResponseDto("Function configuration data is required"));
            }

            if (id != configuration.Id)
            {
                return BadRequest(new ErrorResponseDto("ID mismatch"));
            }

            await _configurationRepository.UpdateAsync(configuration);

            // Fetch the updated entity to return
            var updated = await _configurationRepository.GetByIdAsync(id);

            if (updated == null)
            {
                return NotFound(new ErrorResponseDto("Function configuration not found"));
            }

            return Ok(updated);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating function configuration with ID {Id}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }

    /// <summary>
    /// Deletes a function configuration.
    /// </summary>
    /// <param name="id">The ID of the function configuration to delete</param>
    /// <returns>No content on success</returns>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteConfiguration(int id)
    {
        try
        {
            await _configurationRepository.DeleteAsync(id);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting function configuration with ID {Id}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }
}
