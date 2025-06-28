using Microsoft.Extensions.Logging;
using ConduitLLM.AdminClient;
using ConduitLLM.AdminClient.Client;
using ConduitLLM.AdminClient.Models;
using ConduitLLM.AdminClient.Exceptions;
using ConduitLLM.TUI.Models;

namespace ConduitLLM.TUI.Services;

public class AdminApiService
{
    private readonly ConduitAdminClient _adminClient;
    private readonly StateManager _stateManager;
    private readonly ILogger<AdminApiService> _logger;

    public AdminApiService(ConduitAdminClient adminClient, StateManager stateManager, ILogger<AdminApiService> logger)
    {
        _adminClient = adminClient;
        _stateManager = stateManager;
        _logger = logger;
    }

    // Provider Management
    public async Task<List<ProviderCredentialDto>> GetProvidersAsync()
    {
        try
        {
            var providers = await _adminClient.Providers.ListCredentialsAsync();
            var providerList = providers.ToList();
            _stateManager.Providers = providerList;
            return providerList;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get providers");
            throw;
        }
    }

    public async Task<ProviderCredentialDto> CreateProviderAsync(CreateProviderCredentialDto createDto)
    {
        try
        {
            var provider = await _adminClient.Providers.CreateCredentialAsync(createDto);
            _stateManager.UpdateProvider(provider);
            return provider;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create provider");
            throw;
        }
    }

    public async Task<ProviderCredentialDto> UpdateProviderAsync(int id, UpdateProviderCredentialDto updateDto)
    {
        try
        {
            await _adminClient.Providers.UpdateCredentialAsync(id, updateDto);
            // Fetch the updated provider to get the latest data
            var provider = await _adminClient.Providers.GetCredentialByIdAsync(id);
            _stateManager.UpdateProvider(provider);
            return provider;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update provider");
            throw;
        }
    }

