using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.WebUI.Interfaces;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.WebUI.Services.Adapters
{
    /// <summary>
    /// Adapter that bridges the model provider mapping service interface with the Admin API client
    /// </summary>
    public class ModelProviderMappingServiceAdapter : IModelProviderMappingService
    {
        private readonly IAdminApiClient _adminApiClient;
        private readonly ILogger<ModelProviderMappingServiceAdapter> _logger;

        public ModelProviderMappingServiceAdapter(IAdminApiClient adminApiClient, ILogger<ModelProviderMappingServiceAdapter> logger)
        {
            _adminApiClient = adminApiClient ?? throw new ArgumentNullException(nameof(adminApiClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets all model provider mappings
        /// </summary>
        /// <returns>Collection of model provider mappings</returns>
        public async Task<IEnumerable<ModelProviderMappingDto>> GetAllAsync()
        {
            try
            {
                // Use dynamic to access methods that might not be in the interface
                dynamic dynamicClient = _adminApiClient;
                var mappings = await dynamicClient.GetAllModelProviderMappingsAsync();
                return mappings ?? Enumerable.Empty<ModelProviderMappingDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all model provider mappings");
                throw;
            }
        }

        /// <summary>
        /// Gets a model provider mapping by ID
        /// </summary>
        /// <param name="id">The mapping ID</param>
        /// <returns>The model provider mapping or null if not found</returns>
        public async Task<ModelProviderMappingDto?> GetByIdAsync(int id)
        {
            try
            {
                dynamic dynamicClient = _adminApiClient;
                return await dynamicClient.GetModelProviderMappingByIdAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting model provider mapping by ID {Id}", id);
                return null;
            }
        }

        /// <summary>
        /// Gets model provider mappings by model ID
        /// </summary>
        /// <param name="modelId">The model ID</param>
        /// <returns>The model provider mapping for the model</returns>
        public async Task<ModelProviderMappingDto?> GetByModelIdAsync(string modelId)
        {
            try
            {
                dynamic dynamicClient = _adminApiClient;
                return await dynamicClient.GetModelProviderMappingByAliasAsync(modelId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting model provider mappings by model ID {ModelId}", modelId);
                return null;
            }
        }

        /// <summary>
        /// Creates a new model provider mapping
        /// </summary>
        /// <param name="mapping">The mapping to create</param>
        /// <returns>The created mapping or null if creation failed</returns>
        public async Task<ModelProviderMappingDto?> CreateAsync(ModelProviderMappingDto mapping)
        {
            try
            {
                if (mapping == null)
                {
                    _logger.LogWarning("Attempted to create null model provider mapping");
                    return null;
                }

                // Convert the DTO to entity for the API
                var entity = new ModelProviderMapping
                {
                    ModelAlias = mapping.ModelId,
                    ProviderModelName = mapping.ProviderModelId,
                    ProviderCredentialId = int.Parse(mapping.ProviderId),
                    IsEnabled = mapping.IsEnabled,
                    MaxContextTokens = mapping.MaxContextLength
                };

                dynamic dynamicClient = _adminApiClient;
                var success = await dynamicClient.CreateModelProviderMappingAsync(entity);
                if (!success)
                {
                    return null;
                }

                // Retrieve the created mapping
                return await dynamicClient.GetModelProviderMappingByAliasAsync(mapping.ModelId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating model provider mapping");
                return null;
            }
        }

        /// <summary>
        /// Updates a model provider mapping
        /// </summary>
        /// <param name="mapping">The mapping to update</param>
        /// <returns>The updated mapping or null if update failed</returns>
        public async Task<ModelProviderMappingDto?> UpdateAsync(ModelProviderMappingDto mapping)
        {
            try
            {
                if (mapping == null || mapping.Id == 0)
                {
                    _logger.LogWarning("Attempted to update invalid model provider mapping");
                    return null;
                }

                // Convert the DTO to entity for the API
                var entity = new ModelProviderMapping
                {
                    Id = mapping.Id,
                    ModelAlias = mapping.ModelId,
                    ProviderModelName = mapping.ProviderModelId,
                    ProviderCredentialId = int.Parse(mapping.ProviderId),
                    IsEnabled = mapping.IsEnabled,
                    MaxContextTokens = mapping.MaxContextLength
                };

                dynamic dynamicClient = _adminApiClient;
                var success = await dynamicClient.UpdateModelProviderMappingAsync(mapping.Id, entity);
                if (!success)
                {
                    return null;
                }

                return mapping;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating model provider mapping");
                return null;
            }
        }

        /// <summary>
        /// Deletes a model provider mapping
        /// </summary>
        /// <param name="id">The mapping ID</param>
        /// <returns>True if deleted successfully</returns>
        public async Task<bool> DeleteAsync(int id)
        {
            try
            {
                dynamic dynamicClient = _adminApiClient;
                return await dynamicClient.DeleteModelProviderMappingAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting model provider mapping {Id}", id);
                return false;
            }
        }

        /// <summary>
        /// Gets available providers
        /// </summary>
        /// <returns>Collection of provider data</returns>
        public async Task<IEnumerable<ProviderDataDto>> GetProvidersAsync()
        {
            try
            {
                // Get provider credentials to see available providers
                var credentials = await _adminApiClient.GetAllProviderCredentialsAsync();
                if (credentials == null) return Enumerable.Empty<ProviderDataDto>();

                return credentials.Select(c => new ProviderDataDto
                {
                    Id = c.Id,
                    ProviderName = c.ProviderName
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting providers");
                return Enumerable.Empty<ProviderDataDto>();
            }
        }
    }
}