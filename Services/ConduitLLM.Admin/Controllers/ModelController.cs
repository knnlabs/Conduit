using ConduitLLM.Admin.Extensions;
using ConduitLLM.Admin.Filters;
using ConduitLLM.Admin.Models.Models;
using ConduitLLM.Admin.Models.ModelSeries;
using ConduitLLM.Admin.Models.ModelCapabilities;
using ConduitLLM.Admin.Services;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Messaging;
using ConduitLLM.Configuration.Extensions;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Extensions;
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
    [ServiceFilter(typeof(OperationLoggingFilter))]
    public partial class ModelController : AdminControllerBase
    {
        private readonly IModelRepository _modelRepository;
        private readonly IAdminModelProviderMappingService _mappingService;
        private readonly IProviderRepository _providerRepository;
        private readonly IEventBus _eventBus;

        /// <summary>
        /// Initializes a new instance of the ModelController
        /// </summary>
        public ModelController(
            IModelRepository modelRepository,
            IAdminModelProviderMappingService mappingService,
            IProviderRepository providerRepository,
            IEventBus eventBus,
            ILogger<ModelController> logger)
            : base(eventBus, logger)
        {
            _modelRepository = modelRepository ?? throw new ArgumentNullException(nameof(modelRepository));
            _mappingService = mappingService ?? throw new ArgumentNullException(nameof(mappingService));
            _providerRepository = providerRepository ?? throw new ArgumentNullException(nameof(providerRepository));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        }

        /// <summary>
        /// Gets all models with their capabilities.
        /// Supports optional search and filtering.
        /// </summary>
        /// <param name="search">Optional search term for model name (case-insensitive partial match)</param>
        /// <param name="capability">Optional capability filter: chat, vision, image, video, embeddings</param>
        /// <param name="hasProviders">Optional filter: true = only models with identifiers, false = without</param>
        /// <returns>List of all matching models</returns>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<ModelDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAllModels(
            [FromQuery] string? search = null,
            [FromQuery] string? capability = null,
            [FromQuery] bool? hasProviders = null)
        {
            var (models, _) = await _modelRepository.GetPaginatedWithFilterAsync(
                null, null, search, capability, hasProviders);

            var dtos = models.Select(m => m.ToDto()).ToList();
            return Ok(dtos);
        }

        /// <summary>
        /// Gets a paginated page of models with optional search and filtering.
        /// </summary>
        /// <param name="page">Page number (1-based).</param>
        /// <param name="pageSize">Items per page (max 100).</param>
        /// <param name="search">Optional search term for model name (case-insensitive partial match)</param>
        /// <param name="capability">Optional capability filter: chat, vision, image, video, embeddings</param>
        /// <param name="hasProviders">Optional filter: true = only models with identifiers, false = without</param>
        /// <returns>A paginated result of matching models</returns>
        [HttpGet("paged")]
        [ProducesResponseType(typeof(PagedResult<ModelDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetPagedModels(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            [FromQuery] string? search = null,
            [FromQuery] string? capability = null,
            [FromQuery] bool? hasProviders = null)
        {
            page = Math.Max(page, 1);
            if (pageSize < 1) pageSize = 50;
            if (pageSize > 100) pageSize = 100;

            var (models, totalCount) = await _modelRepository.GetPaginatedWithFilterAsync(
                page, pageSize, search, capability, hasProviders);

            return Ok(new PagedResult<ModelDto>
            {
                Items = models.Select(m => m.ToDto()).ToList(),
                TotalCount = totalCount,
                CurrentPage = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            });
        }

        /// <summary>
        /// Gets a specific model by ID
        /// </summary>
        /// <param name="id">The model ID</param>
        /// <returns>The model with its capabilities</returns>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ModelDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetModelById(int id)
        {
            var model = await _modelRepository.GetByIdWithDetailsAsync(id);
            if (model == null)
            {
                return this.NotFoundEntity("Model", id);
            }

            return Ok(model.ToDto());
        }


        /// <summary>
        /// Searches for models by name
        /// </summary>
        /// <param name="query">The search query</param>
        /// <returns>List of matching models</returns>
        [HttpGet("search")]
        [ProducesResponseType(typeof(IEnumerable<ModelDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> SearchModels([FromQuery] string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return Ok(new List<ModelDto>());
            }

            var models = await _modelRepository.SearchByNameAsync(query);
            return Ok(models.Select(m => m.ToDto()));
        }

        /// <summary>
        /// Gets models available from a specific provider
        /// </summary>
        /// <param name="provider">The provider name (e.g., "groq", "openai", "anthropic")</param>
        /// <returns>List of models available from the provider</returns>
        [HttpGet("provider/{provider}")]
        [ProducesResponseType(typeof(IEnumerable<ModelWithProviderIdDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetModelsByProvider(string provider)
        {
            if (string.IsNullOrWhiteSpace(provider))
            {
                return BadRequest("Provider name is required");
            }

            // Parse provider string to enum
            if (!Enum.TryParse<ProviderType>(provider, ignoreCase: true, out var providerType))
            {
                var validProviders = Enum.GetNames<ProviderType>()
                    .Select(p => p.ToLowerInvariant());
                return BadRequest($"Invalid provider '{provider}'. Valid providers: {string.Join(", ", validProviders)}");
            }

            var models = await _modelRepository.GetByProviderAsync(providerType);
            return Ok(models.Select(m =>
            {
                // Repository already handles the provider string to enum conversion
                // Just get the first identifier for this model (they're already filtered by provider)
                var providerIdentifier = m.Identifiers?.FirstOrDefault()?.Identifier
                    ?? m.Name; // Fallback to model name if no specific identifier

                // Use MapToDto to get base DTO, then create extended DTO
                var baseDto = m.ToDto();
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
                    SupportsSpeechToText = baseDto.SupportsSpeechToText,
                    SupportsTextToSpeech = baseDto.SupportsTextToSpeech,
                    SupportsRerank = baseDto.SupportsRerank,
                    SupportsEmbeddings = baseDto.SupportsEmbeddings,
                    MaxInputTokens = baseDto.MaxInputTokens,
                    MaxOutputTokens = baseDto.MaxOutputTokens,
                    TokenizerType = baseDto.TokenizerType
                };
            }));
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
        public async Task<IActionResult> CreateModel([FromBody] CreateModelDto dto)
        {
            if (dto == null)
            {
                return BadRequest("Model data is required");
            }

            if (string.IsNullOrWhiteSpace(dto.Name))
            {
                return BadRequest("Model name is required");
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
                ModelParameters = dto.ModelParameters,
                IsActive = dto.IsActive ?? true,
                // Set capability fields directly
                SupportsChat = dto.SupportsChat,
                SupportsVision = dto.SupportsVision,
                SupportsFunctionCalling = dto.SupportsFunctionCalling,
                SupportsStreaming = dto.SupportsStreaming,
                SupportsImageGeneration = dto.SupportsImageGeneration,
                SupportsVideoGeneration = dto.SupportsVideoGeneration,
                SupportsSpeechToText = dto.SupportsSpeechToText,
                SupportsTextToSpeech = dto.SupportsTextToSpeech,
                SupportsRerank = dto.SupportsRerank,
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

            LogAdminAudit("Created", "Model", model.Id, $"Name: {LoggingSanitizer.S(model.Name)}");
            AdminOperationsMetricsService.RecordConfigurationChange("model", "create");

            return CreatedAtAction(
                nameof(GetModelById),
                new { id = model.Id },
                model.ToDto());
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
        public async Task<IActionResult> UpdateModel(int id, [FromBody] UpdateModelDto dto)
        {
            if (dto == null)
            {
                return BadRequest("Update data is required");
            }

            var model = await _modelRepository.GetByIdWithDetailsAsync(id);
            if (model == null)
            {
                return NotFound($"Model with ID {id} not found");
            }

            // Capture pre-state for change tracking
            var changes = new List<(string Property, string? OldValue, string? NewValue)>();

            // Check for name conflicts if name is being changed
            if (!string.IsNullOrEmpty(dto.Name) && dto.Name != model.Name)
            {
                var existing = await _modelRepository.GetByNameAsync(dto.Name);
                if (existing != null && existing.Id != id)
                {
                    return Conflict($"A model with name '{dto.Name}' already exists");
                }
                changes.Add(("Name", model.Name, dto.Name));
                model.Name = dto.Name;
            }

            if (dto.ModelSeriesId.HasValue && model.ModelSeriesId != dto.ModelSeriesId.Value)
            {
                changes.Add(("ModelSeriesId", model.ModelSeriesId.ToString(), dto.ModelSeriesId.Value.ToString()));
                model.ModelSeriesId = dto.ModelSeriesId.Value;
            }
            else if (dto.ModelSeriesId.HasValue)
            {
                model.ModelSeriesId = dto.ModelSeriesId.Value;
            }

            if (dto.IsActive.HasValue && model.IsActive != dto.IsActive.Value)
            {
                changes.Add(("IsActive", model.IsActive.ToString(), dto.IsActive.Value.ToString()));
                model.IsActive = dto.IsActive.Value;
            }
            else if (dto.IsActive.HasValue)
            {
                model.IsActive = dto.IsActive.Value;
            }

            if (dto.ModelParameters != null)
            {
                var newParams = string.IsNullOrWhiteSpace(dto.ModelParameters) ? null : dto.ModelParameters;
                if (model.ModelParameters != newParams)
                    changes.Add(("ModelParameters", model.ModelParameters ?? "null", newParams ?? "null"));
                model.ModelParameters = newParams;
            }

            // Update capability fields with change tracking
            if (dto.SupportsChat.HasValue)
            {
                if (model.SupportsChat != dto.SupportsChat.Value)
                    changes.Add(("SupportsChat", model.SupportsChat.ToString(), dto.SupportsChat.Value.ToString()));
                model.SupportsChat = dto.SupportsChat.Value;
            }
            if (dto.SupportsVision.HasValue)
            {
                if (model.SupportsVision != dto.SupportsVision.Value)
                    changes.Add(("SupportsVision", model.SupportsVision.ToString(), dto.SupportsVision.Value.ToString()));
                model.SupportsVision = dto.SupportsVision.Value;
            }
            if (dto.SupportsFunctionCalling.HasValue)
            {
                if (model.SupportsFunctionCalling != dto.SupportsFunctionCalling.Value)
                    changes.Add(("SupportsFunctionCalling", model.SupportsFunctionCalling.ToString(), dto.SupportsFunctionCalling.Value.ToString()));
                model.SupportsFunctionCalling = dto.SupportsFunctionCalling.Value;
            }
            if (dto.SupportsStreaming.HasValue)
            {
                if (model.SupportsStreaming != dto.SupportsStreaming.Value)
                    changes.Add(("SupportsStreaming", model.SupportsStreaming.ToString(), dto.SupportsStreaming.Value.ToString()));
                model.SupportsStreaming = dto.SupportsStreaming.Value;
            }
            if (dto.SupportsImageGeneration.HasValue)
            {
                if (model.SupportsImageGeneration != dto.SupportsImageGeneration.Value)
                    changes.Add(("SupportsImageGeneration", model.SupportsImageGeneration.ToString(), dto.SupportsImageGeneration.Value.ToString()));
                model.SupportsImageGeneration = dto.SupportsImageGeneration.Value;
            }
            if (dto.SupportsVideoGeneration.HasValue)
            {
                if (model.SupportsVideoGeneration != dto.SupportsVideoGeneration.Value)
                    changes.Add(("SupportsVideoGeneration", model.SupportsVideoGeneration.ToString(), dto.SupportsVideoGeneration.Value.ToString()));
                model.SupportsVideoGeneration = dto.SupportsVideoGeneration.Value;
            }
            if (dto.SupportsSpeechToText.HasValue)
            {
                if (model.SupportsSpeechToText != dto.SupportsSpeechToText.Value)
                    changes.Add(("SupportsSpeechToText", model.SupportsSpeechToText.ToString(), dto.SupportsSpeechToText.Value.ToString()));
                model.SupportsSpeechToText = dto.SupportsSpeechToText.Value;
            }
            if (dto.SupportsTextToSpeech.HasValue)
            {
                if (model.SupportsTextToSpeech != dto.SupportsTextToSpeech.Value)
                    changes.Add(("SupportsTextToSpeech", model.SupportsTextToSpeech.ToString(), dto.SupportsTextToSpeech.Value.ToString()));
                model.SupportsTextToSpeech = dto.SupportsTextToSpeech.Value;
            }
            if (dto.SupportsRerank.HasValue)
            {
                if (model.SupportsRerank != dto.SupportsRerank.Value)
                    changes.Add(("SupportsRerank", model.SupportsRerank.ToString(), dto.SupportsRerank.Value.ToString()));
                model.SupportsRerank = dto.SupportsRerank.Value;
            }
            if (dto.SupportsEmbeddings.HasValue)
            {
                if (model.SupportsEmbeddings != dto.SupportsEmbeddings.Value)
                    changes.Add(("SupportsEmbeddings", model.SupportsEmbeddings.ToString(), dto.SupportsEmbeddings.Value.ToString()));
                model.SupportsEmbeddings = dto.SupportsEmbeddings.Value;
            }

            // For nullable int fields, always update since frontend always sends them
            if (model.MaxInputTokens != dto.MaxInputTokens)
                changes.Add(("MaxInputTokens", model.MaxInputTokens?.ToString() ?? "null", dto.MaxInputTokens?.ToString() ?? "null"));
            model.MaxInputTokens = dto.MaxInputTokens;

            if (model.MaxOutputTokens != dto.MaxOutputTokens)
                changes.Add(("MaxOutputTokens", model.MaxOutputTokens?.ToString() ?? "null", dto.MaxOutputTokens?.ToString() ?? "null"));
            model.MaxOutputTokens = dto.MaxOutputTokens;

            model.UpdatedAt = DateTime.UtcNow;

            // Track if parameters were changed
            bool parametersChanged = dto.ModelParameters != null;

            var updatedModel = await _modelRepository.UpdateModelAsync(model);

            // Publish ModelUpdated event for cache invalidation
            var changedPropertyNames = changes.Count > 0
                ? changes.Select(c => c.Property).ToArray()
                : GetChangedProperties(dto);

            await _eventBus.PublishAsync(new ModelUpdated
            {
                ModelId = updatedModel.Id,
                ModelName = updatedModel.Name,
                ModelSeriesId = updatedModel.ModelSeriesId,
                ChangeType = "Updated",
                ParametersChanged = parametersChanged,
                ChangedProperties = changedPropertyNames
            });

            if (changes.Count > 0)
            {
                LogAdminAuditWithChanges("Model", updatedModel.Id, changes,
                    $"Name: {LoggingSanitizer.S(updatedModel.Name)}");
            }
            else
            {
                LogAdminAudit("Updated", "Model", updatedModel.Id,
                    $"Name: {LoggingSanitizer.S(updatedModel.Name)}, no value changes detected");
            }
            AdminOperationsMetricsService.RecordConfigurationChange("model", "update");

            return Ok(updatedModel.ToDto());
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
        public async Task<IActionResult> DeleteModel(int id)
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

            LogAdminAudit("Deleted", "Model", id);
            AdminOperationsMetricsService.RecordConfigurationChange("model", "delete");

            return NoContent();
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
            if (dto.SupportsSpeechToText.HasValue) changedProps.Add("SupportsSpeechToText");
            if (dto.SupportsTextToSpeech.HasValue) changedProps.Add("SupportsTextToSpeech");
            if (dto.SupportsRerank.HasValue) changedProps.Add("SupportsRerank");
            if (dto.SupportsEmbeddings.HasValue) changedProps.Add("SupportsEmbeddings");
            if (dto.MaxInputTokens.HasValue) changedProps.Add("MaxInputTokens");
            if (dto.MaxOutputTokens.HasValue) changedProps.Add("MaxOutputTokens");

            return changedProps.ToArray();
        }
    }
}
