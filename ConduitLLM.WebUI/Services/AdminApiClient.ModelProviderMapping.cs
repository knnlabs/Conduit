using ConduitLLM.Configuration.DTOs;
using ConduitLLM.WebUI.Interfaces;
using ConfigDTO = ConduitLLM.Configuration.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.Http.Json;
using System.Net;
using System;

namespace ConduitLLM.WebUI.Services
{
    public partial class AdminApiClient : IModelProviderMappingService
    {
        #region IModelProviderMappingService Implementation

        /// <inheritdoc />
        public async Task<IEnumerable<ConfigDTO.ModelProviderMappingDto>> GetAllAsync()
        {
            return await GetAllModelProviderMappingsAsync();
        }

        /// <inheritdoc />
        public async Task<ConfigDTO.ModelProviderMappingDto?> GetByIdAsync(int id)
        {
            return await GetModelProviderMappingByIdAsync(id);
        }

        /// <inheritdoc />
        public async Task<ConfigDTO.ModelProviderMappingDto?> GetByModelIdAsync(string modelId)
        {
            return await GetModelProviderMappingByAliasAsync(modelId);
        }

        /// <inheritdoc />
        public async Task<ConfigDTO.ModelProviderMappingDto?> CreateAsync(ConfigDTO.ModelProviderMappingDto mapping)
        {
            try
            {
                // Convert DTO to Entity for the API call
                var entity = new ConduitLLM.Configuration.Entities.ModelProviderMapping
                {
                    ModelAlias = mapping.ModelId ?? string.Empty,
                    ProviderModelName = mapping.ProviderModelId ?? mapping.ModelId ?? string.Empty,
                    ProviderCredentialId = 1, // Default value - this would need to be set properly in a real implementation
                    IsEnabled = mapping.IsEnabled
                    // Other properties will be set by the API
                };
                
                // Call the API to create the mapping
                var success = await CreateModelProviderMappingAsync(entity);
                
                if (success)
                {
                    // If creation was successful, retrieve the newly created mapping by model alias
                    return await GetModelProviderMappingByAliasAsync(mapping.ModelId ?? string.Empty);
                }
                
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating model provider mapping");
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<ConfigDTO.ModelProviderMappingDto?> UpdateAsync(ConfigDTO.ModelProviderMappingDto mapping)
        {
            try
            {
                if (mapping.Id <= 0)
                {
                    _logger.LogError("Cannot update model provider mapping without a valid ID");
                    return null;
                }
                
                // Convert DTO to Entity for the API call
                var entity = new ConduitLLM.Configuration.Entities.ModelProviderMapping
                {
                    Id = mapping.Id,
                    ModelAlias = mapping.ModelId ?? string.Empty,
                    ProviderModelName = mapping.ProviderModelId ?? mapping.ModelId ?? string.Empty,
                    ProviderCredentialId = 1, // Default value - this would need to be set properly in a real implementation
                    IsEnabled = mapping.IsEnabled
                    // Other properties will be set by the API
                };
                
                // Call the API to update the mapping
                var success = await UpdateModelProviderMappingAsync(mapping.Id, entity);
                
                if (success)
                {
                    // If update was successful, retrieve the updated mapping
                    return await GetModelProviderMappingByIdAsync(mapping.Id);
                }
                
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating model provider mapping with ID {MappingId}", mapping.Id);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<bool> DeleteAsync(int id)
        {
            return await DeleteModelProviderMappingAsync(id);
        }

        /// <inheritdoc />
        public async Task<IEnumerable<ConfigDTO.ProviderDataDto>> GetProvidersAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("api/modelprovidermapping/providers");
                response.EnsureSuccessStatusCode();
                
                var result = await response.Content.ReadFromJsonAsync<IEnumerable<ConfigDTO.ProviderDataDto>>(_jsonOptions);
                return result ?? Enumerable.Empty<ConfigDTO.ProviderDataDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving providers list from Admin API");
                return Enumerable.Empty<ConfigDTO.ProviderDataDto>();
            }
        }

        #endregion
    }
}