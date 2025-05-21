using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.WebUI.Interfaces;
using Microsoft.Extensions.Logging;
using ConfigDTO = ConduitLLM.Configuration.DTOs;

namespace ConduitLLM.WebUI.Services.Providers
{
    /// <summary>
    /// Implementation of IModelProviderMappingService that uses IAdminApiClient to interact with the Admin API
    /// </summary>
    public class ModelProviderMappingServiceProvider : IModelProviderMappingService
    {
        private readonly IAdminApiClient _adminApiClient;
        private readonly ILogger<ModelProviderMappingServiceProvider> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ModelProviderMappingServiceProvider"/> class.
        /// </summary>
        /// <param name="adminApiClient">The Admin API client.</param>
        /// <param name="logger">The logger.</param>
        public ModelProviderMappingServiceProvider(
            IAdminApiClient adminApiClient,
            ILogger<ModelProviderMappingServiceProvider> logger)
        {
            _adminApiClient = adminApiClient ?? throw new ArgumentNullException(nameof(adminApiClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<IEnumerable<ConfigDTO.ModelProviderMappingDto>> GetAllAsync()
        {
            try
            {
                var mappings = await _adminApiClient.GetAllModelProviderMappingsAsync();
                return mappings ?? new List<ConfigDTO.ModelProviderMappingDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all model provider mappings from Admin API");
                return new List<ConfigDTO.ModelProviderMappingDto>();
            }
        }

        /// <inheritdoc />
        public async Task<ConfigDTO.ModelProviderMappingDto?> GetByIdAsync(int id)
        {
            try
            {
                return await _adminApiClient.GetModelProviderMappingByIdAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving model provider mapping with ID {Id} from Admin API", id);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<ConfigDTO.ModelProviderMappingDto?> GetByModelIdAsync(string modelId)
        {
            try
            {
                return await _adminApiClient.GetModelProviderMappingByAliasAsync(modelId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving model provider mapping for model ID {ModelId} from Admin API", modelId);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<ConfigDTO.ModelProviderMappingDto?> CreateAsync(ConfigDTO.ModelProviderMappingDto mapping)
        {
            try
            {
                // Convert to entity
                var entity = ConvertToEntity(mapping);
                
                // Create mapping using Admin API
                bool success = await _adminApiClient.CreateModelProviderMappingAsync(entity);
                
                if (!success)
                {
                    _logger.LogWarning("Failed to create model provider mapping for model {ModelId}", mapping.ModelId);
                    return null;
                }
                
                // Return the created mapping
                return await _adminApiClient.GetModelProviderMappingByAliasAsync(mapping.ModelId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating model provider mapping for model {ModelId}", mapping.ModelId);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<ConfigDTO.ModelProviderMappingDto?> UpdateAsync(ConfigDTO.ModelProviderMappingDto mapping)
        {
            try
            {
                // Convert to entity
                var entity = ConvertToEntity(mapping);
                
                // Update mapping using Admin API
                bool success = await _adminApiClient.UpdateModelProviderMappingAsync(mapping.Id, entity);
                
                if (!success)
                {
                    _logger.LogWarning("Failed to update model provider mapping with ID {Id}", mapping.Id);
                    return null;
                }
                
                // Return the updated mapping
                return await _adminApiClient.GetModelProviderMappingByIdAsync(mapping.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating model provider mapping with ID {Id}", mapping.Id);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<bool> DeleteAsync(int id)
        {
            try
            {
                return await _adminApiClient.DeleteModelProviderMappingAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting model provider mapping with ID {Id}", id);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<IEnumerable<ConfigDTO.ProviderDataDto>> GetProvidersAsync()
        {
            try
            {
                // Get all provider credentials
                var credentials = await _adminApiClient.GetAllProviderCredentialsAsync();
                
                if (credentials == null)
                {
                    return new List<ConfigDTO.ProviderDataDto>();
                }
                
                // Convert to ProviderDataDto
                var providers = new List<ConfigDTO.ProviderDataDto>();
                foreach (var cred in credentials)
                {
                    providers.Add(new ConfigDTO.ProviderDataDto
                    {
                        Id = cred.Id,
                        ProviderName = cred.ProviderName
                        // Note: DisplayName and Description aren't available in ProviderDataDto
                    });
                }
                
                return providers;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving provider data from Admin API");
                return new List<ConfigDTO.ProviderDataDto>();
            }
        }

        private static ModelProviderMapping ConvertToEntity(ConfigDTO.ModelProviderMappingDto dto)
        {
            return new ModelProviderMapping
            {
                Id = dto.Id,
                ModelAlias = dto.ModelId, // Use ModelId as ModelAlias
                ProviderModelName = dto.ProviderModelId, // Use ProviderModelId as ProviderModelName
                ProviderCredentialId = 0, // This needs to be set properly, but we don't have enough info in the DTO
                IsEnabled = dto.IsEnabled,
                MaxContextTokens = dto.MaxContextLength,
                CreatedAt = dto.CreatedAt,
                UpdatedAt = dto.UpdatedAt
                // Other fields not available in the DTO or entity
            };
        }
    }
}