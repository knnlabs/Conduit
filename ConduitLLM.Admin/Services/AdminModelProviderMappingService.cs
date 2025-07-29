using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ConduitLLM.Admin.Extensions;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Core.Extensions;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Services;

using MassTransit;
using Microsoft.Extensions.Logging;

using static ConduitLLM.Core.Extensions.LoggingSanitizer;

namespace ConduitLLM.Admin.Services;

/// <summary>
/// Service for managing model provider mappings through the Admin API
/// </summary>
public class AdminModelProviderMappingService : EventPublishingServiceBase, IAdminModelProviderMappingService
{
    private readonly IModelProviderMappingRepository _mappingRepository;
    private readonly IProviderCredentialRepository _credentialRepository;
    private readonly ILogger<AdminModelProviderMappingService> _logger;

    /// <summary>
    /// Initializes a new instance of the AdminModelProviderMappingService class
    /// </summary>
    /// <param name="mappingRepository">The model provider mapping repository</param>
    /// <param name="credentialRepository">The provider credential repository</param>
    /// <param name="publishEndpoint">Optional event publishing endpoint (null if MassTransit not configured)</param>
    /// <param name="logger">The logger</param>
    public AdminModelProviderMappingService(
        IModelProviderMappingRepository mappingRepository,
        IProviderCredentialRepository credentialRepository,
        IPublishEndpoint? publishEndpoint,
        ILogger<AdminModelProviderMappingService> logger)
        : base(publishEndpoint, logger)
    {
        _mappingRepository = mappingRepository ?? throw new ArgumentNullException(nameof(mappingRepository));
        _credentialRepository = credentialRepository ?? throw new ArgumentNullException(nameof(credentialRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        LogEventPublishingConfiguration(nameof(AdminModelProviderMappingService));
    }

    /// <inheritdoc />
    public async Task<IEnumerable<ModelProviderMappingDto>> GetAllMappingsAsync()
    {
        _logger.LogInformation("Getting all model provider mappings");
        var mappings = await _mappingRepository.GetAllAsync();
        return mappings.Select(m => m.ToDto());
    }

    /// <inheritdoc />
    public async Task<ModelProviderMappingDto?> GetMappingByIdAsync(int id)
    {
        _logger.LogInformation("Getting model provider mapping with ID: {Id}", id);
        var mapping = await _mappingRepository.GetByIdAsync(id);
        return mapping?.ToDto();
    }

    /// <inheritdoc />
    public async Task<ModelProviderMappingDto?> GetMappingByModelIdAsync(string modelId)
    {
_logger.LogInformation("Getting model provider mapping for model ID: {ModelId}", modelId.Replace(Environment.NewLine, ""));
        var mapping = await _mappingRepository.GetByModelAliasAsync(modelId);
        return mapping?.ToDto();
    }

    /// <inheritdoc />
    public async Task<bool> AddMappingAsync(ModelProviderMappingDto mappingDto)
    {
        try
        {
_logger.LogInformation("Adding new model provider mapping for model ID: {ModelId}", mappingDto.ModelId.Replace(Environment.NewLine, ""));

            // Convert DTO to entity
            var mapping = mappingDto.ToEntity();

            // Validate provider exists by ID
            var provider = await _credentialRepository.GetByIdAsync(mappingDto.ProviderId);
            if (provider == null)
            {
                _logger.LogWarning("Provider not found with ID: {ProviderId}", mappingDto.ProviderId);
                return false;
            }

            // Update the mapping with the provider ID from DTO
            mapping.ProviderCredentialId = mappingDto.ProviderId;

            // Check if a mapping with the same model ID already exists
            var existingMapping = await _mappingRepository.GetByModelAliasAsync(mapping.ModelAlias);
            if (existingMapping != null)
            {
                _logger.LogWarning("A mapping for model ID already exists: {ModelId}", mapping.ModelAlias.Replace(Environment.NewLine, ""));
                return false;
            }

            // Add the mapping
            await _mappingRepository.AddAsync(mapping);
            
            // Publish ModelMappingChanged event for creation
            await PublishEventAsync(
                new ModelMappingChanged
                {
                    MappingId = mapping.Id,
                    ModelAlias = mapping.ModelAlias,
                    ProviderId = mapping.ProviderCredentialId,
                    IsEnabled = mapping.IsEnabled,
                    ChangeType = "Created",
                    CorrelationId = Guid.NewGuid().ToString()
                },
                $"create model mapping for {mapping.ModelAlias}",
                new { ModelAlias = mapping.ModelAlias, ProviderId = mapping.ProviderCredentialId });
            
            return true;
        }
        catch (Exception ex)
        {
_logger.LogError(ex, "Error adding model provider mapping for model ID: {ModelId}", mappingDto.ModelId.Replace(Environment.NewLine, ""));
            return false;
        }
    }

    /// <summary>
    /// Bulk validation method to check provider existence efficiently
    /// </summary>
    /// <param name="providerIds">Provider IDs to validate</param>
    /// <returns>Dictionary of provider ID to existence status</returns>
    public async Task<Dictionary<int, bool>> ValidateProvidersAsync(IEnumerable<int> providerIds)
    {
        var distinctIds = providerIds.Distinct().ToList();
        var allProviders = await _credentialRepository.GetAllAsync();
        var existingProviderIds = new HashSet<int>(allProviders.Select(p => p.Id));
        
        return distinctIds.ToDictionary(id => id, id => existingProviderIds.Contains(id));
    }

    /// <inheritdoc />
    public async Task<bool> UpdateMappingAsync(ModelProviderMappingDto mappingDto)
    {
        try
        {
            _logger.LogInformation("Updating model provider mapping with ID: {Id}", mappingDto.Id);

            // Convert DTO to entity
            var mapping = mappingDto.ToEntity();

            // Check if the mapping exists
            var existingMapping = await _mappingRepository.GetByIdAsync(mapping.Id);
            if (existingMapping == null)
            {
                _logger.LogWarning("Model provider mapping not found with ID: {Id}", mapping.Id);
                return false;
            }

            // Validate provider exists by ID
            var provider = await _credentialRepository.GetByIdAsync(mappingDto.ProviderId);
            if (provider == null)
            {
                _logger.LogWarning("Provider not found with ID: {ProviderId}", mappingDto.ProviderId);
                return false;
            }

            // Update properties that can be modified
            existingMapping.ModelAlias = mapping.ModelAlias;
            existingMapping.ProviderModelName = mapping.ProviderModelName;
            existingMapping.ProviderCredentialId = mappingDto.ProviderId; // Use the provider ID from DTO
            existingMapping.IsEnabled = mapping.IsEnabled;
            existingMapping.MaxContextTokens = mapping.MaxContextTokens;
            
            // Update capability fields
            existingMapping.SupportsVision = mapping.SupportsVision;
            existingMapping.SupportsAudioTranscription = mapping.SupportsAudioTranscription;
            existingMapping.SupportsTextToSpeech = mapping.SupportsTextToSpeech;
            existingMapping.SupportsRealtimeAudio = mapping.SupportsRealtimeAudio;
            existingMapping.SupportsImageGeneration = mapping.SupportsImageGeneration;
            existingMapping.SupportsVideoGeneration = mapping.SupportsVideoGeneration;
            existingMapping.TokenizerType = mapping.TokenizerType;
            existingMapping.SupportedVoices = mapping.SupportedVoices;
            existingMapping.SupportedLanguages = mapping.SupportedLanguages;
            existingMapping.SupportedFormats = mapping.SupportedFormats;
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
                        ProviderId = existingMapping.ProviderCredentialId,
                        IsEnabled = existingMapping.IsEnabled,
                        ChangeType = "Updated",
                        CorrelationId = Guid.NewGuid().ToString()
                    },
                    $"update model mapping {existingMapping.Id}",
                    new { ModelAlias = existingMapping.ModelAlias, ProviderId = existingMapping.ProviderCredentialId });
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating model provider mapping with ID: {Id}", mappingDto.Id);
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
                        ProviderId = existingMapping.ProviderCredentialId,
                        IsEnabled = existingMapping.IsEnabled,
                        ChangeType = "Deleted",
                        CorrelationId = Guid.NewGuid().ToString()
                    },
                    $"delete model mapping {existingMapping.Id}",
                    new { ModelAlias = existingMapping.ModelAlias, ProviderId = existingMapping.ProviderCredentialId });
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
    public async Task<IEnumerable<ProviderDataDto>> GetProvidersAsync()
    {
        try
        {
            _logger.LogInformation("Getting all providers");

            var providers = await _credentialRepository.GetAllAsync();
            return providers.Select(p => p.ToProviderDataDto());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting providers");
            return Enumerable.Empty<ProviderDataDto>();
        }
    }

