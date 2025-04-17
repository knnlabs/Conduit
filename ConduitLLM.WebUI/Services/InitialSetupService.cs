using ConduitLLM.WebUI.Interfaces;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.WebUI.Services;

/// <summary>
/// Service that handles initial application setup tasks
/// </summary>
public class InitialSetupService
{
    private readonly ILogger<InitialSetupService> _logger;
    private readonly IGlobalSettingService _settingService;

    private const string DefaultMasterKey = "conduit_master_default_key_change_me";

    public InitialSetupService(
        ILogger<InitialSetupService> logger,
        IGlobalSettingService settingService)
    {
        _logger = logger;
        _settingService = settingService;
    }

    /// <summary>
    /// Ensures that a master key exists, either from environment variables or default
    /// </summary>
    public async Task EnsureMasterKeyExistsAsync()
    {
        try
        {
            var existingHash = await _settingService.GetMasterKeyHashAsync();
            if (string.IsNullOrWhiteSpace(existingHash))
            {
                _logger.LogInformation("No master key hash found in settings, checking environment variables...");
                
                // Try to get from environment variable
                var masterKeyEnvVar = Environment.GetEnvironmentVariable("CONDUIT_MASTER_KEY");
                
                if (!string.IsNullOrWhiteSpace(masterKeyEnvVar))
                {
                    await _settingService.SetMasterKeyAsync(masterKeyEnvVar);
                    _logger.LogInformation("Master key set successfully from CONDUIT_MASTER_KEY environment variable.");
                }
                else
                {
                    // No environment variable, set default with warning
                    _logger.LogWarning("No CONDUIT_MASTER_KEY environment variable found. Setting default master key.");
                    _logger.LogWarning("IMPORTANT: Change the default master key for security reasons!");
                    
                    await _settingService.SetMasterKeyAsync(DefaultMasterKey);
                    _logger.LogInformation("Default master key set. Please change it as soon as possible.");
                }
            }
            else
            {
                _logger.LogInformation("Master key hash already exists in settings.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ensuring master key exists.");
            throw; // Rethrow to ensure the application knows there was an error
        }
    }
}
