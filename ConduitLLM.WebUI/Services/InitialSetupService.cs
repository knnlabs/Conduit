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
}
