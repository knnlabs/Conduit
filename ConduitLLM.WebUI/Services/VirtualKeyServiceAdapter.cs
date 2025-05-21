using ConduitLLM.Configuration.DTOs.VirtualKey;
using ConduitLLM.WebUI.Interfaces;
using ConduitLLM.WebUI.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WebUIDTOs = ConduitLLM.WebUI.DTOs;
using ConfigDTOs = ConduitLLM.Configuration.DTOs.VirtualKey;

namespace ConduitLLM.WebUI.Services;

/// <summary>
/// Adapter service for virtual keys that uses the Admin API
/// </summary>
public class VirtualKeyServiceAdapter : IVirtualKeyService
{
    private readonly IAdminApiClient _adminApiClient;
    private readonly AdminApiOptions _adminApiOptions;
    private readonly ILogger<VirtualKeyServiceAdapter> _logger;
    
    /// <summary>
    /// Initializes a new instance of the VirtualKeyServiceAdapter class
    /// </summary>
    /// <param name="adminApiClient">The Admin API client</param>
    /// <param name="adminApiOptions">The Admin API options</param>
    /// <param name="logger">The logger</param>
    public VirtualKeyServiceAdapter(
        IAdminApiClient adminApiClient,
        IOptions<AdminApiOptions> adminApiOptions,
        ILogger<VirtualKeyServiceAdapter> logger)
    {
        _adminApiClient = adminApiClient ?? throw new ArgumentNullException(nameof(adminApiClient));
        _adminApiOptions = adminApiOptions?.Value ?? throw new ArgumentNullException(nameof(adminApiOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    /// <inheritdoc />
    public async Task<CreateVirtualKeyResponseDto> GenerateVirtualKeyAsync(CreateVirtualKeyRequestDto request)
    {
        try
        {
            var result = await _adminApiClient.CreateVirtualKeyAsync(request);
            if (result == null)
            {
                throw new InvalidOperationException("Failed to create virtual key");
            }
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating virtual key through Admin API");
            throw;
        }
    }
    
    /// <inheritdoc />
    public async Task<VirtualKeyDto?> GetVirtualKeyInfoAsync(int id)
    {
        try
        {
            return await _adminApiClient.GetVirtualKeyByIdAsync(id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting virtual key info with ID {Id} from Admin API", id);
            throw;
        }
    }
    
    /// <inheritdoc />
    public async Task<List<VirtualKeyDto>> ListVirtualKeysAsync()
    {
        try
        {
            var keys = await _adminApiClient.GetAllVirtualKeysAsync();
            return new List<VirtualKeyDto>(keys);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing virtual keys from Admin API");
            throw;
        }
    }
    
    /// <inheritdoc />
    public async Task<bool> UpdateVirtualKeyAsync(int id, UpdateVirtualKeyRequestDto request)
    {
        try
        {
            return await _adminApiClient.UpdateVirtualKeyAsync(id, request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating virtual key with ID {Id} through Admin API", id);
            throw;
        }
    }
    
    /// <inheritdoc />
    public async Task<bool> DeleteVirtualKeyAsync(int id)
    {
        try
        {
            return await _adminApiClient.DeleteVirtualKeyAsync(id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting virtual key with ID {Id} through Admin API", id);
            throw;
        }
    }
    
    /// <inheritdoc />
    public async Task<bool> ResetSpendAsync(int id)
    {
        try
        {
            return await _adminApiClient.ResetVirtualKeySpendAsync(id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting spend for virtual key with ID {Id} through Admin API", id);
            throw;
        }
    }
    
    /// <inheritdoc />
    public async Task<VirtualKeyValidationInfoDto?> ValidateVirtualKeyAsync(string key, string? requestedModel = null)
    {
        try
        {
            var validationResult = await _adminApiClient.ValidateVirtualKeyAsync(key, requestedModel);
            
            if (validationResult == null || !validationResult.IsValid)
            {
                return null;
            }
            
            // If validation succeeds, get the full validation info
            if (validationResult.VirtualKeyId.HasValue)
            {
                return await _adminApiClient.GetVirtualKeyValidationInfoAsync(validationResult.VirtualKeyId.Value);
            }
            
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating virtual key through Admin API");
            throw;
        }
    }
    
    /// <inheritdoc />
    public async Task<bool> UpdateSpendAsync(int keyId, decimal cost)
    {
        try
        {
            // Direct call to update spend using keyId and cost
            return await _adminApiClient.UpdateVirtualKeySpendAsync(keyId, cost);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating spend for virtual key ID {KeyId} through Admin API", keyId);
            throw;
        }
    }
    
    /// <inheritdoc />
    public async Task<bool> ResetBudgetIfExpiredAsync(int keyId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _adminApiClient.CheckVirtualKeyBudgetAsync(keyId);
            return result?.WasReset ?? false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking budget expiration for virtual key ID {KeyId} through Admin API", keyId);
            throw;
        }
    }
    
    /// <inheritdoc />
    public async Task<VirtualKeyValidationInfoDto?> GetVirtualKeyInfoForValidationAsync(int keyId, CancellationToken cancellationToken = default)
    {
        try
        {
            // Get the validation info directly through the API
            return await _adminApiClient.GetVirtualKeyValidationInfoAsync(keyId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting virtual key info for validation (ID: {KeyId}) through Admin API", keyId);
            throw;
        }
    }
    
    /// <inheritdoc />
    public async Task PerformMaintenanceAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _adminApiClient.PerformVirtualKeyMaintenanceAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing virtual key maintenance through Admin API");
            throw;
        }
    }
}