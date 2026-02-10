using ConduitLLM.Admin.Models.Models;
using ConduitLLM.Admin.Models.ModelSeries;
using ConduitLLM.Admin.Models.ModelCapabilities;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Extensions;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Core.Events;
using MassTransit;
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
    public class ModelController : AdminControllerBase
    {
        private readonly IModelRepository _modelRepository;
        private readonly IAdminModelProviderMappingService _mappingService;
        private readonly IProviderRepository _providerRepository;
        private readonly IPublishEndpoint _publishEndpoint;

        /// <summary>
        /// Initializes a new instance of the ModelController
        /// </summary>
        public ModelController(
            IModelRepository modelRepository,
            IAdminModelProviderMappingService mappingService,
            IProviderRepository providerRepository,
            IPublishEndpoint publishEndpoint,
            ILogger<ModelController> logger)
            : base(publishEndpoint, logger)
        {
            _modelRepository = modelRepository ?? throw new ArgumentNullException(nameof(modelRepository));
            _mappingService = mappingService ?? throw new ArgumentNullException(nameof(mappingService));
            _providerRepository = providerRepository ?? throw new ArgumentNullException(nameof(providerRepository));
            _publishEndpoint = publishEndpoint ?? throw new ArgumentNullException(nameof(publishEndpoint));
        }

        /// <summary>
        /// Gets all models with their capabilities
        /// </summary>
        /// <returns>List of all models</returns>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<ModelDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public Task<IActionResult> GetAllModels()
        {
            return ExecuteAsync(
                async () =>
                {
                    var models = await _modelRepository.GetAllWithDetailsAsync();
                    return models.Select(m => MapToDto(m));
                },
                result => Ok(result),
                "GetAllModels");
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
        public Task<IActionResult> GetModelById(int id)
        {
            return ExecuteWithNotFoundAsync(
                () => _modelRepository.GetByIdWithDetailsAsync(id),
                model => Ok(MapToDto(model)),
                "Model", id, "GetModelById");
        }


        /// <summary>
        /// Searches for models by name
        /// </summary>
        /// <param name="query">The search query</param>
        /// <returns>List of matching models</returns>
        [HttpGet("search")]
        [ProducesResponseType(typeof(IEnumerable<ModelDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public Task<IActionResult> SearchModels([FromQuery] string query)
        {
            return ExecuteAsync(
                async () =>
                {
                    if (string.IsNullOrWhiteSpace(query))
                    {
                        return (object)new List<ModelDto>();
                    }

                    var models = await _modelRepository.SearchByNameAsync(query);
                    return models.Select(m => MapToDto(m));
                },
                result => Ok(result),
                "SearchModels");
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
        public Task<IActionResult> GetModelsByProvider(string provider)
        {
            if (string.IsNullOrWhiteSpace(provider))
            {
                return Task.FromResult<IActionResult>(BadRequest("Provider name is required"));
            }

            // Parse provider string to enum
            if (!Enum.TryParse<ProviderType>(provider, ignoreCase: true, out var providerType))
            {
                var validProviders = Enum.GetNames<ProviderType>()
                    .Select(p => p.ToLowerInvariant());
                return Task.FromResult<IActionResult>(BadRequest($"Invalid provider '{provider}'. Valid providers: {string.Join(", ", validProviders)}"));
            }

            return ExecuteAsync(
                async () =>
                {
                    var models = await _modelRepository.GetByProviderAsync(providerType);
                    return models.Select(m =>
                    {
                        // Repository already handles the provider string to enum conversion
                        // Just get the first identifier for this model (they're already filtered by provider)
                        var providerIdentifier = m.Identifiers?.FirstOrDefault()?.Identifier
                            ?? m.Name; // Fallback to model name if no specific identifier

                        // Use MapToDto to get base DTO, then create extended DTO
                        var baseDto = MapToDto(m);
                        return new ModelWithProviderIdDto
                        {
                            Id = baseDto.Id,
                            Name = baseDto.Name,
                            ProviderModelId = providerIdentifier,
                            ModelSeriesId = baseDto.ModelSeriesId,
                            IsActive = baseDto.IsActive,
                            CreatedAt = baseDto.CreatedAt,
                            UpdatedAt = baseDto.UpdatedAt,
                            Series = baseDto.Series,
                            ModelParameters = baseDto.ModelParameters,
                            // Copy capability fields
                            SupportsChat = baseDto.SupportsChat,
                            SupportsVision = baseDto.SupportsVision,
                            SupportsFunctionCalling = baseDto.SupportsFunctionCalling,
                            SupportsStreaming = baseDto.SupportsStreaming,
                            SupportsImageGeneration = baseDto.SupportsImageGeneration,
                            SupportsVideoGeneration = baseDto.SupportsVideoGeneration,
                            SupportsEmbeddings = baseDto.SupportsEmbeddings,
                            MaxInputTokens = baseDto.MaxInputTokens,
                            MaxOutputTokens = baseDto.MaxOutputTokens,
                            TokenizerType = baseDto.TokenizerType
                        };
                    });
                },
                result => Ok(result),
                "GetModelsByProvider",
                new { Provider = provider });
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
        public Task<IActionResult> GetModelIdentifiers(int id)
        {
            return ExecuteWithNotFoundAsync(
                () => _modelRepository.GetByIdWithDetailsAsync(id),
                model =>
                {
                    var identifiers = model.Identifiers.Select(i => new
                    {
                        id = i.Id,
                        identifier = i.Identifier,
                        provider = (int?)i.Provider,
                        isPrimary = i.IsPrimary,
                        maxInputTokens = i.MaxInputTokens,
                        maxOutputTokens = i.MaxOutputTokens,
                        speedScore = i.SpeedScore,
                        qualityScore = i.QualityScore,
                        providerVariation = i.ProviderVariation,
                        modelCostId = i.ModelCostId
                    });

                    return Ok(identifiers);
                },
                "Model", id, "GetModelIdentifiers");
        }

        /// <summary>
        /// Gets model associations with available providers
        /// Returns only associations where matching providers are configured
        /// </summary>
        /// <param name="id">The model ID</param>
        /// <returns>List of associations with their available providers</returns>
        [HttpGet("{id}/available-providers")]
        [ProducesResponseType(typeof(IEnumerable<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public Task<IActionResult> GetAvailableProviders(int id)
        {
            return ExecuteWithNotFoundAsync(
                () => _modelRepository.GetByIdWithDetailsAsync(id),
                async model =>
                {
                    var providers = await RepositoryPaginationExtensions.GetAllViaPaginationAsync(
                        _providerRepository.GetPaginatedAsync);
                    var enabledProviders = providers.Where(p => p.IsEnabled).ToList();

                    var result = new List<object>();

                    foreach (var association in model.Identifiers)
                    {
                        // Skip associations without a provider type - they're not properly configured
                        if (association.Provider == null)
                        {
                            Logger.LogWarning(
                                "ModelIdentifier {AssociationId} for model {ModelId} has null Provider field - skipping",
                                association.Id, id);
                            continue;
                        }

                        // Find matching providers for this association
                        var matchingProviders = enabledProviders.Where(p =>
                            p.ProviderType == association.Provider
                        ).ToList();

                        if (matchingProviders.Any())
                        {
                            result.Add(new
                            {
                                associationId = association.Id,
                                identifier = association.Identifier,
                                provider = (int?)association.Provider,
                                providerVariation = association.ProviderVariation,
                                maxInputTokens = association.MaxInputTokens,
                                maxOutputTokens = association.MaxOutputTokens,
                                speedScore = association.SpeedScore,
                                qualityScore = association.QualityScore,
                                isPrimary = association.IsPrimary,
                                availableProviders = matchingProviders.Select(p => new
                                {
                                    providerId = p.Id,
                                    providerName = p.ProviderName,
                                    providerType = p.ProviderType.ToString()
                                })
                            });
                        }
                    }

                    return (IActionResult)Ok(result);
                },
                "Model", id, "GetAvailableProviders");
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
        public Task<IActionResult> CreateModelIdentifier(int id, [FromBody] CreateModelIdentifierDto dto)
        {
            return ExecuteAsync(
                async () =>
                {
                    var model = await _modelRepository.GetByIdWithDetailsAsync(id);
                    if (model == null)
                    {
                        return (IActionResult)NotFound($"Model with ID {id} not found");
                    }

                    // Parse provider if provided as integer
                    ProviderType? providerType = dto.Provider.HasValue ? (ProviderType)dto.Provider.Value : null;

                    // Check if identifier already exists for this provider
                    var existing = model.Identifiers.FirstOrDefault(i =>
                        i.Identifier == dto.Identifier &&
                        i.Provider == providerType);

                    if (existing != null)
                    {
                        return Conflict($"Identifier '{dto.Identifier}' already exists for provider '{dto.Provider}'");
                    }

                    var identifier = new ModelProviderTypeAssociation
                    {
                        ModelId = id,
                        Identifier = dto.Identifier,
                        Provider = providerType,
                        IsPrimary = dto.IsPrimary ?? false,
                        Metadata = dto.Metadata,
                        MaxInputTokens = dto.MaxInputTokens,
                        MaxOutputTokens = dto.MaxOutputTokens,
                        SpeedScore = dto.SpeedScore,
                        QualityScore = dto.QualityScore,
                        ProviderVariation = dto.ProviderVariation
                    };

                    model.Identifiers.Add(identifier);
                    await _modelRepository.UpdateModelAsync(model);

                    return CreatedAtAction(nameof(GetModelIdentifiers), new { id }, new
                    {
                        id = identifier.Id,
                        identifier = identifier.Identifier,
                        provider = (int?)identifier.Provider,
                        isPrimary = identifier.IsPrimary,
                        maxInputTokens = identifier.MaxInputTokens,
                        maxOutputTokens = identifier.MaxOutputTokens,
                        speedScore = identifier.SpeedScore,
                        qualityScore = identifier.QualityScore,
                        providerVariation = identifier.ProviderVariation
                    });
                },
                result => result,
                "CreateModelIdentifier",
                new { Id = id });
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
        public Task<IActionResult> UpdateModelIdentifier(int id, int identifierId, [FromBody] UpdateModelIdentifierDto dto)
        {
            return ExecuteAsync(
                async () =>
                {
                    var model = await _modelRepository.GetByIdWithDetailsAsync(id);
                    if (model == null)
                    {
                        return (IActionResult)NotFound($"Model with ID {id} not found");
                    }

                    var identifier = model.Identifiers.FirstOrDefault(i => i.Id == identifierId);
                    if (identifier == null)
                    {
                        return NotFound($"Identifier with ID {identifierId} not found for model {id}");
                    }

                    // Parse provider if provided as integer
                    ProviderType? providerType = dto.Provider.HasValue ? (ProviderType)dto.Provider.Value : null;

                    // Check if the new identifier/provider combo already exists (if changed)
                    if (identifier.Identifier != dto.Identifier || identifier.Provider != providerType)
                    {
                        var existing = model.Identifiers.FirstOrDefault(i =>
                            i.Id != identifierId &&
                            i.Identifier == dto.Identifier &&
                            i.Provider == providerType);

                        if (existing != null)
                        {
                            return Conflict($"Identifier '{dto.Identifier}' already exists for provider '{dto.Provider}'");
                        }
                    }

                    identifier.Identifier = dto.Identifier;
                    identifier.Provider = providerType;
                    identifier.IsPrimary = dto.IsPrimary ?? identifier.IsPrimary;
                    identifier.Metadata = dto.Metadata;
                    identifier.MaxInputTokens = dto.MaxInputTokens;
                    identifier.MaxOutputTokens = dto.MaxOutputTokens;
                    identifier.SpeedScore = dto.SpeedScore;
                    identifier.QualityScore = dto.QualityScore;
                    identifier.ProviderVariation = dto.ProviderVariation;

                    await _modelRepository.UpdateModelAsync(model);

                    return (IActionResult)NoContent();
                },
                result => result,
                "UpdateModelIdentifier",
                new { Id = id, IdentifierId = identifierId });
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
        public Task<IActionResult> DeleteModelIdentifier(int id, int identifierId)
        {
            return ExecuteAsync(
                async () =>
                {
                    // Directly delete the identifier from the repository
                    var deleted = await _modelRepository.DeleteIdentifierAsync(id, identifierId);

                    if (!deleted)
                    {
                        throw new KeyNotFoundException($"Identifier with ID {identifierId} not found for model {id}");
                    }
                },
                NoContent(),
                "DeleteModelIdentifier",
                new { Id = id, IdentifierId = identifierId });
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
        public Task<IActionResult> CreateModel([FromBody] CreateModelDto dto)
        {
            if (dto == null)
            {
                return Task.FromResult<IActionResult>(BadRequest("Model data is required"));
            }

            if (string.IsNullOrWhiteSpace(dto.Name))
            {
                return Task.FromResult<IActionResult>(BadRequest("Model name is required"));
            }

            if (!ModelState.IsValid)
            {
                return Task.FromResult<IActionResult>(BadRequest(ModelState));
            }

            return ExecuteAsync(
                async () =>
                {
                    // Check if a model with the same name already exists
                    var existing = await _modelRepository.GetByNameAsync(dto.Name);
                    if (existing != null)
                    {
                        return (IActionResult)Conflict($"A model with name '{dto.Name}' already exists");
                    }

                    var model = new Model
                    {
                        Name = dto.Name,
                        ModelSeriesId = dto.ModelSeriesId,
                        ModelParameters = dto.ModelParameters,
                        IsActive = dto.IsActive ?? true,
                        // Set capability fields directly
                        SupportsChat = dto.SupportsChat,
                        SupportsVision = dto.SupportsVision,
                        SupportsFunctionCalling = dto.SupportsFunctionCalling,
                        SupportsStreaming = dto.SupportsStreaming,
                        SupportsImageGeneration = dto.SupportsImageGeneration,
                        SupportsVideoGeneration = dto.SupportsVideoGeneration,
                        SupportsEmbeddings = dto.SupportsEmbeddings,
                        MaxInputTokens = dto.MaxInputTokens,
                        MaxOutputTokens = dto.MaxOutputTokens,
                        TokenizerType = dto.TokenizerType,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    await _modelRepository.CreateModelAsync(model);

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
                },
                result => result,
                "CreateModel");
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
        public Task<IActionResult> UpdateModel(int id, [FromBody] UpdateModelDto dto)
        {
            if (dto == null)
            {
                return Task.FromResult<IActionResult>(BadRequest("Update data is required"));
            }

            if (!ModelState.IsValid)
            {
                return Task.FromResult<IActionResult>(BadRequest(ModelState));
            }

            return ExecuteAsync(
                async () =>
                {
                    var model = await _modelRepository.GetByIdWithDetailsAsync(id);
                    if (model == null)
                    {
                        return (IActionResult)NotFound($"Model with ID {id} not found");
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
                    if (dto.IsActive.HasValue)
                        model.IsActive = dto.IsActive.Value;
                    if (dto.ModelParameters != null)
                        model.ModelParameters = string.IsNullOrWhiteSpace(dto.ModelParameters) ? null : dto.ModelParameters;

                    // Update capability fields
                    if (dto.SupportsChat.HasValue)
                        model.SupportsChat = dto.SupportsChat.Value;
                    if (dto.SupportsVision.HasValue)
                        model.SupportsVision = dto.SupportsVision.Value;
                    if (dto.SupportsFunctionCalling.HasValue)
                        model.SupportsFunctionCalling = dto.SupportsFunctionCalling.Value;
                    if (dto.SupportsStreaming.HasValue)
                        model.SupportsStreaming = dto.SupportsStreaming.Value;
                    if (dto.SupportsImageGeneration.HasValue)
                        model.SupportsImageGeneration = dto.SupportsImageGeneration.Value;
                    if (dto.SupportsVideoGeneration.HasValue)
                        model.SupportsVideoGeneration = dto.SupportsVideoGeneration.Value;
                    if (dto.SupportsEmbeddings.HasValue)
                        model.SupportsEmbeddings = dto.SupportsEmbeddings.Value;
                    // For nullable int fields, we need to handle them differently
                    // The DTO will have the property set if it was included in the JSON
                    // We always update these fields since the frontend always sends them
                    model.MaxInputTokens = dto.MaxInputTokens;
                    model.MaxOutputTokens = dto.MaxOutputTokens;

                    model.UpdatedAt = DateTime.UtcNow;

                    // Track if parameters were changed
                    bool parametersChanged = dto.ModelParameters != null;

                    var updatedModel = await _modelRepository.UpdateModelAsync(model);

                    // Publish ModelUpdated event for cache invalidation
                    await _publishEndpoint.Publish(new ModelUpdated
                    {
                        ModelId = updatedModel.Id,
                        ModelName = updatedModel.Name,
                        ModelSeriesId = updatedModel.ModelSeriesId,
                        ChangeType = "Updated",
                        ParametersChanged = parametersChanged,
                        ChangedProperties = GetChangedProperties(dto)
                    });

                    Logger.LogInformation("Published ModelUpdated event for model {ModelId} ({ModelName})",
                        updatedModel.Id, updatedModel.Name);

                    return (IActionResult)Ok(MapToDto(updatedModel));
                },
                result => result,
                "UpdateModel",
                new { Id = id });
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
        public Task<IActionResult> DeleteModel(int id)
        {
            return ExecuteAsync(
                async () =>
                {
                    var model = await _modelRepository.GetByIdAsync(id);
                    if (model == null)
                    {
                        return (IActionResult)NotFound($"Model with ID {id} not found");
                    }

                    // Check if model is referenced by any mappings
                    var hasReferences = await _modelRepository.HasMappingReferencesAsync(id);
                    if (hasReferences)
                    {
                        return Conflict("Cannot delete model that is referenced by model provider mappings");
                    }

                    await _modelRepository.DeleteAsync(id);

                    return (IActionResult)NoContent();
                },
                result => result,
                "DeleteModel",
                new { Id = id });
        }

        private static ModelDto MapToDto(Model model)
        {
            // Map model with embedded capabilities
            return new ModelDto
            {
                Id = model.Id,
                Name = model.Name,
                ModelSeriesId = model.ModelSeriesId,
                IsActive = model.IsActive,
                CreatedAt = model.CreatedAt,
                UpdatedAt = model.UpdatedAt,
                Series = model.Series != null ? MapSeriesToDto(model.Series) : null,
                ModelParameters = model.ModelParameters,
                // Capability fields embedded directly
                SupportsChat = model.SupportsChat,
                SupportsVision = model.SupportsVision,
                SupportsImageGeneration = model.SupportsImageGeneration,
                SupportsVideoGeneration = model.SupportsVideoGeneration,
                SupportsEmbeddings = model.SupportsEmbeddings,
                SupportsFunctionCalling = model.SupportsFunctionCalling,
                SupportsStreaming = model.SupportsStreaming,
                MaxInputTokens = model.MaxInputTokens,
                MaxOutputTokens = model.MaxOutputTokens,
                TokenizerType = model.TokenizerType
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


        /// <summary>
        /// Gets all provider mappings for a specific model
        /// </summary>
        /// <param name="id">The model ID</param>
        /// <returns>List of provider mappings for the model</returns>
        [HttpGet("{id}/provider-mappings")]
        [ProducesResponseType(typeof(IEnumerable<ModelProviderMappingDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public Task<IActionResult> GetModelProviderMappings(int id)
        {
            return ExecuteWithNotFoundAsync(
                () => _modelRepository.GetByIdAsync(id),
                async model =>
                {
                    // Get all mappings for this model
                    var mappings = await _mappingService.GetMappingsByModelIdAsync(id);
                    var dtos = mappings.Select(m => m.ToDto());

                    return (IActionResult)Ok(dtos);
                },
                "Model", id, "GetModelProviderMappings");
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
        public Task<IActionResult> CreateModelProviderMapping(int id, [FromBody] ModelProviderMappingDto mappingDto)
        {
            return ExecuteAsync(
                async () =>
                {
                    // Skip ModelId validation since it's no longer on the DTO
                    // The ModelProviderTypeAssociationId provides the model relationship

                    // Check if model exists
                    var model = await _modelRepository.GetByIdAsync(id);
                    if (model == null)
                    {
                        return (IActionResult)NotFound($"Model with ID {id} not found");
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
                },
                result => result,
                "CreateModelProviderMapping",
                new { Id = id });
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
        public Task<IActionResult> UpdateModelProviderMapping(int id, int mappingId, [FromBody] ModelProviderMappingDto mappingDto)
        {
            if (mappingDto.Id != mappingId)
            {
                return Task.FromResult<IActionResult>(BadRequest("Mapping ID in URL does not match Mapping ID in request body"));
            }

            return ExecuteAsync(
                async () =>
                {
                    // Skip ModelId validation since it's no longer on the DTO
                    // The ModelProviderTypeAssociationId provides the model relationship

                    // Check if model exists
                    var model = await _modelRepository.GetByIdAsync(id);
                    if (model == null)
                    {
                        return (IActionResult)NotFound($"Model with ID {id} not found");
                    }

                    // Get and update the mapping
                    var existingMapping = await _mappingService.GetMappingByIdAsync(mappingId);
                    if (existingMapping == null)
                    {
                        return NotFound($"Provider mapping with ID {mappingId} not found");
                    }

                    if (existingMapping.ModelProviderTypeAssociation?.ModelId != id)
                    {
                        return BadRequest($"Mapping with ID {mappingId} does not belong to model with ID {id}");
                    }

                    existingMapping.UpdateFromDto(mappingDto);
                    var success = await _mappingService.UpdateMappingAsync(existingMapping);

                    if (!success)
                    {
                        return BadRequest("Failed to update provider mapping");
                    }

                    return (IActionResult)NoContent();
                },
                result => result,
                "UpdateModelProviderMapping",
                new { Id = id, MappingId = mappingId });
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
        public Task<IActionResult> DeleteModelProviderMapping(int id, int mappingId)
        {
            return ExecuteAsync(
                async () =>
                {
                    // Check if model exists
                    var model = await _modelRepository.GetByIdAsync(id);
                    if (model == null)
                    {
                        return (IActionResult)NotFound($"Model with ID {id} not found");
                    }

                    // Check if mapping exists and belongs to this model
                    var existingMapping = await _mappingService.GetMappingByIdAsync(mappingId);
                    if (existingMapping == null)
                    {
                        return NotFound($"Provider mapping with ID {mappingId} not found");
                    }

                    if (existingMapping.ModelProviderTypeAssociation?.ModelId != id)
                    {
                        return BadRequest($"Mapping with ID {mappingId} does not belong to model with ID {id}");
                    }

                    var success = await _mappingService.DeleteMappingAsync(mappingId);

                    if (!success)
                    {
                        return BadRequest("Failed to delete provider mapping");
                    }

                    return (IActionResult)NoContent();
                },
                result => result,
                "DeleteModelProviderMapping",
                new { Id = id, MappingId = mappingId });
        }

        /// <summary>
        /// Helper method to get list of changed properties from DTO
        /// </summary>
        private static string[] GetChangedProperties(UpdateModelDto dto)
        {
            var changedProps = new List<string>();

            if (dto.Name != null) changedProps.Add("Name");
            if (dto.ModelSeriesId.HasValue) changedProps.Add("ModelSeriesId");
            if (dto.IsActive.HasValue) changedProps.Add("IsActive");
            if (dto.ModelParameters != null) changedProps.Add("ModelParameters");
            if (dto.SupportsChat.HasValue) changedProps.Add("SupportsChat");
            if (dto.SupportsVision.HasValue) changedProps.Add("SupportsVision");
            if (dto.SupportsFunctionCalling.HasValue) changedProps.Add("SupportsFunctionCalling");
            if (dto.SupportsStreaming.HasValue) changedProps.Add("SupportsStreaming");
            if (dto.SupportsImageGeneration.HasValue) changedProps.Add("SupportsImageGeneration");
            if (dto.SupportsVideoGeneration.HasValue) changedProps.Add("SupportsVideoGeneration");
            if (dto.SupportsEmbeddings.HasValue) changedProps.Add("SupportsEmbeddings");
            if (dto.MaxInputTokens.HasValue) changedProps.Add("MaxInputTokens");
            if (dto.MaxOutputTokens.HasValue) changedProps.Add("MaxOutputTokens");

            return changedProps.ToArray();
        }
    }
}
