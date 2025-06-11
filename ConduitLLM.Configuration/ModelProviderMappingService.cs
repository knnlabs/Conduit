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
                _logger.LogInformation("Adding mapping: {ModelAlias}", mapping.ModelAlias);

                // Get the provider credential
                var credential = await _credentialRepository.GetByProviderNameAsync(mapping.ProviderName);
                if (credential == null)
                {
                    throw new InvalidOperationException($"Provider credentials not found for provider: {mapping.ProviderName}");
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
                _logger.LogError(ex, "Error adding mapping for model alias {ModelAlias}", mapping.ModelAlias);
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
                _logger.LogInformation("Getting mapping by model alias: {ModelAlias}", modelAlias);
                var entity = await _repository.GetByModelNameAsync(modelAlias);
                return ModelProviderMappingMapper.ToDto(entity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting mapping for model alias {ModelAlias}", modelAlias);
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
                _logger.LogInformation("Updating mapping: {ModelAlias}", mapping.ModelAlias);

                // Get the existing entity
                var existingEntity = await _repository.GetByModelNameAsync(mapping.ModelAlias);
                if (existingEntity == null)
                {
                    throw new InvalidOperationException($"Mapping not found for model alias: {mapping.ModelAlias}");
                }

                // Get the provider credential
                var credential = await _credentialRepository.GetByProviderNameAsync(mapping.ProviderName);
                if (credential == null)
                {
                    throw new InvalidOperationException($"Provider credentials not found for provider: {mapping.ProviderName}");
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
                _logger.LogError(ex, "Error updating mapping for model alias {ModelAlias}", mapping.ModelAlias);
                throw;
            }
        }
    }
}
