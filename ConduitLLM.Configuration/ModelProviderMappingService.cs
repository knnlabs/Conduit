using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;

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
        private readonly IProviderRepository _providerRepository;

        public ModelProviderMappingService(
            ILogger<ModelProviderMappingService> logger,
            IModelProviderMappingRepository repository,
            IProviderRepository providerRepository)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _providerRepository = providerRepository ?? throw new ArgumentNullException(nameof(providerRepository));
        }

        public async Task AddMappingAsync(Entities.ModelProviderMapping mapping)
        {
            if (mapping == null)
            {
                throw new ArgumentNullException(nameof(mapping));
            }

            try
            {
_logger.LogInformation("Adding mapping: {ModelAlias}", mapping.ModelAlias.Replace(Environment.NewLine, ""));

                // Get the provider credential
                Provider? credential = null;
                
                // Prefer ProviderId if available
                if (mapping.ProviderId > 0)
                {
                    credential = await _providerRepository.GetByIdAsync(mapping.ProviderId);
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

                // Set the provider credential ID
                mapping.ProviderId = credential.Id;

                await _repository.CreateAsync(mapping);
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

        public async Task<List<Entities.ModelProviderMapping>> GetAllMappingsAsync()
        {
            try
            {
                _logger.LogInformation("Getting all model-provider mappings");
                return await _repository.GetAllAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all model-provider mappings");
                throw;
            }
        }

        public async Task<Entities.ModelProviderMapping?> GetMappingByIdAsync(int id)
        {
            try
            {
                _logger.LogInformation("Getting mapping by ID: {Id}", id);
                return await _repository.GetByIdAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting mapping with ID {Id}", id);
                throw;
            }
        }

        public async Task<Entities.ModelProviderMapping?> GetMappingByModelAliasAsync(string modelAlias)
        {
            if (string.IsNullOrEmpty(modelAlias))
            {
                throw new ArgumentException("Model alias cannot be null or empty", nameof(modelAlias));
            }

            try
            {
_logger.LogInformation("Getting mapping by model alias: {ModelAlias}", modelAlias.Replace(Environment.NewLine, ""));
                return await _repository.GetByModelNameAsync(modelAlias);
            }
            catch (Exception ex)
            {
_logger.LogError(ex, "Error getting mapping for model alias {ModelAlias}".Replace(Environment.NewLine, ""), modelAlias.Replace(Environment.NewLine, ""));
                throw;
            }
        }

        public async Task UpdateMappingAsync(Entities.ModelProviderMapping mapping)
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
                Provider? credential = null;
                
                // Prefer ProviderId if available
                if (mapping.ProviderId > 0)
                {
                    credential = await _providerRepository.GetByIdAsync(mapping.ProviderId);
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
                existingEntity.ModelAlias = mapping.ModelAlias;
                existingEntity.ProviderModelId = mapping.ProviderModelId;
                existingEntity.ProviderId = credential.Id;
                existingEntity.IsEnabled = mapping.IsEnabled;
                existingEntity.ModelId = mapping.ModelId;
                existingEntity.MaxContextTokensOverride = mapping.MaxContextTokensOverride;
                existingEntity.ProviderVariation = mapping.ProviderVariation;
                existingEntity.QualityScore = mapping.QualityScore;
                existingEntity.IsDefault = mapping.IsDefault;
                existingEntity.DefaultCapabilityType = mapping.DefaultCapabilityType;

                await _repository.UpdateAsync(existingEntity);
            }
            catch (Exception ex)
            {
_logger.LogError(ex, "Error updating mapping for model alias {ModelAlias}".Replace(Environment.NewLine, ""), mapping.ModelAlias.Replace(Environment.NewLine, ""));
                throw;
            }
        }

        public async Task<(bool success, string? errorMessage, Entities.ModelProviderMapping? createdMapping)> ValidateAndCreateMappingAsync(Entities.ModelProviderMapping mapping)
        {
            if (mapping == null)
            {
                return (false, "Mapping cannot be null", null);
            }

            try
            {
                // Validate that the provider exists
                Provider? provider = null;
                
                // Prefer ProviderId if available
                if (mapping.ProviderId > 0)
                {
                    provider = await _providerRepository.GetByIdAsync(mapping.ProviderId);
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

        public async Task<(bool success, string? errorMessage)> ValidateAndUpdateMappingAsync(int id, Entities.ModelProviderMapping mapping)
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
                    var provider = await _providerRepository.GetByIdAsync(mapping.ProviderId);
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
                var provider = await _providerRepository.GetByIdAsync(providerId);
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
                var providers = await _providerRepository.GetAllAsync();
                return providers.Select(p => (p.Id, p.ProviderName)).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available providers");
                return new List<(int, string)>();
            }
        }
    }
}
