using ConduitLLM.WebUI.Interfaces;
using ConduitLLM.Configuration.DTOs.VirtualKey;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.WebUI.Services;

/// <summary>
/// Service that handles initial application setup tasks
/// </summary>
public class InitialSetupService
{
    private readonly ILogger<InitialSetupService> _logger;
    private readonly IGlobalSettingService _settingService;
    private readonly IServiceProvider _serviceProvider;

    private const string DefaultMasterKey = "conduit_master_default_key_change_me";
    private const string WebUIVirtualKeySettingName = "WebUI_VirtualKey";
    private const string WebUIVirtualKeyIdSettingName = "WebUI_VirtualKeyId";

    public InitialSetupService(
        ILogger<InitialSetupService> logger,
        IGlobalSettingService settingService,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _settingService = settingService;
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Ensures that a master key exists, either from environment variables or default
    /// </summary>
    public async Task EnsureMasterKeyExistsAsync()
    {
        try
        {
            // Always prioritize the environment variable if it's set
            var masterKeyEnvVar = Environment.GetEnvironmentVariable("CONDUIT_MASTER_KEY");

            if (!string.IsNullOrWhiteSpace(masterKeyEnvVar))
            {
                _logger.LogInformation("CONDUIT_MASTER_KEY environment variable found. Setting/updating master key hash in settings...");
                await _settingService.SetMasterKeyAsync(masterKeyEnvVar);
                _logger.LogInformation("Master key hash updated successfully from CONDUIT_MASTER_KEY environment variable.");
            }
            else
            {
                _logger.LogInformation("CONDUIT_MASTER_KEY environment variable not found. Checking for existing master key hash in settings...");
                // Only check/set default if environment variable is NOT present
                var existingHash = await _settingService.GetMasterKeyHashAsync();
                if (string.IsNullOrWhiteSpace(existingHash))
                {
                    // No environment variable AND no existing hash, set default with warning
                    _logger.LogWarning("No existing master key hash found. Setting default master key.");
                    _logger.LogWarning("IMPORTANT: Change the default master key for security reasons!");

                    await _settingService.SetMasterKeyAsync(DefaultMasterKey);
                    _logger.LogInformation("Default master key set. Please change it as soon as possible.");
                }
                else
                {
                    _logger.LogInformation("Existing master key hash found in settings. Using existing hash.");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ensuring master key exists.");
            throw; // Rethrow to ensure the application knows there was an error
        }
    }

    /// <summary>
    /// Ensures that a WebUI virtual key exists for API authentication
    /// </summary>
    public async Task EnsureWebUIVirtualKeyExistsAsync()
    {
        try
        {
            _logger.LogInformation("Checking for existing WebUI virtual key...");
            
            // Check if we already have a stored virtual key
            var existingKey = await _settingService.GetSettingAsync(WebUIVirtualKeySettingName);
            var existingKeyId = await _settingService.GetSettingAsync(WebUIVirtualKeyIdSettingName);
            
            if (!string.IsNullOrWhiteSpace(existingKey))
            {
                _logger.LogInformation("WebUI virtual key already exists in settings.");
                
                // Validate that the key still exists and is active
                if (!string.IsNullOrWhiteSpace(existingKeyId) && int.TryParse(existingKeyId, out var keyId))
                {
                    try
                    {
                        // Get virtual key service from service provider
                        var vkService = _serviceProvider.GetRequiredService<IVirtualKeyService>();
                        var keyInfo = await vkService.GetVirtualKeyInfoAsync(keyId);
                        if (keyInfo != null && keyInfo.IsEnabled)
                        {
                            _logger.LogInformation("WebUI virtual key is valid and active (ID: {KeyId}, Name: {KeyName})", keyId, keyInfo.KeyName);
                            return;
                        }
                        else
                        {
                            _logger.LogWarning("WebUI virtual key exists but is disabled or deleted. Creating a new one...");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error validating existing WebUI virtual key. Creating a new one...");
                    }
                }
            }
            
            // Create a new virtual key for WebUI
            _logger.LogInformation("Creating new WebUI virtual key...");
            
            var createRequest = new CreateVirtualKeyRequestDto
            {
                KeyName = "WebUI Internal Key",
                AllowedModels = null, // Allow all models
                MaxBudget = null, // No budget limit
                BudgetDuration = null,
                ExpiresAt = null, // Never expires
                Metadata = "{\"purpose\": \"Internal WebUI authentication\", \"createdBy\": \"InitialSetupService\"}",
                RateLimitRpm = null, // No rate limit
                RateLimitRpd = null
            };
            
            // Get virtual key service from service provider
            var virtualKeyService = _serviceProvider.GetRequiredService<IVirtualKeyService>();
            var response = await virtualKeyService.GenerateVirtualKeyAsync(createRequest);
            
            if (response != null && !string.IsNullOrWhiteSpace(response.VirtualKey))
            {
                // Store the key and its ID in settings
                await _settingService.SetSettingAsync(WebUIVirtualKeySettingName, response.VirtualKey);
                await _settingService.SetSettingAsync(WebUIVirtualKeyIdSettingName, response.KeyInfo.Id.ToString());
                
                _logger.LogInformation("WebUI virtual key created successfully (ID: {KeyId}, Name: {KeyName})", 
                    response.KeyInfo.Id, response.KeyInfo.KeyName);
            }
            else
            {
                _logger.LogError("Failed to create WebUI virtual key - no response received");
                throw new InvalidOperationException("Failed to create WebUI virtual key");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ensuring WebUI virtual key exists");
            throw; // Rethrow to ensure the application knows there was an error
        }
    }
}
