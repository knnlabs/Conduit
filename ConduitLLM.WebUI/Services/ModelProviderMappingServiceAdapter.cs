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
    /// Adapter service for model provider mappings that can use either direct repository access or the Admin API
    /// </summary>
    public class ModelProviderMappingServiceAdapter : IModelProviderMappingService
    {
        private readonly ModelProviderMappingService _repositoryService;
        private readonly IAdminApiClient _adminApiClient;
        private readonly AdminApiOptions _adminApiOptions;
        private readonly ILogger<ModelProviderMappingServiceAdapter> _logger;

        /// <summary>
        /// Initializes a new instance of the ModelProviderMappingServiceAdapter class
        /// </summary>
        public ModelProviderMappingServiceAdapter(
            ModelProviderMappingService repositoryService,
            IAdminApiClient adminApiClient,
            IOptions<AdminApiOptions> adminApiOptions,
            ILogger<ModelProviderMappingServiceAdapter> logger)
        {
            _repositoryService = repositoryService ?? throw new ArgumentNullException(nameof(repositoryService));
            _adminApiClient = adminApiClient ?? throw new ArgumentNullException(nameof(adminApiClient));
            _adminApiOptions = adminApiOptions?.Value ?? throw new ArgumentNullException(nameof(adminApiOptions));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<IEnumerable<ConfigDTOs.ModelProviderMappingDto>> GetAllAsync()
        {
            if (_adminApiOptions.UseAdminApi)
            {
                try
                {
                    return await _adminApiClient.GetAllModelProviderMappingsAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error getting all model provider mappings from Admin API, falling back to repository");
                    return await _repositoryService.GetAllAsync();
                }
            }

            return await _repositoryService.GetAllAsync();
        }

        /// <inheritdoc />
        public async Task<ConfigDTOs.ModelProviderMappingDto?> GetByIdAsync(int id)
        {
            if (_adminApiOptions.UseAdminApi)
            {
                try
                {
                    return await _adminApiClient.GetModelProviderMappingByIdAsync(id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error getting model provider mapping with ID {Id} from Admin API, falling back to repository", id);
                    return await _repositoryService.GetByIdAsync(id);
                }
            }

            return await _repositoryService.GetByIdAsync(id);
        }

        /// <inheritdoc />
        public async Task<ConfigDTOs.ModelProviderMappingDto?> GetByModelIdAsync(string modelId)
        {
            if (_adminApiOptions.UseAdminApi)
            {
                try
                {
                    return await _adminApiClient.GetModelProviderMappingByAliasAsync(modelId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error getting model provider mapping for model ID {ModelId} from Admin API, falling back to repository", modelId);
                    return await _repositoryService.GetByModelIdAsync(modelId);
                }
            }

            return await _repositoryService.GetByModelIdAsync(modelId);
        }

        /// <inheritdoc />
        public async Task<ConfigDTOs.ModelProviderMappingDto?> CreateAsync(ConfigDTOs.ModelProviderMappingDto mapping)
        {
            if (_adminApiOptions.UseAdminApi)
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
                    _logger.LogError(ex, "Error creating model provider mapping through Admin API, falling back to repository");
                    return await _repositoryService.CreateAsync(mapping);
                }
            }

            return await _repositoryService.CreateAsync(mapping);
        }

        /// <inheritdoc />
        public async Task<ConfigDTOs.ModelProviderMappingDto?> UpdateAsync(ConfigDTOs.ModelProviderMappingDto mapping)
        {
            if (_adminApiOptions.UseAdminApi)
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
                    _logger.LogError(ex, "Error updating model provider mapping through Admin API, falling back to repository");
                    return await _repositoryService.UpdateAsync(mapping);
                }
            }

            return await _repositoryService.UpdateAsync(mapping);
        }

        /// <inheritdoc />
        public async Task<bool> DeleteAsync(int id)
        {
            if (_adminApiOptions.UseAdminApi)
            {
                try
                {
                    return await _adminApiClient.DeleteModelProviderMappingAsync(id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error deleting model provider mapping through Admin API, falling back to repository");
                    return await _repositoryService.DeleteAsync(id);
                }
            }

            return await _repositoryService.DeleteAsync(id);
        }

        /// <inheritdoc />
        public async Task<IEnumerable<ConfigDTOs.ProviderDataDto>> GetProvidersAsync()
        {
            if (_adminApiOptions.UseAdminApi)
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
                    _logger.LogError(ex, "Error getting providers from Admin API, falling back to repository");
                    return await _repositoryService.GetProvidersAsync();
                }
            }

            return await _repositoryService.GetProvidersAsync();
        }
    }
}