using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Extensions;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Utilities;

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

            _logger.LogInformation("Adding mapping: {ModelAlias}", LoggingSanitizer.S(mapping.ModelAlias));

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

        public async Task DeleteMappingAsync(int id)
        {
            _logger.LogInformation("Deleting mapping with ID: {Id}", id);
            await _repository.DeleteAsync(id);
        }

        public async Task<List<Entities.ModelProviderMapping>> GetAllMappingsAsync()
        {
            _logger.LogDebug("Getting all model-provider mappings");
            return await RepositoryPaginationExtensions.GetAllViaPaginationAsync(
                _repository.GetPaginatedAsync);
        }

        public async Task<Entities.ModelProviderMapping?> GetMappingByIdAsync(int id)
        {
            _logger.LogDebug("Getting mapping by ID: {Id}", id);
            return await _repository.GetByIdAsync(id);
        }

        public async Task<Entities.ModelProviderMapping?> GetMappingByModelAliasAsync(string modelAlias)
        {
            if (string.IsNullOrEmpty(modelAlias))
            {
                throw new ArgumentException("Model alias cannot be null or empty", nameof(modelAlias));
            }

            _logger.LogDebug("Getting mapping by model alias: {ModelAlias}", LoggingSanitizer.S(modelAlias));
            return await _repository.GetByModelNameAsync(modelAlias);
        }

        public async Task<List<Entities.ModelProviderMapping>> GetMappingsByModelAliasAsync(string modelAlias)
        {
            if (string.IsNullOrWhiteSpace(modelAlias))
                throw new ArgumentException("Model alias cannot be null or empty", nameof(modelAlias));
            return await _repository.GetAllByModelNameAsync(modelAlias);
        }

        public async Task UpdateMappingAsync(Entities.ModelProviderMapping mapping)
        {
            if (mapping == null)
            {
                throw new ArgumentNullException(nameof(mapping));
            }

            _logger.LogInformation("Updating mapping: {ModelAlias}", LoggingSanitizer.S(mapping.ModelAlias));

            // Get the existing entity
            // An alias may have several provider candidates; update the exact row.
            var existingEntity = await _repository.GetByIdAsync(mapping.Id);
            if (existingEntity == null)
            {
                _logger.LogWarning("Mapping not found for model alias {ModelAlias}", LoggingSanitizer.S(mapping.ModelAlias));
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
            existingEntity.ModelProviderTypeAssociationId = mapping.ModelProviderTypeAssociationId;
            existingEntity.ProviderOptions = mapping.ProviderOptions;
            existingEntity.RoutingPriority = mapping.RoutingPriority;
            existingEntity.RoutingWeight = mapping.RoutingWeight;

            await _repository.UpdateAsync(existingEntity);
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

                var aliasMappings = await GetMappingsByModelAliasAsync(mapping.ModelAlias);
                if (aliasMappings.Any(existing => existing.ProviderId == mapping.ProviderId))
                {
                    return (false, $"A mapping for alias '{mapping.ModelAlias}' and provider {mapping.ProviderId} already exists.", null);
                }
                if (mapping.RoutingWeight is < 0.1m or > 2.0m)
                    return (false, "RoutingWeight must be between 0.1 and 2.0.", null);
                if (aliasMappings.Count > 0 && aliasMappings[0].ModelProviderTypeAssociation?.ModelId is int canonicalModelId)
                {
                    var candidateModelId = await _repository.GetCanonicalModelIdForAssociationAsync(
                        mapping.ModelProviderTypeAssociationId);
                    if (candidateModelId != canonicalModelId)
                        return (false, "All mappings for an alias must reference the same canonical model.", null);
                }

                // Create the mapping
                await AddMappingAsync(mapping);

                // Return the created mapping
                var createdMapping = await GetMappingByIdAsync(mapping.Id);
                return (true, null, createdMapping);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating model provider mapping for alias {ModelAlias}", LoggingSanitizer.S(mapping.ModelAlias));
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

                if (mapping.RoutingWeight is < 0.1m or > 2.0m)
                    return (false, "RoutingWeight must be between 0.1 and 2.0.");
                var aliasMappings = await GetMappingsByModelAliasAsync(mapping.ModelAlias);
                if (aliasMappings.Any(other => other.Id != id && other.ProviderId == mapping.ProviderId))
                    return (false, $"A mapping for alias '{mapping.ModelAlias}' and provider {mapping.ProviderId} already exists.");
                var canonicalIds = aliasMappings.Where(other => other.Id != id)
                    .Select(other => other.ModelProviderTypeAssociation?.ModelId).Where(modelId => modelId.HasValue).Distinct().ToList();
                if (canonicalIds.Count > 0 && await _repository.GetCanonicalModelIdForAssociationAsync(
                    mapping.ModelProviderTypeAssociationId) != canonicalIds[0])
                    return (false, "All mappings for an alias must reference the same canonical model.");

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
                _logger.LogDebug("Getting all available providers");
                var providers = await RepositoryPaginationExtensions.GetAllViaPaginationAsync(
                    _providerRepository.GetPaginatedAsync);
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
