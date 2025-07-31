using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Mapping;
using ConduitLLM.Configuration.Repositories;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Configuration
{
    /// <summary>
    /// Service for managing model-provider mappings
    /// </summary>
    public class ModelProviderMappingService : IModelProviderMappingService
    {
        private readonly ILogger<ModelProviderMappingService> _logger;
        private readonly IModelProviderMappingRepository _repository;
        private readonly IProviderCredentialRepository _credentialRepository;

        public ModelProviderMappingService(
            ILogger<ModelProviderMappingService> logger,
            IModelProviderMappingRepository repository,
            IProviderCredentialRepository credentialRepository)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _credentialRepository = credentialRepository ?? throw new ArgumentNullException(nameof(credentialRepository));
        }

        public async Task AddMappingAsync(ModelProviderMapping mapping)
        {
            if (mapping == null)
            {
                throw new ArgumentNullException(nameof(mapping));
            }

            try
            {
_logger.LogInformation("Adding mapping: {ModelAlias}", mapping.ModelAlias.Replace(Environment.NewLine, ""));

                // Get the provider credential
                ProviderCredential? credential = null;
                
                // Prefer ProviderId if available
                if (mapping.ProviderId > 0)
                {
                    credential = await _credentialRepository.GetByIdAsync(mapping.ProviderId);
                    if (credential == null)
                    {
                        _logger.LogWarning("Provider credentials not found for provider ID {ProviderId}", mapping.ProviderId);
                        throw new InvalidOperationException($"Provider credentials not found for provider ID {mapping.ProviderId}");
                    }
                }
                else
                {
                    // ProviderId is required
                    _logger.LogWarning("ProviderId is required for model provider mapping");
                    throw new InvalidOperationException("ProviderId is required for model provider mapping");
                }

                // Convert to entity and set the provider credential ID
                var entity = ModelProviderMappingMapper.ToEntity(mapping);
                if (entity != null)
                {
                    entity.ProviderCredentialId = credential.Id;
                }
                else
                {
                    throw new InvalidOperationException("Failed to convert DTO to entity");
                }

                await _repository.CreateAsync(entity);
            }
            catch (Exception ex)
            {
_logger.LogError(ex, "Error adding mapping for model alias {ModelAlias}".Replace(Environment.NewLine, ""), mapping.ModelAlias.Replace(Environment.NewLine, ""));
                throw;
            }
        }

        public async Task DeleteMappingAsync(int id)
        {
            try
            {
                _logger.LogInformation("Deleting mapping with ID: {Id}", id);
                await _repository.DeleteAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting mapping with ID {Id}", id);
                throw;
            }
        }

        public async Task<List<ModelProviderMapping>> GetAllMappingsAsync()
        {
            try
            {
                _logger.LogInformation("Getting all model-provider mappings");
                var entities = await _repository.GetAllAsync();
                return ModelProviderMappingMapper.ToDtoList(entities);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all model-provider mappings");
                throw;
            }
        }

        public async Task<ModelProviderMapping?> GetMappingByIdAsync(int id)
        {
            try
            {
                _logger.LogInformation("Getting mapping by ID: {Id}", id);
                var entity = await _repository.GetByIdAsync(id);
                return ModelProviderMappingMapper.ToDto(entity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting mapping with ID {Id}", id);
                throw;
            }
        }

        public async Task<ModelProviderMapping?> GetMappingByModelAliasAsync(string modelAlias)
        {
            if (string.IsNullOrEmpty(modelAlias))
            {
                throw new ArgumentException("Model alias cannot be null or empty", nameof(modelAlias));
            }

            try
            {
_logger.LogInformation("Getting mapping by model alias: {ModelAlias}", modelAlias.Replace(Environment.NewLine, ""));
                var entity = await _repository.GetByModelNameAsync(modelAlias);
                return ModelProviderMappingMapper.ToDto(entity);
            }
            catch (Exception ex)
            {
_logger.LogError(ex, "Error getting mapping for model alias {ModelAlias}".Replace(Environment.NewLine, ""), modelAlias.Replace(Environment.NewLine, ""));
                throw;
            }
        }

        public async Task UpdateMappingAsync(ModelProviderMapping mapping)
        {
            if (mapping == null)
            {
                throw new ArgumentNullException(nameof(mapping));
            }

            try
            {
_logger.LogInformation("Updating mapping: {ModelAlias}", mapping.ModelAlias.Replace(Environment.NewLine, ""));

                // Get the existing entity
                var existingEntity = await _repository.GetByModelNameAsync(mapping.ModelAlias);
                if (existingEntity == null)
                {
_logger.LogWarning("Mapping not found for model alias {ModelAlias}", mapping.ModelAlias.Replace(Environment.NewLine, ""));
                    throw new InvalidOperationException("Mapping not found for the specified model alias");
                }

                // Get the provider credential
                ProviderCredential? credential = null;
                
                // Prefer ProviderId if available
                if (mapping.ProviderId > 0)
                {
                    credential = await _credentialRepository.GetByIdAsync(mapping.ProviderId);
                    if (credential == null)
                    {
                        _logger.LogWarning("Provider credentials not found for provider ID {ProviderId}", mapping.ProviderId);
                        throw new InvalidOperationException($"Provider credentials not found for provider ID {mapping.ProviderId}");
                    }
                }
                else
                {
                    // ProviderId is required
                    _logger.LogWarning("ProviderId is required for model provider mapping");
                    throw new InvalidOperationException("ProviderId is required for model provider mapping");
                }

                // Update the entity
                var entity = ModelProviderMappingMapper.ToEntity(mapping, existingEntity);
                if (entity != null)
                {
                    entity.ProviderCredentialId = credential.Id;
                }
                else
                {
                    throw new InvalidOperationException("Failed to convert DTO to entity");
                }

                await _repository.UpdateAsync(entity);
            }
            catch (Exception ex)
            {
_logger.LogError(ex, "Error updating mapping for model alias {ModelAlias}".Replace(Environment.NewLine, ""), mapping.ModelAlias.Replace(Environment.NewLine, ""));
                throw;
            }
        }

        public async Task<(bool success, string? errorMessage, ModelProviderMapping? createdMapping)> ValidateAndCreateMappingAsync(ModelProviderMapping mapping)
        {
            if (mapping == null)
            {
                return (false, "Mapping cannot be null", null);
            }

            try
            {
                // Validate that the provider exists
                ProviderCredential? provider = null;
                
                // Prefer ProviderId if available
                if (mapping.ProviderId > 0)
                {
                    provider = await _credentialRepository.GetByIdAsync(mapping.ProviderId);
                    if (provider == null)
                    {
                        _logger.LogWarning("Provider does not exist with ID {ProviderId}", mapping.ProviderId);
                        return (false, $"Provider does not exist with ID: {mapping.ProviderId}", null);
                    }
                }
                else
                {
                    // ProviderId is required
                    _logger.LogWarning("ProviderId is required for model provider mapping");
                    return (false, "ProviderId is required for model provider mapping", null);
                }

                // Check if a mapping with the same alias already exists
                var existingMapping = await GetMappingByModelAliasAsync(mapping.ModelAlias);
                if (existingMapping != null)
                {
                    _logger.LogWarning("Mapping already exists for model alias {ModelAlias}", mapping.ModelAlias.Replace(Environment.NewLine, ""));
                    return (false, $"A mapping for this model alias already exists: {mapping.ModelAlias}", null);
                }

                // Create the mapping
                await AddMappingAsync(mapping);

                // Return the created mapping
                var createdMapping = await GetMappingByModelAliasAsync(mapping.ModelAlias);
                return (true, null, createdMapping);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating model provider mapping for alias {ModelAlias}", mapping.ModelAlias.Replace(Environment.NewLine, ""));
                return (false, $"An error occurred while creating the model provider mapping: {ex.Message}", null);
            }
        }

        public async Task<(bool success, string? errorMessage)> ValidateAndUpdateMappingAsync(int id, ModelProviderMapping mapping)
        {
            if (mapping == null)
            {
                return (false, "Mapping cannot be null");
            }

            try
            {
                // Check if the mapping exists
                var existingMapping = await GetMappingByIdAsync(id);
                if (existingMapping == null)
                {
                    _logger.LogWarning("Model provider mapping not found for update {MappingId}", id);
                    return (false, $"Model provider mapping not found: {id}");
                }

                // Validate that the provider exists
                if (mapping.ProviderId > 0)
                {
                    var provider = await _credentialRepository.GetByIdAsync(mapping.ProviderId);
                    if (provider == null)
                    {
                        _logger.LogWarning("Provider does not exist {ProviderId}", mapping.ProviderId);
                        return (false, $"Provider does not exist: {mapping.ProviderId}");
                    }
                }
                else
                {
                    _logger.LogWarning("ProviderId is required for model provider mapping");
                    return (false, "ProviderId is required for model provider mapping");
                }

                // Update the mapping
                await UpdateMappingAsync(mapping);
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating model provider mapping with ID {Id}", id);
                return (false, $"An error occurred while updating the model provider mapping: {ex.Message}");
            }
        }


        public async Task<bool> ProviderExistsByIdAsync(int providerId)
        {
            try
            {
                var provider = await _credentialRepository.GetByIdAsync(providerId);
                return provider != null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if provider exists by ID: {ProviderId}", providerId);
                return false;
            }
        }

        public async Task<List<(int Id, string ProviderName)>> GetAvailableProvidersAsync()
        {
            try
            {
                _logger.LogInformation("Getting all available providers");
                var providers = await _credentialRepository.GetAllAsync();
                return providers.Select(p => (p.Id, p.ProviderType.ToString())).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available providers");
                return new List<(int, string)>();
            }
        }
    }
}
