using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Extensions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ModelProviderMapping = ConduitLLM.Configuration.Entities.ModelProviderMapping;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

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
    private readonly IProviderDiscoveryService _discoveryService;
    private readonly IProviderService _providerService;
    private readonly ILogger<ModelProviderMappingController> _logger;

    /// <summary>
    /// Initializes a new instance of the ModelProviderMappingController
    /// </summary>
    /// <param name="mappingService">The model provider mapping service</param>
    /// <param name="discoveryService">The provider discovery service</param>
    /// <param name="providerService">The provider service</param>
    /// <param name="logger">The logger</param>
    public ModelProviderMappingController(
        IAdminModelProviderMappingService mappingService,
        IProviderDiscoveryService discoveryService,
        IProviderService providerService,
        ILogger<ModelProviderMappingController> logger)
    {
        _mappingService = mappingService ?? throw new ArgumentNullException(nameof(mappingService));
        _discoveryService = discoveryService ?? throw new ArgumentNullException(nameof(discoveryService));
        _providerService = providerService ?? throw new ArgumentNullException(nameof(providerService));
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
            var dtos = mappings.Select(m => m.ToDto());
            return Ok(dtos);
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
                return NotFound(new { error = "Model provider mapping not found" });
            }

            return Ok(mapping.ToDto());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting model provider mapping with ID {Id}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving the model provider mapping");
        }
    }

    /// <summary>
    /// Creates a new model provider mapping
    /// </summary>
    /// <param name="mappingDto">The mapping to create</param>
    /// <returns>The created mapping</returns>
    [HttpPost]
    [ProducesResponseType(typeof(ModelProviderMappingDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateMapping([FromBody] ModelProviderMappingDto mappingDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Check if a mapping with the same model ID already exists
            var existingMapping = await _mappingService.GetMappingByModelIdAsync(mappingDto.ModelId);
            if (existingMapping != null)
            {
                return Conflict(new { error = $"A mapping for model ID '{mappingDto.ModelId}' already exists" });
            }

            var mapping = mappingDto.ToEntity();
            var success = await _mappingService.AddMappingAsync(mapping);

            if (!success)
            {
                return BadRequest(new { error = "Failed to create model provider mapping. Please check the provider ID." });
            }

            var createdMapping = await _mappingService.GetMappingByModelIdAsync(mappingDto.ModelId);
            return CreatedAtAction(nameof(GetMappingById), new { id = createdMapping?.Id }, createdMapping?.ToDto());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating model provider mapping");
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while creating the model provider mapping");
        }
    }

    /// <summary>
    /// Updates an existing model provider mapping
    /// </summary>
    /// <param name="id">The ID of the mapping to update</param>
    /// <param name="mappingDto">The updated mapping data</param>
    /// <returns>No content on success</returns>
    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateMapping(int id, [FromBody] ModelProviderMappingDto mappingDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (id != mappingDto.Id)
            {
                return BadRequest(new { error = "ID mismatch" });
            }

            var existingMapping = await _mappingService.GetMappingByIdAsync(id);
            if (existingMapping == null)
            {
                return NotFound(new { error = "Model provider mapping not found" });
            }

            existingMapping.UpdateFromDto(mappingDto);
            var success = await _mappingService.UpdateMappingAsync(existingMapping);

            if (!success)
            {
                return BadRequest(new { error = "Failed to update model provider mapping" });
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
    /// <returns>No content on success</returns>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteMapping(int id)
    {
        try
        {
            var existingMapping = await _mappingService.GetMappingByIdAsync(id);
            if (existingMapping == null)
            {
                return NotFound(new { error = "Model provider mapping not found" });
            }

            var success = await _mappingService.DeleteMappingAsync(id);

            if (!success)
            {
                return BadRequest(new { error = "Failed to delete model provider mapping" });
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
    /// Gets all available providers
    /// </summary>
    /// <returns>List of providers with IDs and names</returns>
    [HttpGet("providers")]
    [ProducesResponseType(typeof(IEnumerable<Provider>), StatusCodes.Status200OK)]
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

    /// <summary>
    /// Creates multiple model provider mappings in a single operation
    /// </summary>
    /// <param name="mappingDtos">The mappings to create</param>
    /// <returns>The bulk mapping response with results</returns>
    [HttpPost("bulk")]
    [ProducesResponseType(typeof(BulkMappingResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateBulkMappings([FromBody] List<ModelProviderMappingDto> mappingDtos)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (mappingDtos == null || mappingDtos.Count == 0)
            {
                return BadRequest(new { error = "No mappings provided" });
            }

            var mappings = mappingDtos.Select(dto => dto.ToEntity()).ToList();
            var (created, errors) = await _mappingService.CreateBulkMappingsAsync(mappings);

            var result = new BulkMappingResult
            {
                Created = created.Select(m => m.ToDto()).ToList(),
                Errors = errors.ToList(),
                TotalProcessed = mappingDtos.Count,
                SuccessCount = created.Count(),
                FailureCount = errors.Count()
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating bulk model provider mappings");
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while creating bulk model provider mappings");
        }
    }

    /// <summary>
    /// Discovers available models from a specific provider
    /// </summary>
    /// <param name="providerId">The provider ID to discover models from</param>
    /// <returns>List of discovered models</returns>
    [HttpGet("discover/{providerId}")]
    [ProducesResponseType(typeof(IEnumerable<DiscoveredModel>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DiscoverModels(int providerId)
    {
        try
        {
            var provider = await _providerService.GetProviderByIdAsync(providerId);
            if (provider == null)
            {
                return NotFound(new { error = "Provider not found" });
            }

            // Discover models for the provider
            var discoveredModels = await _discoveryService.DiscoverProviderModelsAsync(provider);
            return Ok(discoveredModels);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discovering models for provider {ProviderId}", providerId);
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while discovering models");
        }
    }
}

/// <summary>
/// Result of a bulk mapping operation
/// </summary>
public class BulkMappingResult
{
    /// <summary>
    /// Successfully created mappings
    /// </summary>
    public List<ModelProviderMappingDto> Created { get; set; } = new();

    /// <summary>
    /// Error messages for failed mappings
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Total number of mappings processed
    /// </summary>
    public int TotalProcessed { get; set; }

    /// <summary>
    /// Number of successful mappings
    /// </summary>
    public int SuccessCount { get; set; }

    /// <summary>
    /// Number of failed mappings
    /// </summary>
    public int FailureCount { get; set; }
}