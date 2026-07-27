using ConduitLLM.Core.Extensions;
using ConduitLLM.Admin.Endpoints;
using ConduitLLM.Admin.Extensions;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Extensions;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Services;
using ConduitLLM.Functions.Utilities;

using ConduitLLM.Configuration.Messaging;

using Microsoft.EntityFrameworkCore;

namespace ConduitLLM.Admin.Services
{
    /// <summary>
    /// Service implementation for managing model costs through the Admin API
    /// </summary>
    public partial class AdminModelCostService : EventPublishingServiceBase, IAdminModelCostService
    {
        private readonly IModelCostRepository _modelCostRepository;
        private readonly IRequestLogRepository _requestLogRepository;
        private readonly ILogger<AdminModelCostService> _logger;
        private readonly IDbContextFactory<ConduitDbContext> _dbContextFactory;

        /// <summary>
        /// Initializes a new instance of the AdminModelCostService
        /// </summary>
        /// <param name="modelCostRepository">The model cost repository</param>
        /// <param name="requestLogRepository">The request log repository</param>
        /// <param name="dbContextFactory">The database context factory</param>
        /// <param name="eventBus">Optional event bus (null if not configured)</param>
        /// <param name="logger">The logger</param>
        public AdminModelCostService(
            IModelCostRepository modelCostRepository,
            IRequestLogRepository requestLogRepository,
            IDbContextFactory<ConduitDbContext> dbContextFactory,
            ILogger<AdminModelCostService> logger,
            IEventBus? eventBus = null)
            : base(eventBus, logger)
        {
            _modelCostRepository = modelCostRepository ?? throw new ArgumentNullException(nameof(modelCostRepository));
            _requestLogRepository = requestLogRepository ?? throw new ArgumentNullException(nameof(requestLogRepository));
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<ModelCostDto> CreateModelCostAsync(CreateModelCostDto modelCost)
        {
            if (modelCost == null)
            {
                throw new ArgumentNullException(nameof(modelCost));
            }

            try
            {
                ModelPricingConfigurationValidator.Validate(
                    modelCost.PricingModel,
                    StructuredJson.SerializeObject(modelCost.PricingConfiguration));

                // Check if a model cost with the same name already exists
                var existingModelCost = await _modelCostRepository.GetByCostNameAsync(modelCost.CostName);
                if (existingModelCost != null)
                {
                    throw new InvalidOperationException($"A model cost with name '{modelCost.CostName}' already exists");
                }

                // Convert DTO to entity
                var modelCostEntity = modelCost.ToEntity();

                // Save to database
                var id = await _modelCostRepository.CreateAsync(modelCostEntity);

                // Update ModelProviderTypeAssociations to reference this cost if provided
                if (modelCost.ModelProviderTypeAssociationIds != null && modelCost.ModelProviderTypeAssociationIds.Any())
                {
                    using var dbContext = await _dbContextFactory.CreateDbContextAsync();
                    
                    // Find the ModelProviderTypeAssociations by their IDs and update their ModelCostId
                    var associations = await dbContext.ModelProviderTypeAssociations
                        .Where(mpta => modelCost.ModelProviderTypeAssociationIds.Contains(mpta.Id))
                        .ToListAsync();
                    
                    foreach (var association in associations)
                    {
                        association.ModelCostId = id;
                    }
                    
                    await dbContext.SaveChangesAsync();
                }

                // Get the created model cost with mappings
                var createdModelCost = ConduitLLM.Core.Utilities.ReadBackGuard.RequireCreated(
                    await _modelCostRepository.GetByIdAsync(id),
                    "model cost",
                    id);

                // Publish ModelCostChanged event for cache invalidation and cross-service coordination
                await PublishEventAsync(
                    new ModelCostChanged
                    {
                        ModelCostId = createdModelCost.Id,
                        CostName = createdModelCost.CostName,
                        ChangeType = "Created",
                        ChangedProperties = new[] { "Created" },
                        CorrelationId = Guid.NewGuid().ToString()
                    },
                    "CreateModelCost");

                _logger.LogInformation("Created model cost with name '{CostName}'", LoggingSanitizer.S(modelCost.CostName));
                return createdModelCost.ToDto();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating model cost with name '{CostName}'", LoggingSanitizer.S(modelCost.CostName));
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<bool> DeleteModelCostAsync(int id)
        {
            try
            {
                // Get model cost info before deletion for event publishing
                var modelCostToDelete = await _modelCostRepository.GetByIdAsync(id);
                
                var result = await _modelCostRepository.DeleteAsync(id);

                if (result)
                {
                    // Publish ModelCostChanged event for cache invalidation and cleanup
                    if (modelCostToDelete != null)
                    {
                        await PublishEventAsync(
                            new ModelCostChanged
                            {
                                ModelCostId = id,
                                CostName = modelCostToDelete.CostName,
                                ChangeType = "Deleted",
                                ChangedProperties = new[] { "Deleted" },
                                CorrelationId = Guid.NewGuid().ToString()
                            },
                            "DeleteModelCost");
                    }
                    
                    _logger.LogInformation("Deleted model cost with ID {Id}",
                id);
                }
                else
                {
                    _logger.LogWarning("Model cost with ID {Id} not found for deletion",
                id);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                "Error deleting model cost with ID {Id}",
                id);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<IEnumerable<ModelCostDto>> GetAllModelCostsAsync()
        {
            try
            {
                var modelCosts = await RepositoryPaginationExtensions.GetAllViaPaginationAsync(
                    _modelCostRepository.GetPaginatedAsync);
                return modelCosts.Select(mc => mc.ToDto()).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                "Error getting all model costs");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<ModelCostDto?> GetModelCostByIdAsync(int id)
        {
            try
            {
                var modelCost = await _modelCostRepository.GetByIdAsync(id);
                return modelCost?.ToDto();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                "Error getting model cost with ID {Id}",
                id);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<ModelCostDto?> GetModelCostByCostNameAsync(string costName)
        {
            if (string.IsNullOrWhiteSpace(costName))
            {
                throw new ArgumentException("Cost name cannot be null or empty", nameof(costName));
            }

            try
            {
                var modelCost = await _modelCostRepository.GetByCostNameAsync(costName);
                return modelCost?.ToDto();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting model cost with name '{CostName}'", LoggingSanitizer.S(costName));
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<IEnumerable<ModelCostOverviewDto>> GetModelCostOverviewAsync(DateTime startDate, DateTime endDate)
        {
            if (startDate > endDate)
            {
                throw new ArgumentException("Start date cannot be after end date", nameof(startDate));
            }

            try
            {
                // Use database-level aggregation instead of loading all logs into memory
                var modelAggregations = await _requestLogRepository.GetAggregatedByModelAsync(startDate, endDate);
                if (modelAggregations.Count == 0)
                {
                    return Enumerable.Empty<ModelCostOverviewDto>();
                }

                return modelAggregations
                    .Where(m => !string.IsNullOrEmpty(m.ModelName))
                    .Select(m => new ModelCostOverviewDto
                    {
                        Model = m.ModelName,
                        RequestCount = m.RequestCount,
                        TotalCost = m.TotalCost,
                        InputTokens = (int)Math.Min(m.InputTokens, int.MaxValue),
                        OutputTokens = (int)Math.Min(m.OutputTokens, int.MaxValue)
                    })
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                "Error getting model cost overview for period {StartDate} to {EndDate}",
                startDate,
                endDate);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<IEnumerable<ModelCostDto>> GetModelCostsByProviderAsync(int providerId)
        {
            try
            {
                var modelCosts = await RepositoryPaginationExtensions.GetAllViaPaginationAsync(
                    _modelCostRepository.GetByProviderPaginatedAsync, providerId);
                return modelCosts.Select(mc => mc.ToDto()).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting model costs for provider {ProviderId}", providerId);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<ModelCostDto?> UpdateModelCostAsync(int id, UpdateModelCostDto modelCost)
        {
            if (modelCost == null)
            {
                throw new ArgumentNullException(nameof(modelCost));
            }

            try
            {
                // Get existing model cost
                var existingModelCost = await _modelCostRepository.GetByIdAsync(id);
                if (existingModelCost == null)
                {
                    _logger.LogWarning("Model cost with ID {Id} not found",
                id);
                    return null;
                }

                JsonMergePatchState.TryGetPatchedProperty(
                    modelCost,
                    nameof(modelCost.PricingModel),
                    existingModelCost.PricingModel,
                    out var effectivePricingModel);
                JsonMergePatchState.TryGetPatchedProperty(
                    modelCost,
                    nameof(modelCost.PricingConfiguration),
                    StructuredJson.ParseObject(existingModelCost.PricingConfiguration),
                    out Dictionary<string, System.Text.Json.JsonElement>? effectivePricingConfigurationObject);
                var effectivePricingConfiguration =
                    StructuredJson.SerializeObject(effectivePricingConfigurationObject);
                ModelPricingConfigurationValidator.Validate(
                    effectivePricingModel,
                    effectivePricingConfiguration);

                // Check if the cost name is being changed and a model cost with the new name already exists
                JsonMergePatchState.TryGetPatchedProperty(
                    modelCost,
                    nameof(modelCost.CostName),
                    existingModelCost.CostName,
                    out var effectiveCostName);
                if (string.IsNullOrWhiteSpace(effectiveCostName))
                {
                    throw new InvalidOperationException("costName cannot be null or empty.");
                }
                JsonMergePatchState.TryGetPatchedProperty(
                    modelCost,
                    nameof(modelCost.ModelType),
                    existingModelCost.ModelType,
                    out var effectiveModelType);
                if (string.IsNullOrWhiteSpace(effectiveModelType))
                {
                    throw new InvalidOperationException("modelType cannot be null or empty.");
                }
                if (existingModelCost.CostName != effectiveCostName)
                {
                    var nameExists = await _modelCostRepository.GetByCostNameAsync(effectiveCostName);
                    if (nameExists != null && nameExists.Id != id)
                    {
                        throw new InvalidOperationException($"Another model cost with name '{effectiveCostName}' already exists");
                    }
                }

                var changedProperties = new List<string>();
                ApplyPatch(modelCost, nameof(modelCost.CostName), existingModelCost.CostName,
                    value => existingModelCost.CostName = value, changedProperties);
                ApplyPatch(modelCost, nameof(modelCost.PricingModel), existingModelCost.PricingModel,
                    value => existingModelCost.PricingModel = value, changedProperties);
                if (JsonMergePatchState.IsDefined(modelCost, nameof(modelCost.PricingConfiguration)))
                {
                    SetPatchedValue(
                        nameof(modelCost.PricingConfiguration),
                        existingModelCost.PricingConfiguration,
                        effectivePricingConfiguration,
                        value => existingModelCost.PricingConfiguration = value,
                        changedProperties);
                }
                ApplyPatch(modelCost, nameof(modelCost.ModelType), existingModelCost.ModelType,
                    value => existingModelCost.ModelType = value, changedProperties);
                ApplyPatch(modelCost, nameof(modelCost.IsActive), existingModelCost.IsActive,
                    value => existingModelCost.IsActive = value, changedProperties);
                ApplyPatch(modelCost, nameof(modelCost.Priority), existingModelCost.Priority,
                    value => existingModelCost.Priority = value, changedProperties);
                ApplyPatch(modelCost, nameof(modelCost.Description), existingModelCost.Description,
                    value => existingModelCost.Description = value, changedProperties);
                ApplyPatch(modelCost, nameof(modelCost.InputCostPerMillionTokens), existingModelCost.InputCostPerMillionTokens,
                    value => existingModelCost.InputCostPerMillionTokens = value, changedProperties);
                ApplyPatch(modelCost, nameof(modelCost.OutputCostPerMillionTokens), existingModelCost.OutputCostPerMillionTokens,
                    value => existingModelCost.OutputCostPerMillionTokens = value, changedProperties);
                ApplyPatch(modelCost, nameof(modelCost.ReasoningCostPerMillionTokens), existingModelCost.ReasoningCostPerMillionTokens,
                    value => existingModelCost.ReasoningCostPerMillionTokens = value, changedProperties);
                ApplyPatch(modelCost, nameof(modelCost.EmbeddingCostPerMillionTokens), existingModelCost.EmbeddingCostPerMillionTokens,
                    value => existingModelCost.EmbeddingCostPerMillionTokens = value, changedProperties);
                ApplyPatch(modelCost, nameof(modelCost.BatchProcessingMultiplier), existingModelCost.BatchProcessingMultiplier,
                    value => existingModelCost.BatchProcessingMultiplier = value, changedProperties);
                ApplyPatch(modelCost, nameof(modelCost.SupportsBatchProcessing), existingModelCost.SupportsBatchProcessing,
                    value => existingModelCost.SupportsBatchProcessing = value, changedProperties);
                ApplyPatch(modelCost, nameof(modelCost.CachedInputCostPerMillionTokens), existingModelCost.CachedInputCostPerMillionTokens,
                    value => existingModelCost.CachedInputCostPerMillionTokens = value, changedProperties);
                ApplyPatch(modelCost, nameof(modelCost.CachedInputWriteCostPerMillionTokens), existingModelCost.CachedInputWriteCostPerMillionTokens,
                    value => existingModelCost.CachedInputWriteCostPerMillionTokens = value, changedProperties);
                ApplyPatch(modelCost, nameof(modelCost.CostPerSearchUnit), existingModelCost.CostPerSearchUnit,
                    value => existingModelCost.CostPerSearchUnit = value, changedProperties);
                ApplyPatch(modelCost, nameof(modelCost.AudioCostPerMinute), existingModelCost.AudioCostPerMinute,
                    value => existingModelCost.AudioCostPerMinute = value, changedProperties);
                ApplyPatch(modelCost, nameof(modelCost.AudioCostPerThousandCharacters), existingModelCost.AudioCostPerThousandCharacters,
                    value => existingModelCost.AudioCostPerThousandCharacters = value, changedProperties);
                existingModelCost.UpdatedAt = DateTime.UtcNow;

                // Save changes
                var result = await _modelCostRepository.UpdateAsync(existingModelCost);

                if (JsonMergePatchState.IsDefined(
                        modelCost,
                        nameof(modelCost.ModelProviderTypeAssociationIds)))
                {
                    var associationIds = modelCost.ModelProviderTypeAssociationIds ?? [];
                    using var dbContext = await _dbContextFactory.CreateDbContextAsync();

                    // Clear existing associations for this cost
                    var existingAssociations = await dbContext.ModelProviderTypeAssociations
                        .Where(mpta => mpta.ModelCostId == id)
                        .ToListAsync();

                    foreach (var association in existingAssociations)
                    {
                        association.ModelCostId = null;
                    }

                    // Set new associations
                    var newAssociations = await dbContext.ModelProviderTypeAssociations
                        .Where(mpta => associationIds.Contains(mpta.Id))
                        .ToListAsync();

                    foreach (var association in newAssociations)
                    {
                        association.ModelCostId = id;
                    }

                    await dbContext.SaveChangesAsync();

                    // Publish an event when the set of associated models changed so caches
                    // keyed by model identifier are invalidated too
                    if (!existingAssociations.Select(a => a.Id).OrderBy(id => id)
                            .SequenceEqual(newAssociations.Select(a => a.Id).OrderBy(id => id)))
                    {
                        changedProperties.Add("ModelProviderTypeAssociations");
                    }
                }

                if (result)
                {
                    // Publish ModelCostChanged event for cache invalidation and cross-service coordination
                    if (changedProperties.Any())
                    {
                        await PublishEventAsync(
                            new ModelCostChanged
                            {
                                ModelCostId = id,
                                CostName = existingModelCost.CostName,
                                ChangeType = "Updated",
                                ChangedProperties = changedProperties.ToArray(),
                                CorrelationId = Guid.NewGuid().ToString()
                            },
                            "UpdateModelCost");
                    }
                    
                    _logger.LogInformation("Updated model cost with ID {Id}",
                id);
                }
                else
                {
                    _logger.LogWarning("Failed to update model cost with ID {Id}",
                id);
                }

                return result ? await GetModelCostByIdAsync(id) : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                "Error updating model cost with ID {Id}",
                id);
                throw;
            }
        }

        private static void ApplyPatch<T>(
            UpdateModelCostDto request,
            string propertyName,
            T currentValue,
            Action<T> setter,
            List<string> changedProperties)
        {
            if (JsonMergePatchState.TryGetPatchedProperty(
                    request,
                    propertyName,
                    currentValue,
                    out T patchedValue))
            {
                SetPatchedValue(propertyName, currentValue, patchedValue, setter, changedProperties);
            }
        }

        private static void SetPatchedValue<T>(
            string propertyName,
            T currentValue,
            T patchedValue,
            Action<T> setter,
            List<string> changedProperties)
        {
            if (!EqualityComparer<T>.Default.Equals(currentValue, patchedValue))
            {
                changedProperties.Add(propertyName);
            }
            setter(patchedValue);
        }

    }
}
