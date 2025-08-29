using ConduitLLM.Admin.Models.Models;
using ConduitLLM.Admin.Models.ModelCapabilities;
using ConduitLLM.Admin.Models.ModelSeries;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Extensions;
using ConduitLLM.Admin.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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
        private readonly IAdminModelProviderMappingService _mappingService;
        private readonly ILogger<ModelController> _logger;

        /// <summary>
        /// Initializes a new instance of the ModelController
        /// </summary>
        public ModelController(
            IModelRepository modelRepository,
            IAdminModelProviderMappingService mappingService,
            ILogger<ModelController> logger)
        {
            _modelRepository = modelRepository ?? throw new ArgumentNullException(nameof(modelRepository));
            _mappingService = mappingService ?? throw new ArgumentNullException(nameof(mappingService));
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
        /// Gets model identifiers for a specific model
        /// </summary>
        /// <param name="id">The model ID</param>
        /// <returns>List of model identifiers showing which providers offer this model</returns>
        [HttpGet("{id}/identifiers")]
        [ProducesResponseType(typeof(IEnumerable<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetModelIdentifiers(int id)
        {
            try
            {
                var model = await _modelRepository.GetByIdWithDetailsAsync(id);
                if (model == null)
                {
                    return NotFound($"Model with ID {id} not found");
                }

                var identifiers = model.Identifiers.Select(i => new
                {
                    id = i.Id,
                    identifier = i.Identifier,
                    provider = i.Provider,
                    isPrimary = i.IsPrimary
                });

                return Ok(identifiers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting identifiers for model with ID {Id}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving model identifiers");
            }
        }

        /// <summary>
        /// Creates a new model identifier for a specific model
        /// </summary>
        /// <param name="id">The model ID</param>
        /// <param name="dto">The identifier data</param>
        /// <returns>The created identifier</returns>
        [HttpPost("{id}/identifiers")]
        [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CreateModelIdentifier(int id, [FromBody] CreateModelIdentifierDto dto)
        {
            try
            {
                var model = await _modelRepository.GetByIdWithDetailsAsync(id);
                if (model == null)
                {
                    return NotFound($"Model with ID {id} not found");
                }

                // Check if identifier already exists for this provider
                var existing = model.Identifiers.FirstOrDefault(i => 
                    i.Identifier == dto.Identifier && 
                    i.Provider == dto.Provider);
                    
                if (existing != null)
                {
                    return Conflict($"Identifier '{dto.Identifier}' already exists for provider '{dto.Provider}'");
                }

                var identifier = new ModelProviderTypeAssociation
                {
                    ModelId = id,
                    Identifier = dto.Identifier,
                    Provider = dto.Provider,
                    IsPrimary = dto.IsPrimary ?? false,
                    Metadata = dto.Metadata
                };

                model.Identifiers.Add(identifier);
                await _modelRepository.UpdateAsync(model);

                return CreatedAtAction(nameof(GetModelIdentifiers), new { id }, new
                {
                    id = identifier.Id,
                    identifier = identifier.Identifier,
                    provider = identifier.Provider,
                    isPrimary = identifier.IsPrimary
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating identifier for model {Id}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while creating the identifier");
            }
        }

        /// <summary>
        /// Updates a model identifier
        /// </summary>
        /// <param name="id">The model ID</param>
        /// <param name="identifierId">The identifier ID</param>
        /// <param name="dto">The updated identifier data</param>
        /// <returns>No content on success</returns>
        [HttpPut("{id}/identifiers/{identifierId}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> UpdateModelIdentifier(int id, int identifierId, [FromBody] UpdateModelIdentifierDto dto)
        {
            try
            {
                var model = await _modelRepository.GetByIdWithDetailsAsync(id);
                if (model == null)
                {
                    return NotFound($"Model with ID {id} not found");
                }

                var identifier = model.Identifiers.FirstOrDefault(i => i.Id == identifierId);
                if (identifier == null)
                {
                    return NotFound($"Identifier with ID {identifierId} not found for model {id}");
                }

                // Check if the new identifier/provider combo already exists (if changed)
                if (identifier.Identifier != dto.Identifier || identifier.Provider != dto.Provider)
                {
                    var existing = model.Identifiers.FirstOrDefault(i => 
                        i.Id != identifierId &&
                        i.Identifier == dto.Identifier && 
                        i.Provider == dto.Provider);
                        
                    if (existing != null)
                    {
                        return Conflict($"Identifier '{dto.Identifier}' already exists for provider '{dto.Provider}'");
                    }
                }

                identifier.Identifier = dto.Identifier;
                identifier.Provider = dto.Provider;
                identifier.IsPrimary = dto.IsPrimary ?? identifier.IsPrimary;
                identifier.Metadata = dto.Metadata;

                await _modelRepository.UpdateAsync(model);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating identifier {IdentifierId} for model {Id}", identifierId, id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while updating the identifier");
            }
        }

        /// <summary>
        /// Deletes a model identifier
        /// </summary>
        /// <param name="id">The model ID</param>
        /// <param name="identifierId">The identifier ID to delete</param>
        /// <returns>No content on success</returns>
        [HttpDelete("{id}/identifiers/{identifierId}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteModelIdentifier(int id, int identifierId)
        {
            try
            {
                // Directly delete the identifier from the repository
                var deleted = await _modelRepository.DeleteIdentifierAsync(id, identifierId);
                
                if (!deleted)
                {
                    return NotFound($"Identifier with ID {identifierId} not found for model {id}");
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting identifier {IdentifierId} for model {Id}", identifierId, id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while deleting the identifier");
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
                if (dto == null)
                {
                    return BadRequest("Model data is required");
                }

                if (string.IsNullOrWhiteSpace(dto.Name))
                {
                    return BadRequest("Model name is required");
                }

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
                    ModelSeriesId = dto.ModelSeriesId,
                    ModelCapabilitiesId = dto.ModelCapabilitiesId,
                    ModelParameters = dto.ModelParameters,
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
        [ProducesResponseType(typeof(ModelDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateModel(int id, [FromBody] UpdateModelDto dto)
        {
            try
            {
                if (dto == null)
                {
                    return BadRequest("Update data is required");
                }

                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var model = await _modelRepository.GetByIdWithDetailsAsync(id);
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

                if (dto.ModelSeriesId.HasValue)
                    model.ModelSeriesId = dto.ModelSeriesId.Value;
                if (dto.ModelCapabilitiesId.HasValue)
                    model.ModelCapabilitiesId = dto.ModelCapabilitiesId.Value;
                if (dto.IsActive.HasValue)
                    model.IsActive = dto.IsActive.Value;
                if (dto.ModelParameters != null)
                    model.ModelParameters = string.IsNullOrWhiteSpace(dto.ModelParameters) ? null : dto.ModelParameters;

                model.UpdatedAt = DateTime.UtcNow;

                var updatedModel = await _modelRepository.UpdateAsync(model);

                return Ok(MapToDto(updatedModel));
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
                ModelSeriesId = model.ModelSeriesId,
                ModelCapabilitiesId = model.ModelCapabilitiesId,
                Capabilities = model.Capabilities != null ? MapCapabilitiesToDto(model.Capabilities) : null,
                IsActive = model.IsActive,
                CreatedAt = model.CreatedAt,
                UpdatedAt = model.UpdatedAt,
                Series = model.Series != null ? MapSeriesToDto(model.Series) : null,
                ModelParameters = model.ModelParameters
            };
        }

        private static ModelSeriesDto MapSeriesToDto(ModelSeries series)
        {
            return new ModelSeriesDto
            {
                Id = series.Id,
                AuthorId = series.AuthorId,
                AuthorName = series.Author?.Name,
                Name = series.Name,
                Description = series.Description,
                TokenizerType = series.TokenizerType,
                Parameters = series.Parameters
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
                SupportsImageGeneration = capabilities.SupportsImageGeneration,
                SupportsVideoGeneration = capabilities.SupportsVideoGeneration,
                SupportsEmbeddings = capabilities.SupportsEmbeddings,
                MaxTokens = capabilities.MaxTokens,
                TokenizerType = capabilities.TokenizerType,
            };
        }

        /// <summary>
        /// Gets all provider mappings for a specific model
        /// </summary>
        /// <param name="id">The model ID</param>
        /// <returns>List of provider mappings for the model</returns>
        [HttpGet("{id}/provider-mappings")]
        [ProducesResponseType(typeof(IEnumerable<ModelProviderMappingDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetModelProviderMappings(int id)
        {
            try
            {
                // Check if model exists
                var model = await _modelRepository.GetByIdAsync(id);
                if (model == null)
                {
                    return NotFound($"Model with ID {id} not found");
                }

                // Get all mappings for this model
                var mappings = await _mappingService.GetMappingsByModelIdAsync(id);
                var dtos = mappings.Select(m => m.ToDto());
                
                return Ok(dtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting provider mappings for model with ID {Id}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving provider mappings");
            }
        }

        /// <summary>
        /// Creates a new provider mapping for a specific model
        /// </summary>
        /// <param name="id">The model ID</param>
        /// <param name="mappingDto">The provider mapping to create</param>
        /// <returns>The created provider mapping</returns>
        [HttpPost("{id}/provider-mappings")]
        [ProducesResponseType(typeof(ModelProviderMappingDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateModelProviderMapping(int id, [FromBody] ModelProviderMappingDto mappingDto)
        {
            try
            {
                // Validate model ID consistency
                if (mappingDto.ModelId != id)
                {
                    return BadRequest("Model ID in URL does not match Model ID in request body");
                }

                // Check if model exists
                var model = await _modelRepository.GetByIdAsync(id);
                if (model == null)
                {
                    return NotFound($"Model with ID {id} not found");
                }

                // Check for duplicate mapping
                var existingMappings = await _mappingService.GetMappingsByModelIdAsync(id);
                if (existingMappings.Any(m => m.ProviderId == mappingDto.ProviderId))
                {
                    return Conflict($"A mapping for model ID {id} with provider ID {mappingDto.ProviderId} already exists");
                }

                // Create the mapping
                var mapping = mappingDto.ToEntity();
                var success = await _mappingService.AddMappingAsync(mapping);

                if (!success)
                {
                    return BadRequest("Failed to create provider mapping");
                }

                // Get the created mapping
                var createdMappings = await _mappingService.GetMappingsByModelIdAsync(id);
                var createdMapping = createdMappings.FirstOrDefault(m => m.ProviderId == mappingDto.ProviderId);

                return CreatedAtAction(
                    nameof(GetModelProviderMappings), 
                    new { id = id }, 
                    createdMapping?.ToDto()
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating provider mapping for model with ID {Id}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while creating the provider mapping");
            }
        }

        /// <summary>
        /// Updates a provider mapping for a specific model
        /// </summary>
        /// <param name="id">The model ID</param>
        /// <param name="mappingId">The mapping ID</param>
        /// <param name="mappingDto">The updated provider mapping data</param>
        /// <returns>No content on success</returns>
        [HttpPut("{id}/provider-mappings/{mappingId}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateModelProviderMapping(int id, int mappingId, [FromBody] ModelProviderMappingDto mappingDto)
        {
            try
            {
                // Validate IDs
                if (mappingDto.ModelId != id)
                {
                    return BadRequest("Model ID in URL does not match Model ID in request body");
                }

                if (mappingDto.Id != mappingId)
                {
                    return BadRequest("Mapping ID in URL does not match Mapping ID in request body");
                }

                // Check if model exists
                var model = await _modelRepository.GetByIdAsync(id);
                if (model == null)
                {
                    return NotFound($"Model with ID {id} not found");
                }

                // Get and update the mapping
                var existingMapping = await _mappingService.GetMappingByIdAsync(mappingId);
                if (existingMapping == null)
                {
                    return NotFound($"Provider mapping with ID {mappingId} not found");
                }

                if (existingMapping.ModelId != id)
                {
                    return BadRequest($"Mapping with ID {mappingId} does not belong to model with ID {id}");
                }

                existingMapping.UpdateFromDto(mappingDto);
                var success = await _mappingService.UpdateMappingAsync(existingMapping);

                if (!success)
                {
                    return BadRequest("Failed to update provider mapping");
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating provider mapping {MappingId} for model {ModelId}", mappingId, id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while updating the provider mapping");
            }
        }

        /// <summary>
        /// Deletes a provider mapping for a specific model
        /// </summary>
        /// <param name="id">The model ID</param>
        /// <param name="mappingId">The mapping ID to delete</param>
        /// <returns>No content on success</returns>
        [HttpDelete("{id}/provider-mappings/{mappingId}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteModelProviderMapping(int id, int mappingId)
        {
            try
            {
                // Check if model exists
                var model = await _modelRepository.GetByIdAsync(id);
                if (model == null)
                {
                    return NotFound($"Model with ID {id} not found");
                }

                // Check if mapping exists and belongs to this model
                var existingMapping = await _mappingService.GetMappingByIdAsync(mappingId);
                if (existingMapping == null)
                {
                    return NotFound($"Provider mapping with ID {mappingId} not found");
                }

                if (existingMapping.ModelId != id)
                {
                    return BadRequest($"Mapping with ID {mappingId} does not belong to model with ID {id}");
                }

                var success = await _mappingService.DeleteMappingAsync(mappingId);

                if (!success)
                {
                    return BadRequest("Failed to delete provider mapping");
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting provider mapping {MappingId} for model {ModelId}", mappingId, id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while deleting the provider mapping");
            }
        }
    }
}