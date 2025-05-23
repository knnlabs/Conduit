using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ConduitLLM.Admin.Controllers;

/// <summary>
/// Controller for managing model provider mappings
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "MasterKeyPolicy")]
public class ModelProviderMappingController : ControllerBase
{
    private readonly IAdminModelProviderMappingService _mappingService;
    private readonly ILogger<ModelProviderMappingController> _logger;
    
    /// <summary>
    /// Initializes a new instance of the ModelProviderMappingController
    /// </summary>
    /// <param name="mappingService">The model provider mapping service</param>
    /// <param name="logger">The logger</param>
    public ModelProviderMappingController(
        IAdminModelProviderMappingService mappingService,
        ILogger<ModelProviderMappingController> logger)
    {
        _mappingService = mappingService ?? throw new ArgumentNullException(nameof(mappingService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    /// <summary>
    /// Gets all model provider mappings
    /// </summary>
    /// <returns>A list of all model provider mappings</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ModelProviderMappingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetAllMappings()
    {
        try
        {
            var mappings = await _mappingService.GetAllMappingsAsync();
            return Ok(mappings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all model provider mappings");
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving model provider mappings");
        }
    }
    
    /// <summary>
    /// Gets a specific model provider mapping by ID
    /// </summary>
    /// <param name="id">The ID of the mapping to retrieve</param>
    /// <returns>The model provider mapping</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ModelProviderMappingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetMappingById(int id)
    {
        try
        {
            var mapping = await _mappingService.GetMappingByIdAsync(id);
            
            if (mapping == null)
            {
                return NotFound($"Model provider mapping with ID {id} not found");
            }
            
            return Ok(mapping);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting model provider mapping with ID {Id}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving the model provider mapping");
        }
    }
    
    /// <summary>
    /// Gets a model provider mapping by model ID
    /// </summary>
    /// <param name="modelId">The model ID to look up</param>
    /// <returns>The model provider mapping if found</returns>
    [HttpGet("by-model/{modelId}")]
    [ProducesResponseType(typeof(ModelProviderMappingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetMappingByModelId(string modelId)
    {
        try
        {
            var mapping = await _mappingService.GetMappingByModelIdAsync(modelId);

            if (mapping == null)
            {
                return NotFound($"No mapping found for model ID '{modelId}'");
            }

            return Ok(mapping);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting model provider mapping for model ID {ModelId}", modelId);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving the model provider mapping");
        }
    }
    
    /// <summary>
    /// Creates a new model provider mapping
    /// </summary>
    /// <param name="mappingDto">The mapping to create</param>
    /// <returns>The created mapping</returns>
    [HttpPost]
    [Authorize(Policy = "MasterKeyPolicy")]
    [ProducesResponseType(typeof(ModelProviderMappingDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateMapping([FromBody] ModelProviderMappingDto mappingDto)
    {
        if (mappingDto == null)
        {
            return BadRequest("Mapping cannot be null");
        }

        try
        {
            bool success = await _mappingService.AddMappingAsync(mappingDto);

            if (!success)
            {
                return BadRequest("Failed to create model provider mapping. Verify that the provider exists and the model ID is unique.");
            }

            // Get the created mapping
            var createdMapping = await _mappingService.GetMappingByModelIdAsync(mappingDto.ModelId);
            return CreatedAtAction(nameof(GetMappingByModelId), new { modelId = mappingDto.ModelId }, createdMapping);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating model provider mapping for model ID {ModelId}", mappingDto.ModelId);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while creating the model provider mapping");
        }
    }
    
    /// <summary>
    /// Updates an existing model provider mapping
    /// </summary>
    /// <param name="id">The ID of the mapping to update</param>
    /// <param name="mappingDto">The updated mapping data</param>
    /// <returns>No content if successful</returns>
    [HttpPut("{id}")]
    [Authorize(Policy = "MasterKeyPolicy")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateMapping(int id, [FromBody] ModelProviderMappingDto mappingDto)
    {
        if (mappingDto == null)
        {
            return BadRequest("Mapping cannot be null");
        }

        // Ensure ID in route matches ID in body
        if (id != mappingDto.Id)
        {
            return BadRequest("ID in route must match ID in body");
        }

        try
        {
            bool success = await _mappingService.UpdateMappingAsync(mappingDto);

            if (!success)
            {
                return NotFound($"Model provider mapping with ID {id} not found or provider doesn't exist");
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating model provider mapping with ID {Id}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while updating the model provider mapping");
        }
    }
    
    /// <summary>
    /// Deletes a model provider mapping
    /// </summary>
    /// <param name="id">The ID of the mapping to delete</param>
    /// <returns>No content if successful</returns>
    [HttpDelete("{id}")]
    [Authorize(Policy = "MasterKeyPolicy")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteMapping(int id)
    {
        try
        {
            bool success = await _mappingService.DeleteMappingAsync(id);
            
            if (!success)
            {
                return NotFound($"Model provider mapping with ID {id} not found");
            }
            
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting model provider mapping with ID {Id}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while deleting the model provider mapping");
        }
    }
    
    /// <summary>
    /// Gets a list of all available providers
    /// </summary>
    /// <returns>A list of provider names</returns>
    [HttpGet("providers")]
    [ProducesResponseType(typeof(IEnumerable<ProviderDataDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetProviders()
    {
        try
        {
            var providers = await _mappingService.GetProvidersAsync();
            return Ok(providers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting providers");
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving providers");
        }
    }
}