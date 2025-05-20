using ConduitLLM.Configuration.DTOs.VirtualKey;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.WebUI.Interfaces;
using ConduitLLM.WebUI.Options;
using Microsoft.Extensions.Options;
using WebUIDTOs = ConduitLLM.WebUI.DTOs;
using ConfigDTOs = ConduitLLM.Configuration.DTOs.VirtualKey;

namespace ConduitLLM.WebUI.Services;

/// <summary>
/// Adapter service for virtual keys that can use either direct repository access or the Admin API
/// </summary>
public class VirtualKeyServiceAdapter : IVirtualKeyService
{
    private readonly VirtualKeyService _repositoryService;
    private readonly IAdminApiClient _adminApiClient;
    private readonly AdminApiOptions _adminApiOptions;
    private readonly ILogger<VirtualKeyServiceAdapter> _logger;
    
    /// <summary>
    /// Initializes a new instance of the VirtualKeyServiceAdapter class
    /// </summary>
    /// <param name="repositoryService">The repository-based virtual key service</param>
    /// <param name="adminApiClient">The Admin API client</param>
    /// <param name="adminApiOptions">The Admin API options</param>
    /// <param name="logger">The logger</param>
    public VirtualKeyServiceAdapter(
        VirtualKeyService repositoryService,
        IAdminApiClient adminApiClient,
        IOptions<AdminApiOptions> adminApiOptions,
        ILogger<VirtualKeyServiceAdapter> logger)
    {
        _repositoryService = repositoryService ?? throw new ArgumentNullException(nameof(repositoryService));
        _adminApiClient = adminApiClient ?? throw new ArgumentNullException(nameof(adminApiClient));
        _adminApiOptions = adminApiOptions?.Value ?? throw new ArgumentNullException(nameof(adminApiOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    /// <inheritdoc />
    public async Task<CreateVirtualKeyResponseDto> GenerateVirtualKeyAsync(CreateVirtualKeyRequestDto request)
    {
        if (_adminApiOptions.Enabled)
        {
            try
            {
                return await _adminApiClient.CreateVirtualKeyAsync(request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating virtual key through Admin API, falling back to repository");
                return await _repositoryService.GenerateVirtualKeyAsync(request);
            }
        }
        
        return await _repositoryService.GenerateVirtualKeyAsync(request);
    }
    
    /// <inheritdoc />
    public async Task<VirtualKeyDto?> GetVirtualKeyInfoAsync(int id)
    {
        if (_adminApiOptions.Enabled)
        {
            try
            {
                return await _adminApiClient.GetVirtualKeyByIdAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting virtual key info with ID {Id} from Admin API, falling back to repository", id);
                return await _repositoryService.GetVirtualKeyInfoAsync(id);
            }
        }
        
        return await _repositoryService.GetVirtualKeyInfoAsync(id);
    }
    
    /// <inheritdoc />
    public async Task<List<VirtualKeyDto>> ListVirtualKeysAsync()
    {
        if (_adminApiOptions.Enabled)
        {
            try
            {
                var keys = await _adminApiClient.GetAllVirtualKeysAsync();
                return new List<VirtualKeyDto>(keys);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing virtual keys from Admin API, falling back to repository");
                return await _repositoryService.ListVirtualKeysAsync();
            }
        }
        
        return await _repositoryService.ListVirtualKeysAsync();
    }
    
    /// <inheritdoc />
    public async Task<bool> UpdateVirtualKeyAsync(int id, UpdateVirtualKeyRequestDto request)
    {
        if (_adminApiOptions.Enabled)
        {
            try
            {
                return await _adminApiClient.UpdateVirtualKeyAsync(id, request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating virtual key with ID {Id} through Admin API, falling back to repository", id);
                return await _repositoryService.UpdateVirtualKeyAsync(id, request);
            }
        }
        
        return await _repositoryService.UpdateVirtualKeyAsync(id, request);
    }
    
    /// <inheritdoc />
    public async Task<bool> DeleteVirtualKeyAsync(int id)
    {
        if (_adminApiOptions.Enabled)
        {
            try
            {
                return await _adminApiClient.DeleteVirtualKeyAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting virtual key with ID {Id} through Admin API, falling back to repository", id);
                return await _repositoryService.DeleteVirtualKeyAsync(id);
            }
        }
        
        return await _repositoryService.DeleteVirtualKeyAsync(id);
    }
    
    /// <inheritdoc />
    public async Task<bool> ResetSpendAsync(int id)
    {
        if (_adminApiOptions.Enabled)
        {
            try
            {
                return await _adminApiClient.ResetVirtualKeySpendAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting spend for virtual key with ID {Id} through Admin API, falling back to repository", id);
                return await _repositoryService.ResetSpendAsync(id);
            }
        }
        
        return await _repositoryService.ResetSpendAsync(id);
    }
    
    /// <inheritdoc />
    public async Task<VirtualKey?> ValidateVirtualKeyAsync(string key, string? requestedModel = null)
    {
        // Always use the repository service for validation since it's a critical and
        // frequently-used operation that requires direct database access for performance
        return await _repositoryService.ValidateVirtualKeyAsync(key, requestedModel);
    }
    
    /// <inheritdoc />
    public async Task<bool> UpdateSpendAsync(int keyId, decimal cost)
    {
        // Always use the repository service for spend updates since they're performance-critical
        // and frequently-used operations
        return await _repositoryService.UpdateSpendAsync(keyId, cost);
    }
    
    /// <inheritdoc />
    public async Task<bool> ResetBudgetIfExpiredAsync(int keyId, CancellationToken cancellationToken = default)
    {
        // Always use the repository service for budget reset checks since they're
        // internal operations that happen frequently
        return await _repositoryService.ResetBudgetIfExpiredAsync(keyId, cancellationToken);
    }
    
    /// <inheritdoc />
    public async Task<VirtualKey?> GetVirtualKeyInfoForValidationAsync(int keyId, CancellationToken cancellationToken = default)
    {
        // Always use the repository service for this internal method
        return await _repositoryService.GetVirtualKeyInfoForValidationAsync(keyId, cancellationToken);
    }
}