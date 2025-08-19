using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Admin.Controllers
{
    /// <summary>
    /// Controller for managing canonical Model entities
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "MasterKeyPolicy")]
    public class ModelController : ControllerBase
    {
        private readonly IModelRepository _modelRepository;
        private readonly ILogger<ModelController> _logger;

        /// <summary>
        /// Initializes a new instance of the ModelController
        /// </summary>
        public ModelController(
            IModelRepository modelRepository,
            ILogger<ModelController> logger)
        {
            _modelRepository = modelRepository ?? throw new ArgumentNullException(nameof(modelRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets all models with their capabilities
        /// </summary>
        /// <returns>List of all models</returns>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<ModelDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetAllModels()
        {
            try
            {
                var models = await _modelRepository.GetAllWithDetailsAsync();
                var dtos = models.Select(m => MapToDto(m));
                return Ok(dtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all models");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving models");
            }
        }

        /// <summary>
        /// Gets a specific model by ID
        /// </summary>
        /// <param name="id">The model ID</param>
        /// <returns>The model with its capabilities</returns>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ModelDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetModelById(int id)
        {
            try
            {
                var model = await _modelRepository.GetByIdWithDetailsAsync(id);
                if (model == null)
                {
                    return NotFound($"Model with ID {id} not found");
                }

                return Ok(MapToDto(model));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting model with ID {Id}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving the model");
            }
        }

        /// <summary>
        /// Gets models by type
        /// </summary>
        /// <param name="type">The model type (Text, Image, Audio, Video, Embedding)</param>
        /// <returns>List of models of the specified type</returns>
        [HttpGet("type/{type}")]
        [ProducesResponseType(typeof(IEnumerable<ModelDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetModelsByType(string type)
        {
            try
            {
                if (!Enum.TryParse<ModelType>(type, true, out var modelType))
                {
                    return BadRequest($"Invalid model type: {type}");
                }

                var models = await _modelRepository.GetByTypeAsync(modelType);
                var dtos = models.Select(m => MapToDto(m));
                return Ok(dtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting models by type {Type}", type);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving models");
            }
        }

        /// <summary>
        /// Searches for models by name
        /// </summary>
        /// <param name="query">The search query</param>
        /// <returns>List of matching models</returns>
        [HttpGet("search")]
        [ProducesResponseType(typeof(IEnumerable<ModelDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> SearchModels([FromQuery] string query)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query))
                {
                    return Ok(new List<ModelDto>());
                }

                var models = await _modelRepository.SearchByNameAsync(query);
                var dtos = models.Select(m => MapToDto(m));
                return Ok(dtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching models with query {Query}", query);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while searching models");
            }
        }

        /// <summary>
        /// Gets models available from a specific provider
        /// </summary>
        /// <param name="provider">The provider name (e.g., "groq", "openai", "anthropic")</param>
        /// <returns>List of models available from the provider</returns>
        [HttpGet("provider/{provider}")]
        [ProducesResponseType(typeof(IEnumerable<ModelWithProviderIdDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetModelsByProvider(string provider)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(provider))
                {
                    return BadRequest("Provider name is required");
                }

                var models = await _modelRepository.GetByProviderAsync(provider);
                var dtos = models.Select(m => 
                {
                    // Find the identifier for this specific provider
                    var providerIdentifier = m.Identifiers?.FirstOrDefault(i => 
                        string.Equals(i.Provider, provider, StringComparison.OrdinalIgnoreCase))?.Identifier 
                        ?? m.Name; // Fallback to model name if no specific identifier

                    return new ModelWithProviderIdDto
                    {
                        Id = m.Id,
                        Name = m.Name,
                        ProviderModelId = providerIdentifier,
                        ModelType = m.ModelType,
                        ModelSeriesId = m.ModelSeriesId,
                        ModelCapabilitiesId = m.ModelCapabilitiesId,
                        Capabilities = m.Capabilities != null ? MapCapabilitiesToDto(m.Capabilities) : null,
                        IsActive = m.IsActive,
                        CreatedAt = m.CreatedAt,
                        UpdatedAt = m.UpdatedAt
                    };
                });
                return Ok(dtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting models for provider {Provider}", provider);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving models");
            }
        }

        /// <summary>
        /// Creates a new model
        /// </summary>
        /// <param name="dto">The model to create</param>
        /// <returns>The created model</returns>
        [HttpPost]
        [ProducesResponseType(typeof(ModelDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateModel([FromBody] CreateModelDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Check if a model with the same name already exists
                var existing = await _modelRepository.GetByNameAsync(dto.Name);
                if (existing != null)
                {
                    return Conflict($"A model with name '{dto.Name}' already exists");
                }

                var model = new Model
                {
                    Name = dto.Name,
                    ModelType = dto.ModelType,
                    ModelSeriesId = dto.ModelSeriesId,
                    ModelCapabilitiesId = dto.ModelCapabilitiesId,
                    IsActive = dto.IsActive ?? true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await _modelRepository.CreateAsync(model);

                // Reload with capabilities
                model = await _modelRepository.GetByIdWithDetailsAsync(model.Id);
                if (model == null)
                {
                    return StatusCode(StatusCodes.Status500InternalServerError, "Failed to reload created model");
                }

                return CreatedAtAction(
                    nameof(GetModelById),
                    new { id = model.Id },
                    MapToDto(model));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating model");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while creating the model");
            }
        }

        /// <summary>
        /// Updates an existing model
        /// </summary>
        /// <param name="id">The model ID</param>
        /// <param name="dto">The updated model data</param>
        /// <returns>No content on success</returns>
        [HttpPut("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateModel(int id, [FromBody] UpdateModelDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                if (id != dto.Id)
                {
                    return BadRequest("ID mismatch");
                }

                var model = await _modelRepository.GetByIdAsync(id);
                if (model == null)
                {
                    return NotFound($"Model with ID {id} not found");
                }

                // Check for name conflicts if name is being changed
                if (!string.IsNullOrEmpty(dto.Name) && dto.Name != model.Name)
                {
                    var existing = await _modelRepository.GetByNameAsync(dto.Name);
                    if (existing != null && existing.Id != id)
                    {
                        return Conflict($"A model with name '{dto.Name}' already exists");
                    }
                    model.Name = dto.Name;
                }

                if (dto.ModelType.HasValue)
                    model.ModelType = dto.ModelType.Value;
                if (dto.ModelSeriesId.HasValue)
                    model.ModelSeriesId = dto.ModelSeriesId.Value;
                if (dto.ModelCapabilitiesId.HasValue)
                    model.ModelCapabilitiesId = dto.ModelCapabilitiesId.Value;
                if (dto.IsActive.HasValue)
                    model.IsActive = dto.IsActive.Value;

                model.UpdatedAt = DateTime.UtcNow;

                await _modelRepository.UpdateAsync(model);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating model with ID {Id}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while updating the model");
            }
        }

        /// <summary>
        /// Deletes a model
        /// </summary>
        /// <param name="id">The model ID</param>
        /// <returns>No content on success</returns>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteModel(int id)
        {
            try
            {
                var model = await _modelRepository.GetByIdAsync(id);
                if (model == null)
                {
                    return NotFound($"Model with ID {id} not found");
                }

                // Check if model is referenced by any mappings
                var hasReferences = await _modelRepository.HasMappingReferencesAsync(id);
                if (hasReferences)
                {
                    return Conflict("Cannot delete model that is referenced by model provider mappings");
                }

                await _modelRepository.DeleteAsync(id);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting model with ID {Id}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while deleting the model");
            }
        }

        private static ModelDto MapToDto(Model model)
        {
            return new ModelDto
            {
                Id = model.Id,
                Name = model.Name,
                ModelType = model.ModelType,
                ModelSeriesId = model.ModelSeriesId,
                ModelCapabilitiesId = model.ModelCapabilitiesId,
                Capabilities = model.Capabilities != null ? MapCapabilitiesToDto(model.Capabilities) : null,
                IsActive = model.IsActive,
                CreatedAt = model.CreatedAt,
                UpdatedAt = model.UpdatedAt
            };
        }

        private static ModelCapabilitiesDto MapCapabilitiesToDto(ModelCapabilities capabilities)
        {
            return new ModelCapabilitiesDto
            {
                Id = capabilities.Id,
                SupportsChat = capabilities.SupportsChat,
                SupportsVision = capabilities.SupportsVision,
                SupportsFunctionCalling = capabilities.SupportsFunctionCalling,
                SupportsStreaming = capabilities.SupportsStreaming,
                SupportsAudioTranscription = capabilities.SupportsAudioTranscription,
                SupportsTextToSpeech = capabilities.SupportsTextToSpeech,
                SupportsRealtimeAudio = capabilities.SupportsRealtimeAudio,
                SupportsImageGeneration = capabilities.SupportsImageGeneration,
                SupportsVideoGeneration = capabilities.SupportsVideoGeneration,
                SupportsEmbeddings = capabilities.SupportsEmbeddings,
                MaxTokens = capabilities.MaxTokens,
                TokenizerType = capabilities.TokenizerType.ToString(),
                SupportedVoices = capabilities.SupportedVoices,
                SupportedLanguages = capabilities.SupportedLanguages,
                SupportedFormats = capabilities.SupportedFormats
            };
        }
    }

    // DTOs for the Model API
    public class ModelDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public ModelType ModelType { get; set; }
        public int ModelSeriesId { get; set; }
        public int ModelCapabilitiesId { get; set; }
        public ModelCapabilitiesDto? Capabilities { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class CreateModelDto
    {
        public string Name { get; set; } = string.Empty;
        public ModelType ModelType { get; set; }
        public int ModelSeriesId { get; set; }
        public int ModelCapabilitiesId { get; set; }
        public bool? IsActive { get; set; }
    }

    public class UpdateModelDto
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public ModelType? ModelType { get; set; }
        public int? ModelSeriesId { get; set; }  // Nullable for partial updates
        public int? ModelCapabilitiesId { get; set; }
        public bool? IsActive { get; set; }
    }

    public class ModelCapabilitiesDto
    {
        public int Id { get; set; }
        public bool SupportsChat { get; set; }
        public bool SupportsVision { get; set; }
        public bool SupportsFunctionCalling { get; set; }
        public bool SupportsStreaming { get; set; }
        public bool SupportsAudioTranscription { get; set; }
        public bool SupportsTextToSpeech { get; set; }
        public bool SupportsRealtimeAudio { get; set; }
        public bool SupportsImageGeneration { get; set; }
        public bool SupportsVideoGeneration { get; set; }
        public bool SupportsEmbeddings { get; set; }
        public int MaxTokens { get; set; }
        public string? TokenizerType { get; set; }
        public string? SupportedVoices { get; set; }
        public string? SupportedLanguages { get; set; }
        public string? SupportedFormats { get; set; }
    }

    public class ModelWithProviderIdDto : ModelDto
    {
        public string ProviderModelId { get; set; } = string.Empty;
    }
}