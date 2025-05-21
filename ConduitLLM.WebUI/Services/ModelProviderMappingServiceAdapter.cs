using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ConfigDTOs = ConduitLLM.Configuration.DTOs;
using ConduitLLM.WebUI.Interfaces;
using ConduitLLM.WebUI.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConduitLLM.WebUI.Services
{
    /// <summary>
    /// Adapter service for model provider mappings that uses the Admin API
    /// </summary>
    public class ModelProviderMappingServiceAdapter : IModelProviderMappingService
    {
        private readonly IAdminApiClient _adminApiClient;
        private readonly AdminApiOptions _adminApiOptions;
        private readonly ILogger<ModelProviderMappingServiceAdapter> _logger;

        /// <summary>
        /// Initializes a new instance of the ModelProviderMappingServiceAdapter class
        /// </summary>
        public ModelProviderMappingServiceAdapter(
            IAdminApiClient adminApiClient,
            IOptions<AdminApiOptions> adminApiOptions,
            ILogger<ModelProviderMappingServiceAdapter> logger)
        {
            _adminApiClient = adminApiClient ?? throw new ArgumentNullException(nameof(adminApiClient));
            _adminApiOptions = adminApiOptions?.Value ?? throw new ArgumentNullException(nameof(adminApiOptions));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<IEnumerable<ConfigDTOs.ModelProviderMappingDto>> GetAllAsync()
        {
            try
            {
                return await _adminApiClient.GetAllModelProviderMappingsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all model provider mappings from Admin API");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<ConfigDTOs.ModelProviderMappingDto?> GetByIdAsync(int id)
        {
            try
            {
                return await _adminApiClient.GetModelProviderMappingByIdAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting model provider mapping with ID {Id} from Admin API", id);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<ConfigDTOs.ModelProviderMappingDto?> GetByModelIdAsync(string modelId)
        {
            try
            {
                return await _adminApiClient.GetModelProviderMappingByAliasAsync(modelId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting model provider mapping for model ID {ModelId} from Admin API", modelId);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<ConfigDTOs.ModelProviderMappingDto?> CreateAsync(ConfigDTOs.ModelProviderMappingDto mapping)
        {
            try
            {
                var entity = new ConduitLLM.Configuration.Entities.ModelProviderMapping
                {
                    ModelAlias = mapping.ModelId,
                    ProviderCredentialId = int.Parse(mapping.ProviderId),
                    ProviderModelName = mapping.ProviderModelId
                };
                var success = await _adminApiClient.CreateModelProviderMappingAsync(entity);
                if (success)
                {
                    // Return the DTO that was passed in since the API returns bool
                    return mapping;
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating model provider mapping through Admin API");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<ConfigDTOs.ModelProviderMappingDto?> UpdateAsync(ConfigDTOs.ModelProviderMappingDto mapping)
        {
            try
            {
                var entity = new ConduitLLM.Configuration.Entities.ModelProviderMapping
                {
                    Id = mapping.Id,
                    ModelAlias = mapping.ModelId,
                    ProviderCredentialId = int.Parse(mapping.ProviderId),
                    ProviderModelName = mapping.ProviderModelId
                };
                var success = await _adminApiClient.UpdateModelProviderMappingAsync(mapping.Id, entity);
                if (success)
                {
                    // Return the DTO that was passed in since the API returns bool
                    return mapping;
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating model provider mapping through Admin API");
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
                _logger.LogError(ex, "Error deleting model provider mapping through Admin API");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<IEnumerable<ConfigDTOs.ProviderDataDto>> GetProvidersAsync()
        {
            try
            {
                var providers = await _adminApiClient.GetAllProviderCredentialsAsync();
                return providers.Select(p => new ConfigDTOs.ProviderDataDto
                {
                    Id = p.Id,
                    ProviderName = p.ProviderName
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting providers from Admin API");
                throw;
            }
        }
    }
}