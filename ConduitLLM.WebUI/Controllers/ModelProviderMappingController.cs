using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Repositories;

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
        private readonly IProviderCredentialRepository _credentialRepository;
        private readonly ILogger<ModelProviderMappingController> _logger;

        /// <summary>
        /// Initializes a new instance of the ModelProviderMappingController.
        /// </summary>
        /// <param name="mappingService">The model provider mapping service.</param>
        /// <param name="credentialRepository">The provider credential repository.</param>
        /// <param name="logger">The logger.</param>
        public ModelProviderMappingController(
            IModelProviderMappingService mappingService,
            IProviderCredentialRepository credentialRepository,
            ILogger<ModelProviderMappingController> logger)
        {
            _mappingService = mappingService ?? throw new ArgumentNullException(nameof(mappingService));
            _credentialRepository = credentialRepository ?? throw new ArgumentNullException(nameof(credentialRepository));
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
                    _logger.LogWarning("Model provider mapping not found {MappingId}", S(id));
                    return NotFound(new { error = "Model provider mapping not found", id = id });
                }

                return Ok(mapping);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting model provider mapping with ID {Id}", S(id));
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
                    _logger.LogWarning("No mapping found for model alias {ModelAlias}", S(modelAlias));
                    return NotFound(new { error = "No mapping found for model alias", modelAlias = modelAlias });
                }

                return Ok(mapping);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting model provider mapping for alias {ModelAlias}", S(modelAlias));
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
                // Validate that the provider exists
                var provider = await _credentialRepository.GetByProviderNameAsync(mapping.ProviderName);
                if (provider == null)
                {
                    _logger.LogWarning("Provider does not exist {ProviderName}", S(mapping.ProviderName));
                    return BadRequest(new { error = "Provider does not exist", provider = mapping.ProviderName });
                }

                // Check if a mapping with the same alias already exists
                var existingMapping = await _mappingService.GetMappingByModelAliasAsync(mapping.ModelAlias);
                if (existingMapping != null)
                {
                    _logger.LogWarning("Mapping already exists for model alias {ModelAlias}", S(mapping.ModelAlias));
                    return BadRequest(new { error = "A mapping for this model alias already exists", modelAlias = mapping.ModelAlias });
                }

                await _mappingService.AddMappingAsync(mapping);

                // Return the created mapping
                var createdMapping = await _mappingService.GetMappingByModelAliasAsync(mapping.ModelAlias);
                return CreatedAtAction(nameof(GetMappingByAlias), new { modelAlias = mapping.ModelAlias }, createdMapping);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating model provider mapping for alias {ModelAlias}", S(mapping.ModelAlias));
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
                // Check if the mapping exists
                var existingMapping = await _mappingService.GetMappingByIdAsync(id);
                if (existingMapping == null)
                {
                    _logger.LogWarning("Model provider mapping not found for update {MappingId}", S(id));
                    return NotFound(new { error = "Model provider mapping not found", id = id });
                }

                // Validate that the provider exists
                var provider = await _credentialRepository.GetByProviderNameAsync(mapping.ProviderName);
                if (provider == null)
                {
                    _logger.LogWarning("Provider does not exist {ProviderName}", S(mapping.ProviderName));
                    return BadRequest(new { error = "Provider does not exist", provider = mapping.ProviderName });
                }

                // Update the mapping
                await _mappingService.UpdateMappingAsync(mapping);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating model provider mapping with ID {Id}", S(id));
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
                    _logger.LogWarning("Model provider mapping not found for deletion {MappingId}", S(id));
                    return NotFound(new { error = "Model provider mapping not found", id = id });
                }

                await _mappingService.DeleteMappingAsync(id);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting model provider mapping with ID {Id}", S(id));
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
                var providers = await _credentialRepository.GetAllAsync();
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
