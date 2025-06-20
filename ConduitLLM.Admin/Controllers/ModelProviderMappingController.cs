using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;

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
    private readonly ILogger<ModelProviderMappingController> _logger;

    /// <summary>
    /// Initializes a new instance of the ModelProviderMappingController
    /// </summary>
    /// <param name="mappingService">The model provider mapping service</param>
    /// <param name="discoveryService">The provider discovery service</param>
    /// <param name="logger">The logger</param>
    public ModelProviderMappingController(
        IAdminModelProviderMappingService mappingService,
        IProviderDiscoveryService discoveryService,
        ILogger<ModelProviderMappingController> logger)
    {
        _mappingService = mappingService ?? throw new ArgumentNullException(nameof(mappingService));
        _discoveryService = discoveryService ?? throw new ArgumentNullException(nameof(discoveryService));
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
                return NotFound("Model provider mapping not found");
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
                return NotFound("Model provider mapping not found");
            }

            return Ok(mapping);
        }
        catch (Exception ex)
        {
_logger.LogError(ex, "Error getting model provider mapping for model ID {ModelId}", modelId.Replace(Environment.NewLine, ""));
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
_logger.LogError(ex, "Error creating model provider mapping for model ID {ModelId}", mappingDto.ModelId.Replace(Environment.NewLine, ""));
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
                return NotFound("Model provider mapping not found");
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
                return NotFound("Model provider mapping not found");
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

    /// <summary>
    /// Creates multiple model provider mappings in a single operation
    /// </summary>
    /// <param name="request">The bulk mapping request containing mappings to create</param>
    /// <returns>The bulk mapping response with results and errors</returns>
    [HttpPost("bulk")]
    [Authorize(Policy = "MasterKeyPolicy")]
    [ProducesResponseType(typeof(BulkModelMappingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateBulkMappings([FromBody] BulkModelMappingRequest request)
    {
        if (request == null)
        {
            return BadRequest("Bulk mapping request cannot be null");
        }

        if (request.Mappings == null || !request.Mappings.Any())
        {
            return BadRequest("At least one mapping must be provided");
        }

        try
        {
            var response = await _mappingService.CreateBulkMappingsAsync(request);
            
            // Return 200 OK even if some mappings failed - the response contains success/failure details
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating bulk model provider mappings");
            
            // Return a response indicating complete failure
            var errorResponse = new BulkModelMappingResponse
            {
                TotalProcessed = request.Mappings.Count,
                Failed = request.Mappings.Select((mapping, index) => new BulkMappingError
                {
                    Index = index,
                    Mapping = mapping,
                    ErrorMessage = $"System error: {ex.Message}",
                    ErrorType = BulkMappingErrorType.SystemError
                }).ToList()
            };

            return StatusCode(StatusCodes.Status500InternalServerError, errorResponse);
        }
    }

    /// <summary>
    /// Discovers available models for a specific provider
    /// </summary>
    /// <param name="providerName">The name of the provider to discover models for</param>
    /// <returns>A list of discovered models with their capabilities</returns>
    [HttpGet("discover/provider/{providerName}")]
    [ProducesResponseType(typeof(IEnumerable<DiscoveredModel>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DiscoverProviderModels(string providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            return BadRequest("Provider name cannot be empty");
        }

        try
        {
            _logger.LogInformation("Discovering models for provider: {Provider}", providerName);
            var models = await _discoveryService.DiscoverProviderModelsAsync(providerName);
            
            // Convert dictionary values to list
            return Ok(models.Values);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discovering models for provider {Provider}", providerName);
            return StatusCode(StatusCodes.Status500InternalServerError, $"An error occurred while discovering models: {ex.Message}");
        }
    }

    /// <summary>
    /// Discovers capabilities for a specific model
    /// </summary>
    /// <param name="providerName">The name of the provider</param>
    /// <param name="modelId">The model ID to check capabilities for</param>
    /// <returns>Model information with detailed capabilities</returns>
    [HttpGet("discover/model/{providerName}/{modelId}")]
    [ProducesResponseType(typeof(DiscoveredModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DiscoverModelCapabilities(string providerName, string modelId)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            return BadRequest("Provider name cannot be empty");
        }

        if (string.IsNullOrWhiteSpace(modelId))
        {
            return BadRequest("Model ID cannot be empty");
        }

        try
        {
            _logger.LogInformation("Discovering capabilities for model {ModelId} from provider {Provider}", modelId, providerName);
            
            // Discover all models for the provider
            var models = await _discoveryService.DiscoverProviderModelsAsync(providerName);
            
            // Find the specific model by key or value
            DiscoveredModel? model = null;
            
            // First try to find by dictionary key
            if (models.TryGetValue(modelId, out var modelByKey))
            {
                model = modelByKey;
            }
            else
            {
                // Then try to find by ModelId property
                model = models.Values.FirstOrDefault(m => m.ModelId.Equals(modelId, StringComparison.OrdinalIgnoreCase));
            }
            
            if (model == null)
            {
                return NotFound($"Model {modelId} not found for provider {providerName}");
            }
            
            return Ok(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discovering capabilities for model {ModelId} from provider {Provider}", modelId, providerName);
            return StatusCode(StatusCodes.Status500InternalServerError, $"An error occurred while discovering model capabilities: {ex.Message}");
        }
    }

    /// <summary>
    /// Tests a specific capability for a model
    /// </summary>
    /// <param name="modelAlias">The model alias to test</param>
    /// <param name="capability">The capability to test (e.g., "ImageGeneration", "Vision", "ChatStream")</param>
    /// <returns>Whether the model supports the capability</returns>
    [HttpGet("discover/capability/{modelAlias}/{capability}")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> TestModelCapability(string modelAlias, string capability)
    {
        if (string.IsNullOrWhiteSpace(modelAlias))
        {
            return BadRequest("Model alias cannot be empty");
        }

        if (string.IsNullOrWhiteSpace(capability))
        {
            return BadRequest("Capability cannot be empty");
        }

        try
        {
            // Parse the capability string to ModelCapability enum
            if (!Enum.TryParse<ModelCapability>(capability, true, out var modelCapability))
            {
                return BadRequest($"Invalid capability: {capability}. Valid values are: {string.Join(", ", Enum.GetNames(typeof(ModelCapability)))}");
            }

            _logger.LogInformation("Testing capability {Capability} for model {ModelAlias}", capability, modelAlias);
            
            var supportsCapability = await _discoveryService.TestModelCapabilityAsync(modelAlias, modelCapability);
            
            return Ok(supportsCapability);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing capability {Capability} for model {ModelAlias}", capability, modelAlias);
            return StatusCode(StatusCodes.Status500InternalServerError, $"An error occurred while testing model capability: {ex.Message}");
        }
    }
}
