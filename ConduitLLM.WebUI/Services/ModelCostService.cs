using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ConfigDTO = ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.WebUI.Interfaces;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.WebUI.Services
{
    /// <summary>
    /// Service implementation for managing model costs using direct repository access.
    /// </summary>
    public class ModelCostService : IModelCostService
    {
        private readonly IModelCostRepository _repository;
        private readonly ILogger<ModelCostService> _logger;

        /// <summary>
        /// Initializes a new instance of the ModelCostService.
        /// </summary>
        /// <param name="repository">The model cost repository.</param>
        /// <param name="logger">The logger.</param>
        public ModelCostService(IModelCostRepository repository, ILogger<ModelCostService> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<IEnumerable<ConfigDTO.ModelCostDto>> GetAllModelCostsAsync()
        {
            var costs = await _repository.GetAllAsync();
            return costs.Select(ToDto);
        }

        /// <inheritdoc />
        public async Task<ConfigDTO.ModelCostDto?> GetModelCostByIdAsync(int id)
        {
            var cost = await _repository.GetByIdAsync(id);
            return cost != null ? ToDto(cost) : null;
        }

        /// <inheritdoc />
        public async Task<ConfigDTO.ModelCostDto?> GetModelCostByPatternAsync(string modelIdPattern)
        {
            // This would need to be implemented in the repository
            var costs = await _repository.GetAllAsync();
            var cost = costs.FirstOrDefault(c => c.ModelIdPattern == modelIdPattern);
            return cost != null ? ToDto(cost) : null;
        }

        /// <inheritdoc />
        public async Task<ConfigDTO.ModelCostDto?> CreateModelCostAsync(ConfigDTO.CreateModelCostDto modelCost)
        {
            var entity = new ModelCost
            {
                ModelIdPattern = modelCost.ModelIdPattern,
                InputTokenCost = modelCost.InputTokenCost,
                OutputTokenCost = modelCost.OutputTokenCost,
                EmbeddingTokenCost = modelCost.EmbeddingTokenCost,
                ImageCostPerImage = modelCost.ImageCostPerImage
            };

            var createdId = await _repository.CreateAsync(entity);
            if (createdId > 0)
            {
                var createdEntity = await _repository.GetByIdAsync(createdId);
                return createdEntity != null ? ToDto(createdEntity) : null;
            }
            return null;
        }

        /// <inheritdoc />
        public async Task<ConfigDTO.ModelCostDto?> UpdateModelCostAsync(int id, ConfigDTO.UpdateModelCostDto modelCost)
        {
            var existing = await _repository.GetByIdAsync(id);
            if (existing == null)
            {
                _logger.LogWarning($"Model cost with ID {id} not found for update");
                return null;
            }

            existing.ModelIdPattern = modelCost.ModelIdPattern;
            existing.InputTokenCost = modelCost.InputTokenCost;
            existing.OutputTokenCost = modelCost.OutputTokenCost;
            existing.EmbeddingTokenCost = modelCost.EmbeddingTokenCost;
            existing.ImageCostPerImage = modelCost.ImageCostPerImage;

            var updated = await _repository.UpdateAsync(existing);
            if (updated)
            {
                var updatedEntity = await _repository.GetByIdAsync(id);
                return updatedEntity != null ? ToDto(updatedEntity) : null;
            }
            return null;
        }

        /// <inheritdoc />
        public async Task<decimal> CalculateCostAsync(string modelId, int inputTokens, int outputTokens)
        {
            // Get all costs and find the matching one
            var costs = await _repository.GetAllAsync();
            var cost = costs.FirstOrDefault(c => modelId.Contains(c.ModelIdPattern));
            
            if (cost == null)
            {
                _logger.LogWarning($"No cost information found for model {modelId}");
                return 0m;
            }

            // Calculate cost using the actual token costs
            var inputCost = (decimal)inputTokens * cost.InputTokenCost;
            var outputCost = (decimal)outputTokens * cost.OutputTokenCost;
            
            return inputCost + outputCost;
        }

        private static ConfigDTO.ModelCostDto ToDto(ModelCost entity)
        {
            return new ConfigDTO.ModelCostDto
            {
                Id = entity.Id,
                ModelIdPattern = entity.ModelIdPattern,
                InputTokenCost = entity.InputTokenCost,
                OutputTokenCost = entity.OutputTokenCost,
                EmbeddingTokenCost = entity.EmbeddingTokenCost,
                ImageCostPerImage = entity.ImageCostPerImage,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt,
                Description = null,
                Priority = 0
            };
        }
    }
}