    public async Task DeleteProviderAsync(int id)
    {
        try
        {
            await _adminClient.Providers.DeleteCredentialAsync(id);
            _stateManager.RemoveProvider(id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete provider");
            throw;
        }
    }

    // Model Mapping Management
    public async Task<List<ModelProviderMappingDto>> GetModelMappingsAsync()
    {
        try
        {
            var mappings = await _adminClient.ModelMappings.ListAsync();
            var mappingList = mappings.ToList();
            _stateManager.ModelMappings = mappingList;
            return mappingList;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get model mappings");
            throw;
        }
    }

    public async Task<ModelProviderMappingDto> CreateModelMappingAsync(CreateModelProviderMappingDto createDto)
    {
        try
        {
            var mapping = await _adminClient.ModelMappings.CreateAsync(createDto);
            _stateManager.UpdateModelMapping(mapping);
            return mapping;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create model mapping");
            throw;
        }
    }

    public async Task<ModelProviderMappingDto> UpdateModelMappingAsync(int id, UpdateModelProviderMappingDto updateDto)
    {
        try
        {
            await _adminClient.ModelMappings.UpdateAsync(id, updateDto);
            // Fetch the updated mapping to get the latest data
            var mapping = await _adminClient.ModelMappings.GetByIdAsync(id);
            _stateManager.UpdateModelMapping(mapping);
            return mapping;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update model mapping");
            throw;
        }
    }

    public async Task DeleteModelMappingAsync(int id)
    {
        try
        {
            await _adminClient.ModelMappings.DeleteAsync(id);
            _stateManager.RemoveModelMapping(id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete model mapping");
            throw;
        }
    }

    // Virtual Key Management
    public async Task<List<VirtualKeyDto>> GetVirtualKeysAsync()
    {
        try
        {
            var keys = await _adminClient.VirtualKeys.ListAsync();
            var keyList = keys.ToList();
            _stateManager.VirtualKeys = keyList;
            return keyList;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get virtual keys");
            throw;
        }
    }

    public async Task<CreateVirtualKeyResponse> CreateVirtualKeyAsync(CreateVirtualKeyRequest createDto)
    {
        try
        {
            var response = await _adminClient.VirtualKeys.CreateAsync(createDto);
            // Fetch the created key details to add to state
            var key = response.KeyInfo;
            var keys = _stateManager.VirtualKeys.ToList();
            keys.Add(key);
            _stateManager.VirtualKeys = keys;
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create virtual key");
            throw;
        }
    }

    public async Task<VirtualKeyDto> UpdateVirtualKeyAsync(int id, UpdateVirtualKeyRequest updateDto)
    {
        try
        {
            await _adminClient.VirtualKeys.UpdateAsync(id, updateDto);
            // Fetch the updated key to get the latest data
            var key = await _adminClient.VirtualKeys.GetByIdAsync(id);
            var keys = _stateManager.VirtualKeys.ToList();
            var index = keys.FindIndex(k => k.Id == id);
            if (index >= 0)
            {
                keys[index] = key;
                _stateManager.VirtualKeys = keys;
            }
            return key;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update virtual key");
            throw;
        }
    }

    public async Task DeleteVirtualKeyAsync(int id)
    {
        try
        {
            await _adminClient.VirtualKeys.DeleteAsync(id);
            var keys = _stateManager.VirtualKeys.ToList();
            keys.RemoveAll(k => k.Id == id);
            _stateManager.VirtualKeys = keys;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete virtual key");
            throw;
        }
    }

    // Model Discovery
    public async Task<Dictionary<string, List<ModelCapabilityDto>>> DiscoverModelsAsync()
    {
        try
        {
            var response = await _adminClient.Discovery.DiscoverAllModelsAsync();
            // Convert the response to the expected dictionary format
            var capabilities = new Dictionary<string, List<ModelCapabilityDto>>();
            
            if (response?.Models != null)
            {
                foreach (var model in response.Models)
                {
                    if (!capabilities.ContainsKey(model.Provider))
                    {
                        capabilities[model.Provider] = new List<ModelCapabilityDto>();
                    }
                    
                    capabilities[model.Provider].Add(new ModelCapabilityDto
                    {
                        ModelId = model.Name,
                        Provider = model.Provider,
                        Capabilities = model.Capabilities?.ToList() ?? new List<string>(),
                        IsAvailable = true, // Discovery models are assumed available
                        ErrorMessage = null
                    });
                }
            }
            
            _stateManager.ModelCapabilities = capabilities;
            return capabilities;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to discover models");
            throw;
        }
    }

    // Settings Management
    public async Task<List<GlobalSettingDto>> GetSettingsAsync()
    {
        try
        {
            var settings = await _adminClient.Settings.GetGlobalSettingsAsync();
            return settings.ToList();
        }
        catch (ConduitLLM.AdminClient.Exceptions.AuthorizationException authEx)
        {
            _logger.LogError("Authorization failed when retrieving settings: {Message}", authEx.Message);
            return new List<GlobalSettingDto>();
        }
        catch (ConduitLLM.AdminClient.Exceptions.AuthenticationException authEx)
        {
            _logger.LogError("Authentication failed when retrieving settings: {Message}", authEx.Message);
            return new List<GlobalSettingDto>();
        }
        catch (ConduitLLM.AdminClient.Exceptions.NetworkException netEx)
        {
            _logger.LogError("Network error when retrieving settings: {Message}", netEx.Message);
            return new List<GlobalSettingDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to get settings: {Message}", ex.Message);
            return new List<GlobalSettingDto>();
        }
    }

    public async Task<GlobalSettingDto?> GetSettingByKeyAsync(string key)
    {
        try
        {
            // First try the SDK method
            _logger.LogInformation("Trying to get setting by key: {Key}", key);
            var setting = await _adminClient.Settings.GetGlobalSettingAsync(key);
            _logger.LogInformation("Successfully retrieved setting: {Key} = {Value}", key, setting?.Value ?? "null");
            return setting;
        }
        catch (ConduitLLM.AdminClient.Exceptions.NotFoundException notFoundEx)
        {
            _logger.LogWarning("Setting not found: {Key} - {Message}", key, notFoundEx.Message);
            return null;
        }
        catch (ConduitLLM.AdminClient.Exceptions.AuthorizationException authEx)
        {
            _logger.LogWarning("Authorization failed for setting '{Key}': {Message}", key, authEx.Message);
            // Don't fall back when it's an authorization issue
            return null;
        }
        catch (ConduitLLM.AdminClient.Exceptions.AuthenticationException authEx)
        {
            _logger.LogWarning("Authentication failed for setting '{Key}': {Message}", key, authEx.Message);
            // Don't fall back when it's an authentication issue
            return null;
        }
        catch (ConduitLLM.AdminClient.Exceptions.NetworkException netEx)
        {
            _logger.LogWarning("Network error when retrieving setting '{Key}': {Message}", key, netEx.Message);
            // For network errors, we might want to try the fallback
            _logger.LogInformation("Attempting fallback method due to network error");
            // Fall through to the fallback logic below
        }
        catch (Exception ex)
        {
            _logger.LogWarning("SDK method failed to get setting by key '{Key}': {Message}, trying fallback", key, ex.Message);
        }
        
        // Fallback: Try getting all settings and filter by key
        try
        {
            var allSettings = await GetSettingsAsync();
            var foundSetting = allSettings.FirstOrDefault(s => s.Key == key);
            if (foundSetting != null)
            {
                _logger.LogInformation("Found setting via fallback: {Key} = {Value}", key, foundSetting.Value);
            }
            return foundSetting;
        }
        catch (Exception fallbackEx)
        {
            _logger.LogError("Fallback method also failed to get setting by key '{Key}': {Message}", key, fallbackEx.Message);
            return null;
        }
    }
}