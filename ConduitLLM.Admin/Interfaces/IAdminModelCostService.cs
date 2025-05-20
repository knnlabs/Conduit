using ConduitLLM.Configuration.DTOs;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ConduitLLM.Admin.Interfaces
{
    /// <summary>
    /// Service interface for managing model costs through the Admin API
    /// </summary>
    public interface IAdminModelCostService
    {
        /// <summary>
        /// Gets all model costs
        /// </summary>
        /// <returns>List of all model costs</returns>
        Task<IEnumerable<ModelCostDto>> GetAllModelCostsAsync();

        /// <summary>
        /// Gets all model costs for a specific provider
        /// </summary>
        /// <param name="providerName">The name of the provider</param>
        /// <returns>List of model costs for the specified provider</returns>
        Task<IEnumerable<ModelCostDto>> GetModelCostsByProviderAsync(string providerName);

        /// <summary>
        /// Gets a model cost by ID
        /// </summary>
        /// <param name="id">The ID of the model cost to get</param>
        /// <returns>The model cost, or null if not found</returns>
        Task<ModelCostDto?> GetModelCostByIdAsync(int id);

        /// <summary>
        /// Gets a model cost by model ID pattern
        /// </summary>
        /// <param name="modelIdPattern">The model ID pattern</param>
        /// <returns>The model cost, or null if not found</returns>
        Task<ModelCostDto?> GetModelCostByPatternAsync(string modelIdPattern);

        /// <summary>
        /// Creates a new model cost
        /// </summary>
        /// <param name="modelCost">The model cost to create</param>
        /// <returns>The created model cost</returns>
        Task<ModelCostDto> CreateModelCostAsync(CreateModelCostDto modelCost);

        /// <summary>
        /// Updates a model cost
        /// </summary>
        /// <param name="modelCost">The model cost to update</param>
        /// <returns>True if update was successful, false if the model cost was not found</returns>
        Task<bool> UpdateModelCostAsync(UpdateModelCostDto modelCost);

        /// <summary>
        /// Deletes a model cost
        /// </summary>
        /// <param name="id">The ID of the model cost to delete</param>
        /// <returns>True if deletion was successful, false if the model cost was not found</returns>
        Task<bool> DeleteModelCostAsync(int id);

        /// <summary>
        /// Gets model cost overview data for a specific time period
        /// </summary>
        /// <param name="startDate">The start date for the period (inclusive)</param>
        /// <param name="endDate">The end date for the period (inclusive)</param>
        /// <returns>List of model cost overview data</returns>
        Task<IEnumerable<ModelCostOverviewDto>> GetModelCostOverviewAsync(DateTime startDate, DateTime endDate);
        
        /// <summary>
        /// Imports model costs from a list of DTOs
        /// </summary>
        /// <param name="modelCosts">The list of model costs to import</param>
        /// <returns>The number of model costs imported</returns>
        Task<int> ImportModelCostsAsync(IEnumerable<CreateModelCostDto> modelCosts);
    }
}