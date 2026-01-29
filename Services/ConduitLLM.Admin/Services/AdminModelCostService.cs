using ConduitLLM.Core.Extensions;
using ConduitLLM.Admin.Extensions;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Extensions;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Services;

using MassTransit;

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
        /// <param name="publishEndpoint">Optional event publishing endpoint (null if MassTransit not configured)</param>
        /// <param name="logger">The logger</param>
        public AdminModelCostService(
            IModelCostRepository modelCostRepository,
            IRequestLogRepository requestLogRepository,
            IDbContextFactory<ConduitDbContext> dbContextFactory,
            IPublishEndpoint? publishEndpoint,
            ILogger<AdminModelCostService> logger)
            : base(publishEndpoint, logger)
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
                var createdModelCost = await _modelCostRepository.GetByIdAsync(id);
                if (createdModelCost == null)
                {
                    throw new InvalidOperationException($"Failed to retrieve newly created model cost with ID {id}");
                }

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
                // Get request logs for the specified time period
                var logs = await _requestLogRepository.GetByDateRangeAsync(startDate, endDate);
                if (logs == null || !logs.Any())
                {
                    return Enumerable.Empty<ModelCostOverviewDto>();
                }

                // Group by model and aggregate cost data
                var modelGroups = logs
                    .Where(l => !string.IsNullOrEmpty(l.ModelName)) // Filter out logs with no model name
                    .GroupBy(l => l.ModelName)
                    .Select(g => new ModelCostOverviewDto
                    {
                        Model = g.Key ?? "Unknown",
                        RequestCount = g.Count(),
                        TotalCost = g.Sum(l => l.Cost),
                        InputTokens = g.Sum(l => l.InputTokens),
                        OutputTokens = g.Sum(l => l.OutputTokens)
                    })
                    .OrderByDescending(m => m.TotalCost)
                    .ToList();

                return modelGroups;
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
        public async Task<bool> UpdateModelCostAsync(UpdateModelCostDto modelCost)
        {
            if (modelCost == null)
            {
                throw new ArgumentNullException(nameof(modelCost));
            }

            try
            {
                // Get existing model cost
                var existingModelCost = await _modelCostRepository.GetByIdAsync(modelCost.Id);
                if (existingModelCost == null)
                {
                    _logger.LogWarning("Model cost with ID {Id} not found",
                modelCost.Id);
                    return false;
                }

                // Check if the cost name is being changed and a model cost with the new name already exists
                if (existingModelCost.CostName != modelCost.CostName)
                {
                    var nameExists = await _modelCostRepository.GetByCostNameAsync(modelCost.CostName);
                    if (nameExists != null && nameExists.Id != modelCost.Id)
                    {
                        throw new InvalidOperationException($"Another model cost with name '{modelCost.CostName}' already exists");
                    }
                }

                // Track changes for event publishing
                var changedProperties = new List<string>();
                if (existingModelCost.CostName != modelCost.CostName)
                    changedProperties.Add(nameof(modelCost.CostName));
                if (existingModelCost.InputCostPerMillionTokens != modelCost.InputCostPerMillionTokens)
                    changedProperties.Add(nameof(modelCost.InputCostPerMillionTokens));
                if (existingModelCost.OutputCostPerMillionTokens != modelCost.OutputCostPerMillionTokens)
                    changedProperties.Add(nameof(modelCost.OutputCostPerMillionTokens));
                if (existingModelCost.EmbeddingCostPerMillionTokens != modelCost.EmbeddingCostPerMillionTokens)
                    changedProperties.Add(nameof(modelCost.EmbeddingCostPerMillionTokens));

                // Update entity
                existingModelCost.UpdateFrom(modelCost);

                // Save changes
                var result = await _modelCostRepository.UpdateAsync(existingModelCost);

                // Update ModelProviderTypeAssociations if provided
                if (modelCost.ModelProviderTypeAssociationIds != null)
                {
                    using var dbContext = await _dbContextFactory.CreateDbContextAsync();
                    
                    // Clear existing associations for this cost
                    var existingAssociations = await dbContext.ModelProviderTypeAssociations
                        .Where(mpta => mpta.ModelCostId == modelCost.Id)
                        .ToListAsync();
                    
                    foreach (var association in existingAssociations)
                    {
                        association.ModelCostId = null;
                    }
                    
                    // Set new associations
                    var newAssociations = await dbContext.ModelProviderTypeAssociations
                        .Where(mpta => modelCost.ModelProviderTypeAssociationIds.Contains(mpta.Id))
                        .ToListAsync();
                    
                    foreach (var association in newAssociations)
                    {
                        association.ModelCostId = modelCost.Id;
                    }
                    
                    await dbContext.SaveChangesAsync();
                }

                if (result)
                {
                    // Publish ModelCostChanged event for cache invalidation and cross-service coordination
                    if (changedProperties.Any())
                    {
                        await PublishEventAsync(
                            new ModelCostChanged
                            {
                                ModelCostId = modelCost.Id,
                                CostName = existingModelCost.CostName,
                                ChangeType = "Updated",
                                ChangedProperties = changedProperties.ToArray(),
                                CorrelationId = Guid.NewGuid().ToString()
                            },
                            "UpdateModelCost");
                    }
                    
                    _logger.LogInformation("Updated model cost with ID {Id}",
                modelCost.Id);
                }
                else
                {
                    _logger.LogWarning("Failed to update model cost with ID {Id}",
                modelCost.Id);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                "Error updating model cost with ID {Id}",
                modelCost.Id);
                throw;
            }
        }
    }
}
