using ConduitLLM.Admin.Extensions;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ConduitLLM.Admin.Services;

/// <summary>
/// Service for managing model provider mappings through the Admin API
/// </summary>
public class AdminModelProviderMappingService : IAdminModelProviderMappingService
{
    private readonly IModelProviderMappingRepository _mappingRepository;
    private readonly IProviderCredentialRepository _credentialRepository;
    private readonly ILogger<AdminModelProviderMappingService> _logger;

    /// <summary>
    /// Initializes a new instance of the AdminModelProviderMappingService class
    /// </summary>
    /// <param name="mappingRepository">The model provider mapping repository</param>
    /// <param name="credentialRepository">The provider credential repository</param>
    /// <param name="logger">The logger</param>
    public AdminModelProviderMappingService(
        IModelProviderMappingRepository mappingRepository,
        IProviderCredentialRepository credentialRepository,
        ILogger<AdminModelProviderMappingService> logger)
    {
        _mappingRepository = mappingRepository ?? throw new ArgumentNullException(nameof(mappingRepository));
        _credentialRepository = credentialRepository ?? throw new ArgumentNullException(nameof(credentialRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
        _logger.LogInformation("Getting model provider mapping for model ID: {ModelId}", modelId);
        var mapping = await _mappingRepository.GetByModelAliasAsync(modelId);
        return mapping?.ToDto();
    }

    /// <inheritdoc />
    public async Task<bool> AddMappingAsync(ModelProviderMappingDto mappingDto)
    {
        try
        {
            _logger.LogInformation("Adding new model provider mapping for model ID: {ModelId}", mappingDto.ModelId);

            // Convert DTO to entity
            var mapping = mappingDto.ToEntity();

            // Validate that the provider exists
            int providerId = 0;
            if (!string.IsNullOrEmpty(mappingDto.ProviderId) && int.TryParse(mappingDto.ProviderId, out providerId))
            {
                var provider = await _credentialRepository.GetByIdAsync(providerId);
                if (provider == null)
                {
                    _logger.LogWarning("Provider not found with ID: {ProviderId}", providerId);
                    return false;
                }
            }
            else
            {
                _logger.LogWarning("Invalid provider ID: {ProviderId}", mappingDto.ProviderId);
                return false;
            }

            // Check if a mapping with the same model ID already exists
            var existingMapping = await _mappingRepository.GetByModelAliasAsync(mapping.ModelAlias);
            if (existingMapping != null)
            {
                _logger.LogWarning("A mapping for model ID already exists: {ModelId}", mapping.ModelAlias);
                return false;
            }

            // Add the mapping
            await _mappingRepository.AddAsync(mapping);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding model provider mapping for model ID: {ModelId}", mappingDto.ModelId);
            return false;
        }
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

            // Validate that the provider exists
            int providerId = 0;
            if (!string.IsNullOrEmpty(mappingDto.ProviderId) && int.TryParse(mappingDto.ProviderId, out providerId))
            {
                var provider = await _credentialRepository.GetByIdAsync(providerId);
                if (provider == null)
                {
                    _logger.LogWarning("Provider not found with ID: {ProviderId}", providerId);
                    return false;
                }
            }
            else
            {
                _logger.LogWarning("Invalid provider ID: {ProviderId}", mappingDto.ProviderId);
                return false;
            }

            // Update properties that can be modified
            existingMapping.ModelAlias = mapping.ModelAlias;
            existingMapping.ProviderModelName = mapping.ProviderModelName;
            existingMapping.ProviderCredentialId = mapping.ProviderCredentialId;
            existingMapping.IsEnabled = mapping.IsEnabled;
            existingMapping.MaxContextTokens = mapping.MaxContextTokens;
            existingMapping.UpdatedAt = DateTime.UtcNow;

            // Update the mapping
            return await _mappingRepository.UpdateAsync(existingMapping);
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
            return await _mappingRepository.DeleteAsync(id);
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
}