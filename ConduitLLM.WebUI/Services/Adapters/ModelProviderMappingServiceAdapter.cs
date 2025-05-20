// Import with alias to avoid ambiguous references
using ConfigDTO = ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.WebUI.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ConduitLLM.WebUI.Services.Adapters
{
    /// <summary>
    /// Adapter that implements <see cref="IModelProviderMappingService"/> using the Admin API client.
    /// </summary>
    public class ModelProviderMappingServiceAdapter : IModelProviderMappingService
    {
        private readonly IAdminApiClient _adminApiClient;
        private readonly ILogger<ModelProviderMappingServiceAdapter> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ModelProviderMappingServiceAdapter"/> class.
        /// </summary>
        /// <param name="adminApiClient">The Admin API client.</param>
        /// <param name="logger">The logger.</param>
        public ModelProviderMappingServiceAdapter(
            IAdminApiClient adminApiClient,
            ILogger<ModelProviderMappingServiceAdapter> logger)
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
                return mappings ?? Enumerable.Empty<ConfigDTO.ModelProviderMappingDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all model provider mappings from Admin API");
                throw;
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
                _logger.LogError(ex, "Error retrieving model provider mapping {Id} from Admin API", id);
                throw;
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
                _logger.LogError(ex, "Error retrieving model provider mapping for model {ModelId} from Admin API", modelId);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<ConfigDTO.ModelProviderMappingDto?> CreateAsync(ConfigDTO.ModelProviderMappingDto mapping)
        {
            try
            {
                // Convert DTO to entity for the API client
                // We need to convert DTO to entity for the API client
                var entity = new ModelProviderMapping
                {
                    ModelAlias = mapping.ModelId,
                    ProviderModelName = mapping.ProviderModelId,
                    ProviderCredentialId = int.TryParse(mapping.ProviderId, out int providerId) ? providerId : 0,
                    IsEnabled = mapping.IsEnabled,
                    MaxContextTokens = mapping.MaxContextLength
                };
                var created = await _adminApiClient.CreateModelProviderMappingAsync(entity);
                if (created)
                {
                    // Try to retrieve the created mapping by model ID
                    return await _adminApiClient.GetModelProviderMappingByAliasAsync(mapping.ModelId);
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating model provider mapping from Admin API");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<ConfigDTO.ModelProviderMappingDto?> UpdateAsync(ConfigDTO.ModelProviderMappingDto mapping)
        {
            try
            {
                // We need to convert DTO to entity for the API client
                var entity = new ModelProviderMapping
                {
                    Id = mapping.Id,
                    ModelAlias = mapping.ModelId,
                    ProviderModelName = mapping.ProviderModelId, 
                    ProviderCredentialId = int.TryParse(mapping.ProviderId, out int providerId) ? providerId : 0,
                    IsEnabled = mapping.IsEnabled,
                    MaxContextTokens = mapping.MaxContextLength
                };
                var updated = await _adminApiClient.UpdateModelProviderMappingAsync(mapping.Id, entity);
                if (updated)
                {
                    // Return the updated mapping
                    return mapping;
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating model provider mapping from Admin API");
                throw;
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
                _logger.LogError(ex, "Error deleting model provider mapping {Id} from Admin API", id);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<IEnumerable<ConfigDTO.ProviderDataDto>> GetProvidersAsync()
        {
            try
            {
                var providers = await _adminApiClient.GetAllProviderCredentialsAsync();
                // Convert provider credentials to provider data DTOs
                return providers.Select(p => new ConfigDTO.ProviderDataDto
                {
                    Id = p.Id,  // No need to convert to string
                    ProviderName = p.ProviderName 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving providers from Admin API");
                throw;
            }
        }

    }
}