    /// <inheritdoc />
    public async Task<BulkModelMappingResponse> CreateBulkMappingsAsync(BulkModelMappingRequest request)
    {
        _logger.LogInformation("Creating bulk model provider mappings for {Count} models", request.Mappings.Count);

        var response = new BulkModelMappingResponse
        {
            TotalProcessed = request.Mappings.Count
        };

        // BULK OPTIMIZATION: Pre-load all providers and existing mappings to avoid N+1 queries
        var allProviders = await _credentialRepository.GetAllAsync();
        var providerLookup = allProviders.ToDictionary(p => p.Id, p => p);
        var allMappings = await _mappingRepository.GetAllAsync();
        var existingMappingsLookup = allMappings.ToDictionary(m => m.ModelAlias.ToLowerInvariant(), m => m);

        for (int i = 0; i < request.Mappings.Count; i++)
        {
            var mappingDto = request.Mappings[i];

            try
            {
                // Validate and resolve provider by ID
                if (!providerLookup.TryGetValue(mappingDto.ProviderId, out var provider))
                {
                    response.Failed.Add(new BulkMappingError
                    {
                        Index = i,
                        Mapping = mappingDto,
                        ErrorMessage = $"Provider not found with ID: {mappingDto.ProviderId}",
                        ErrorType = BulkMappingErrorType.ProviderNotFound
                    });
                    continue;
                }
                
                int providerId = mappingDto.ProviderId;

                // Check for duplicate model ID using pre-loaded lookup
                var modelKeyLookup = mappingDto.ModelId.ToLowerInvariant();
                if (existingMappingsLookup.TryGetValue(modelKeyLookup, out var existingMapping))
                {
                    if (request.ReplaceExisting)
                    {
                        // Update the existing mapping with resolved provider ID
                        var updateDto = new ModelProviderMappingDto
                        {
                            Id = existingMapping.Id,
                            ModelId = mappingDto.ModelId,
                            ProviderModelId = mappingDto.ProviderModelId,
                            ProviderId = providerId,
                            ProviderType = provider.ProviderType,
                            Priority = mappingDto.Priority,
                            IsEnabled = mappingDto.IsEnabled,
                            Capabilities = mappingDto.Capabilities,
                            MaxContextLength = mappingDto.MaxContextLength,
                            SupportsVision = mappingDto.SupportsVision,
                            SupportsAudioTranscription = mappingDto.SupportsAudioTranscription,
                            SupportsTextToSpeech = mappingDto.SupportsTextToSpeech,
                            SupportsRealtimeAudio = mappingDto.SupportsRealtimeAudio,
                            SupportsImageGeneration = mappingDto.SupportsImageGeneration,
                            SupportsVideoGeneration = mappingDto.SupportsVideoGeneration,
                            TokenizerType = mappingDto.TokenizerType,
                            SupportedVoices = mappingDto.SupportedVoices,
                            SupportedLanguages = mappingDto.SupportedLanguages,
                            SupportedFormats = mappingDto.SupportedFormats,
                            IsDefault = mappingDto.IsDefault,
                            DefaultCapabilityType = mappingDto.DefaultCapabilityType,
                            Notes = mappingDto.Notes,
                            CreatedAt = existingMapping.CreatedAt,
                            UpdatedAt = DateTime.UtcNow
                        };

                        bool updateSuccess = await UpdateMappingAsync(updateDto);
                        if (updateSuccess)
                        {
                            var updatedMapping = await GetMappingByIdAsync(existingMapping.Id);
                            if (updatedMapping != null)
                            {
                                response.Updated.Add(updatedMapping);
                            }
                        }
                        else
                        {
                            response.Failed.Add(new BulkMappingError
                            {
                                Index = i,
                                Mapping = mappingDto,
                                ErrorMessage = "Failed to update existing mapping",
                                ErrorType = BulkMappingErrorType.SystemError
                            });
                        }
                    }
                    else
                    {
                        response.Failed.Add(new BulkMappingError
                        {
                            Index = i,
                            Mapping = mappingDto,
                            ErrorMessage = $"Model ID already exists: {mappingDto.ModelId}. Use ReplaceExisting=true to update.",
                            ErrorType = BulkMappingErrorType.Duplicate
                        });
                    }
                    continue;
                }

                // Create the new mapping with resolved provider ID
                var createDto = new ModelProviderMappingDto
                {
                    ModelId = mappingDto.ModelId,
                    ProviderModelId = mappingDto.ProviderModelId,
                    ProviderId = providerId,
                    ProviderType = provider.ProviderType,
                    Priority = mappingDto.Priority,
                    IsEnabled = mappingDto.IsEnabled,
                    Capabilities = mappingDto.Capabilities,
                    MaxContextLength = mappingDto.MaxContextLength,
                    SupportsVision = mappingDto.SupportsVision,
                    SupportsAudioTranscription = mappingDto.SupportsAudioTranscription,
                    SupportsTextToSpeech = mappingDto.SupportsTextToSpeech,
                    SupportsRealtimeAudio = mappingDto.SupportsRealtimeAudio,
                    SupportsImageGeneration = mappingDto.SupportsImageGeneration,
                    SupportsVideoGeneration = mappingDto.SupportsVideoGeneration,
                    TokenizerType = mappingDto.TokenizerType,
                    SupportedVoices = mappingDto.SupportedVoices,
                    SupportedLanguages = mappingDto.SupportedLanguages,
                    SupportedFormats = mappingDto.SupportedFormats,
                    IsDefault = mappingDto.IsDefault,
                    DefaultCapabilityType = mappingDto.DefaultCapabilityType,
                    Notes = mappingDto.Notes,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                bool createSuccess = await AddMappingAsync(createDto);
                if (createSuccess)
                {
                    var createdMapping = await GetMappingByModelIdAsync(mappingDto.ModelId);
                    if (createdMapping != null)
                    {
                        response.Created.Add(createdMapping);
                    }
                }
                else
                {
                    response.Failed.Add(new BulkMappingError
                    {
                        Index = i,
                        Mapping = mappingDto,
                        ErrorMessage = "Failed to create mapping",
                        ErrorType = BulkMappingErrorType.SystemError
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing bulk mapping at index {Index} for model {ModelId}", 
                    i, mappingDto.ModelId.Replace(Environment.NewLine, ""));
                
                response.Failed.Add(new BulkMappingError
                {
                    Index = i,
                    Mapping = mappingDto,
                    ErrorMessage = $"System error: {ex.Message}",
                    Details = ex.ToString(),
                    ErrorType = BulkMappingErrorType.SystemError
                });
            }
        }

        _logger.LogInformation("Bulk mapping operation completed. Created: {Created}, Updated: {Updated}, Failed: {Failed}",
            response.Created.Count, response.Updated.Count, response.Failed.Count);

        return response;
    }
}
