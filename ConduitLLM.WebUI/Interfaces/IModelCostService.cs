using ConfigDTO = ConduitLLM.Configuration.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ConduitLLM.WebUI.Interfaces
{
    /// <summary>
    /// Interface for managing model cost information
    /// </summary>
    public interface IModelCostService
    {
        /// <summary>
        /// Gets all model costs.
        /// </summary>
        /// <returns>A collection of model costs.</returns>
        Task<IEnumerable<ConfigDTO.ModelCostDto>> GetAllModelCostsAsync();

        /// <summary>
        /// Gets a model cost by ID.
        /// </summary>
        /// <param name="id">The ID of the model cost.</param>
        /// <returns>The model cost, or null if not found.</returns>
        Task<ConfigDTO.ModelCostDto?> GetModelCostByIdAsync(int id);

        /// <summary>
        /// Gets a model cost by pattern.
        /// </summary>
        /// <param name="modelIdPattern">The model ID pattern.</param>
        /// <returns>The model cost, or null if not found.</returns>
        Task<ConfigDTO.ModelCostDto?> GetModelCostByPatternAsync(string modelIdPattern);

        /// <summary>
        /// Creates a new model cost.
        /// </summary>
        /// <param name="modelCost">The model cost to create.</param>
        /// <returns>The created model cost.</returns>
        Task<ConfigDTO.ModelCostDto?> CreateModelCostAsync(ConfigDTO.CreateModelCostDto modelCost);

        /// <summary>
        /// Updates a model cost.
        /// </summary>
        /// <param name="id">The ID of the model cost to update.</param>
        /// <param name="modelCost">The updated model cost.</param>
        /// <returns>The updated model cost, or null if the update failed.</returns>
        Task<ConfigDTO.ModelCostDto?> UpdateModelCostAsync(int id, ConfigDTO.UpdateModelCostDto modelCost);

        /// <summary>
        /// Calculates the cost for a request.
        /// </summary>
        /// <param name="modelId">The model ID.</param>
        /// <param name="inputTokens">The number of input tokens.</param>
        /// <param name="outputTokens">The number of output tokens.</param>
        /// <returns>The calculated cost.</returns>
        Task<decimal> CalculateCostAsync(string modelId, int inputTokens, int outputTokens);
    }
}