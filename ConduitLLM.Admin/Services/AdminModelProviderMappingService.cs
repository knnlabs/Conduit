using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Services;

using MassTransit;
using Microsoft.Extensions.Logging;

using ConduitLLM.Configuration.Interfaces;
namespace ConduitLLM.Admin.Services;

/// <summary>
/// Service for managing model provider mappings through the Admin API
/// </summary>
public class AdminModelProviderMappingService : EventPublishingServiceBase, IAdminModelProviderMappingService
{
    private readonly IModelProviderMappingRepository _mappingRepository;
    private readonly IProviderRepository _providerRepository;
    private readonly ILogger<AdminModelProviderMappingService> _logger;

    /// <summary>
    /// Initializes a new instance of the AdminModelProviderMappingService class
    /// </summary>
    /// <param name="mappingRepository">The model provider mapping repository</param>
    /// <param name="providerRepository">The provider repository</param>
    /// <param name="publishEndpoint">Optional event publishing endpoint (null if MassTransit not configured)</param>
    /// <param name="logger">The logger</param>
    public AdminModelProviderMappingService(
        IModelProviderMappingRepository mappingRepository,
        IProviderRepository providerRepository,
        IPublishEndpoint? publishEndpoint,
        ILogger<AdminModelProviderMappingService> logger)
        : base(publishEndpoint, logger)
    {
        _mappingRepository = mappingRepository ?? throw new ArgumentNullException(nameof(mappingRepository));
        _providerRepository = providerRepository ?? throw new ArgumentNullException(nameof(providerRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        LogEventPublishingConfiguration(nameof(AdminModelProviderMappingService));
    }

    /// <inheritdoc />
    public async Task<IEnumerable<ModelProviderMapping>> GetAllMappingsAsync()
    {
        _logger.LogInformation("Getting all model provider mappings");
        return await _mappingRepository.GetAllAsync();
    }

    /// <inheritdoc />
    public async Task<ModelProviderMapping?> GetMappingByIdAsync(int id)
    {
        _logger.LogInformation("Getting model provider mapping with ID: {Id}", id);
        return await _mappingRepository.GetByIdAsync(id);
    }

    /// <inheritdoc />
    public async Task<ModelProviderMapping?> GetMappingByModelIdAsync(string modelId)
    {
        _logger.LogInformation("Getting model provider mapping for model ID: {ModelId}", modelId.Replace(Environment.NewLine, ""));
        return await _mappingRepository.GetByModelNameAsync(modelId);
    }

    /// <inheritdoc />
    public async Task<bool> AddMappingAsync(ModelProviderMapping mapping)
    {
        try
        {
            _logger.LogInformation("Adding new model provider mapping for model ID: {ModelId}", mapping.ModelAlias.Replace(Environment.NewLine, ""));

            // Validate provider exists by ID
            var provider = await _providerRepository.GetByIdAsync(mapping.ProviderId);
            if (provider == null)
            {
                _logger.LogWarning("Provider not found with ID: {ProviderId}", mapping.ProviderId);
                return false;
            }

            // Check if a mapping with the same model ID already exists
            var existingMapping = await _mappingRepository.GetByModelNameAsync(mapping.ModelAlias);
            if (existingMapping != null)
            {
                _logger.LogWarning("A mapping for model ID already exists: {ModelId}", mapping.ModelAlias.Replace(Environment.NewLine, ""));
                return false;
            }

            // Set timestamps
            mapping.CreatedAt = DateTime.UtcNow;
            mapping.UpdatedAt = DateTime.UtcNow;

            // Add the mapping
            await _mappingRepository.CreateAsync(mapping);
            
            // Publish ModelMappingChanged event for creation
            await PublishEventAsync(
                new ModelMappingChanged
                {
                    MappingId = mapping.Id,
                    ModelAlias = mapping.ModelAlias,
                    ProviderId = mapping.ProviderId,
                    IsEnabled = mapping.IsEnabled,
                    ChangeType = "Created",
                    CorrelationId = Guid.NewGuid().ToString()
                },
                $"create model mapping for {mapping.ModelAlias}",
                new { ModelAlias = mapping.ModelAlias, ProviderId = mapping.ProviderId });
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding model provider mapping for model ID: {ModelId}", mapping.ModelAlias.Replace(Environment.NewLine, ""));
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> UpdateMappingAsync(ModelProviderMapping mapping)
    {
        try
        {
            _logger.LogInformation("Updating model provider mapping with ID: {Id}", mapping.Id);

            // Check if the mapping exists
            var existingMapping = await _mappingRepository.GetByIdAsync(mapping.Id);
            if (existingMapping == null)
            {
                _logger.LogWarning("Model provider mapping not found with ID: {Id}", mapping.Id);
                return false;
            }

            // Validate provider exists by ID
            var provider = await _providerRepository.GetByIdAsync(mapping.ProviderId);
            if (provider == null)
            {
                _logger.LogWarning("Provider not found with ID: {ProviderId}", mapping.ProviderId);
                return false;
            }

            // Update properties that can be modified
            existingMapping.ModelAlias = mapping.ModelAlias;
            existingMapping.ModelId = mapping.ModelId;
            existingMapping.ProviderModelId = mapping.ProviderModelId;
            existingMapping.ProviderId = mapping.ProviderId;
            existingMapping.IsEnabled = mapping.IsEnabled;
            existingMapping.MaxContextTokensOverride = mapping.MaxContextTokensOverride;
            existingMapping.CapabilityOverrides = mapping.CapabilityOverrides;
            existingMapping.ProviderVariation = mapping.ProviderVariation;
            existingMapping.QualityScore = mapping.QualityScore;
            existingMapping.IsDefault = mapping.IsDefault;
            existingMapping.DefaultCapabilityType = mapping.DefaultCapabilityType;
            
            existingMapping.UpdatedAt = DateTime.UtcNow;

            // Update the mapping
            var result = await _mappingRepository.UpdateAsync(existingMapping);
            
            if (result)
            {
                // Publish ModelMappingChanged event for update
                await PublishEventAsync(
                    new ModelMappingChanged
                    {
                        MappingId = existingMapping.Id,
                        ModelAlias = existingMapping.ModelAlias,
                        ProviderId = existingMapping.ProviderId,
                        IsEnabled = existingMapping.IsEnabled,
                        ChangeType = "Updated",
                        CorrelationId = Guid.NewGuid().ToString()
                    },
                    $"update model mapping {existingMapping.Id}",
                    new { ModelAlias = existingMapping.ModelAlias, ProviderId = existingMapping.ProviderId });
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating model provider mapping with ID: {Id}", mapping.Id);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> DeleteMappingAsync(int id)
    {
        try
        {
            _logger.LogInformation("Deleting model provider mapping with ID: {Id}", id);

            // Check if the mapping exists
            var existingMapping = await _mappingRepository.GetByIdAsync(id);
            if (existingMapping == null)
            {
                _logger.LogWarning("Model provider mapping not found with ID: {Id}", id);
                return false;
            }

            // Delete the mapping
            var result = await _mappingRepository.DeleteAsync(id);
            
            if (result)
            {
                // Publish ModelMappingChanged event for deletion
                await PublishEventAsync(
                    new ModelMappingChanged
                    {
                        MappingId = existingMapping.Id,
                        ModelAlias = existingMapping.ModelAlias,
                        ProviderId = existingMapping.ProviderId,
                        IsEnabled = existingMapping.IsEnabled,
                        ChangeType = "Deleted",
                        CorrelationId = Guid.NewGuid().ToString()
                    },
                    $"delete model mapping {existingMapping.Id}",
                    new { ModelAlias = existingMapping.ModelAlias, ProviderId = existingMapping.ProviderId });
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting model provider mapping with ID: {Id}", id);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Provider>> GetProvidersAsync()
    {
        try
        {
            _logger.LogInformation("Getting all providers");
            return await _providerRepository.GetAllAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting providers");
            return Enumerable.Empty<Provider>();
        }
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<ModelProviderMapping> created, IEnumerable<string> errors)> CreateBulkMappingsAsync(IEnumerable<ModelProviderMapping> mappings)
    {
        _logger.LogInformation("Creating bulk model provider mappings");

        var created = new List<ModelProviderMapping>();
        var errors = new List<string>();
        var mappingsList = mappings.ToList();

        // Pre-load all providers and existing mappings to avoid N+1 queries
        var allProviders = await _providerRepository.GetAllAsync();
        var providerLookup = allProviders.ToDictionary(p => p.Id, p => p);
        var allMappings = await _mappingRepository.GetAllAsync();
        var existingMappingsLookup = allMappings.ToDictionary(m => m.ModelAlias.ToLowerInvariant(), m => m);

        for (int i = 0; i < mappingsList.Count; i++)
        {
            var mapping = mappingsList[i];

            try
            {
                // Validate provider exists
                if (!providerLookup.ContainsKey(mapping.ProviderId))
                {
                    errors.Add($"Index {i}: Provider not found with ID: {mapping.ProviderId}");
                    continue;
                }

                // Check for duplicate model ID
                var modelKeyLookup = mapping.ModelAlias.ToLowerInvariant();
                if (existingMappingsLookup.ContainsKey(modelKeyLookup))
                {
                    errors.Add($"Index {i}: Model ID already exists: {mapping.ModelAlias}");
                    continue;
                }

                // Set timestamps
                mapping.CreatedAt = DateTime.UtcNow;
                mapping.UpdatedAt = DateTime.UtcNow;

                // Add the mapping
                await _mappingRepository.CreateAsync(mapping);
                created.Add(mapping);

                // Add to lookup to prevent duplicates within the batch
                existingMappingsLookup[modelKeyLookup] = mapping;

                // Publish event
                await PublishEventAsync(
                    new ModelMappingChanged
                    {
                        MappingId = mapping.Id,
                        ModelAlias = mapping.ModelAlias,
                        ProviderId = mapping.ProviderId,
                        IsEnabled = mapping.IsEnabled,
                        ChangeType = "Created",
                        CorrelationId = Guid.NewGuid().ToString()
                    },
                    $"bulk create model mapping for {mapping.ModelAlias}",
                    new { ModelAlias = mapping.ModelAlias, ProviderId = mapping.ProviderId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing bulk mapping at index {Index} for model {ModelId}", 
                    i, mapping.ModelAlias.Replace(Environment.NewLine, ""));
                errors.Add($"Index {i}: System error: {ex.Message}");
            }
        }

        _logger.LogInformation("Bulk mapping operation completed. Created: {Created}, Failed: {Failed}",
            created.Count, errors.Count);

        return (created, errors);
    }
}