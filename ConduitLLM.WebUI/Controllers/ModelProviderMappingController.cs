using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ConduitLLM.Configuration;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using static ConduitLLM.Core.Extensions.LoggingSanitizer;

namespace ConduitLLM.WebUI.Controllers
{
    /// <summary>
    /// Controller for managing model provider mappings using the repository pattern.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This controller provides endpoints for managing the mappings between model aliases
    /// and actual provider model implementations. These mappings are critical for the Conduit
    /// routing system to function properly.
    /// </para>
    /// <para>
    /// Model provider mappings define how user-friendly model aliases (e.g., "gpt-4") map to
    /// actual provider-specific implementations (e.g., "gpt-4-turbo-preview" on OpenAI).
    /// </para>
    /// </remarks>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = "MasterKeyPolicy")]
    public class ModelProviderMappingController : ControllerBase
    {
        private readonly IModelProviderMappingService _mappingService;
        private readonly ILogger<ModelProviderMappingController> _logger;

        /// <summary>
        /// Initializes a new instance of the ModelProviderMappingController.
        /// </summary>
        /// <param name="mappingService">The model provider mapping service.</param>
        /// <param name="logger">The logger.</param>
        public ModelProviderMappingController(
            IModelProviderMappingService mappingService,
            ILogger<ModelProviderMappingController> logger)
        {
            _mappingService = mappingService ?? throw new ArgumentNullException(nameof(mappingService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets all model provider mappings.
        /// </summary>
        /// <returns>A list of all model provider mappings.</returns>
        /// <response code="200">Returns the list of mappings</response>
        /// <response code="500">If an error occurs</response>
        [HttpGet]
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
                return StatusCode(500, "An error occurred while retrieving model provider mappings");
            }
        }

        /// <summary>
        /// Gets a specific model provider mapping by ID.
        /// </summary>
        /// <param name="id">The ID of the mapping to retrieve.</param>
        /// <returns>The model provider mapping.</returns>
        /// <response code="200">Returns the mapping</response>
        /// <response code="404">If the mapping is not found</response>
        /// <response code="500">If an error occurs</response>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetMappingById(int id)
        {
            try
            {
                var mapping = await _mappingService.GetMappingByIdAsync(id);

                if (mapping == null)
                {
                    _logger.LogWarning("Model provider mapping not found {MappingId}", id);
                    return NotFound(new { error = "Model provider mapping not found", id = id });
                }

                return Ok(mapping);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting model provider mapping with ID {Id}", id);
                return StatusCode(500, "An error occurred while retrieving the model provider mapping");
            }
        }

        /// <summary>
        /// Gets a model provider mapping by model alias.
        /// </summary>
        /// <param name="modelAlias">The model alias to look up.</param>
        /// <returns>The model provider mapping if found.</returns>
        /// <response code="200">Returns the mapping</response>
        /// <response code="404">If no mapping is found for the model alias</response>
        /// <response code="500">If an error occurs</response>
        [HttpGet("by-alias/{modelAlias}")]
        public async Task<IActionResult> GetMappingByAlias(string modelAlias)
        {
            try
            {
                var mapping = await _mappingService.GetMappingByModelAliasAsync(modelAlias);

                if (mapping == null)
                {
                    _logger.LogWarning("No mapping found for model alias {ModelAlias}", modelAlias.Replace(Environment.NewLine, ""));
                    return NotFound(new { error = "No mapping found for model alias", modelAlias = modelAlias });
                }

                return Ok(mapping);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting model provider mapping for alias {ModelAlias}", modelAlias.Replace(Environment.NewLine, ""));
                return StatusCode(500, "An error occurred while retrieving the model provider mapping");
            }
        }

        /// <summary>
        /// Creates a new model provider mapping.
        /// </summary>
        /// <param name="mapping">The mapping to create.</param>
        /// <returns>The created mapping.</returns>
        /// <response code="201">Returns the newly created mapping</response>
        /// <response code="400">If the request is invalid</response>
        /// <response code="500">If an error occurs</response>
        [HttpPost]
        public async Task<IActionResult> CreateMapping([FromBody] ModelProviderMapping mapping)
        {
            if (mapping == null)
            {
                return BadRequest("Mapping cannot be null");
            }

            try
            {
                var (success, errorMessage, createdMapping) = await _mappingService.ValidateAndCreateMappingAsync(mapping);
                
                if (!success)
                {
                    return BadRequest(new { error = errorMessage });
                }

                return CreatedAtAction(nameof(GetMappingByAlias), new { modelAlias = mapping.ModelAlias }, createdMapping);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating model provider mapping for alias {ModelAlias}", mapping.ModelAlias.Replace(Environment.NewLine, ""));
                return StatusCode(500, "An error occurred while creating the model provider mapping");
            }
        }

        /// <summary>
        /// Updates an existing model provider mapping.
        /// </summary>
        /// <param name="id">The ID of the mapping to update.</param>
        /// <param name="mapping">The updated mapping data.</param>
        /// <returns>No content if successful.</returns>
        /// <response code="204">If the update is successful</response>
        /// <response code="400">If the request is invalid</response>
        /// <response code="404">If the mapping is not found</response>
        /// <response code="500">If an error occurs</response>
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateMapping(int id, [FromBody] ModelProviderMapping mapping)
        {
            if (mapping == null)
            {
                return BadRequest("Mapping cannot be null");
            }

            try
            {
                var (success, errorMessage) = await _mappingService.ValidateAndUpdateMappingAsync(id, mapping);
                
                if (!success)
                {
                    if (errorMessage?.Contains("not found") == true)
                    {
                        return NotFound(new { error = errorMessage, id = id });
                    }
                    return BadRequest(new { error = errorMessage });
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating model provider mapping with ID {Id}", id);
                return StatusCode(500, "An error occurred while updating the model provider mapping");
            }
        }

        /// <summary>
        /// Deletes a model provider mapping.
        /// </summary>
        /// <param name="id">The ID of the mapping to delete.</param>
        /// <returns>No content if successful.</returns>
        /// <response code="204">If the deletion is successful</response>
        /// <response code="404">If the mapping is not found</response>
        /// <response code="500">If an error occurs</response>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteMapping(int id)
        {
            try
            {
                // Check if the mapping exists
                var existingMapping = await _mappingService.GetMappingByIdAsync(id);
                if (existingMapping == null)
                {
                    _logger.LogWarning("Model provider mapping not found for deletion {MappingId}", id);
                    return NotFound(new { error = "Model provider mapping not found", id = id });
                }

                await _mappingService.DeleteMappingAsync(id);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting model provider mapping with ID {Id}", id);
                return StatusCode(500, "An error occurred while deleting the model provider mapping");
            }
        }

        /// <summary>
        /// Gets a list of all available providers.
        /// </summary>
        /// <returns>A list of provider names.</returns>
        /// <response code="200">Returns the list of providers</response>
        /// <response code="500">If an error occurs</response>
        [HttpGet("providers")]
        public async Task<IActionResult> GetProviders()
        {
            try
            {
                var providers = await _mappingService.GetAvailableProvidersAsync();
                return Ok(providers.Select(p => new { p.Id, p.ProviderName }).ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting providers");
                return StatusCode(500, "An error occurred while retrieving providers");
            }
        }
    }
}
