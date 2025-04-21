using System;
using System.Linq;
using System.Threading.Tasks;
using ConduitLLM.Providers.Configuration;
using ConduitLLM.WebUI.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConduitLLM.WebUI.Services;

/// <summary>
/// Service for managing HTTP retry configuration settings.
/// </summary>
public class HttpRetryConfigurationService
{
    // Inject the factory for the CORRECT DbContext that manages GlobalSettings
    private readonly IDbContextFactory<ConduitLLM.Configuration.ConfigurationDbContext> _configContextFactory; 
    private readonly ILogger<HttpRetryConfigurationService> _logger;
    private readonly IOptionsMonitor<RetryOptions> _options;

    // Update constructor signature
    public HttpRetryConfigurationService(
        IDbContextFactory<ConduitLLM.Configuration.ConfigurationDbContext> configContextFactory, 
        ILogger<HttpRetryConfigurationService> logger,
        IOptionsMonitor<RetryOptions> options)
    {
        _configContextFactory = configContextFactory ?? throw new ArgumentNullException(nameof(configContextFactory)); // Assign the correct factory
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Gets the current retry configuration
    /// </summary>
    /// <returns>The current retry configuration</returns>
    public RetryOptions GetRetryConfiguration()
    {
        return _options.CurrentValue;
    }

    /// <summary>
    /// Updates the retry configuration settings
    /// </summary>
    /// <param name="retryOptions">The new retry options to apply</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public async Task UpdateRetryConfigurationAsync(RetryOptions retryOptions)
    {
        if (retryOptions == null)
        {
            throw new ArgumentNullException(nameof(retryOptions));
        }

        try
        {
            // Use the correct context factory
            using var dbContext = await _configContextFactory.CreateDbContextAsync(); 
            
            // Save MaxRetries setting
            await UpdateSettingAsync(dbContext, "HttpRetry:MaxRetries", retryOptions.MaxRetries.ToString());
            
            // Save InitialDelaySeconds setting
            await UpdateSettingAsync(dbContext, "HttpRetry:InitialDelaySeconds", retryOptions.InitialDelaySeconds.ToString());
            
            // Save MaxDelaySeconds setting
            await UpdateSettingAsync(dbContext, "HttpRetry:MaxDelaySeconds", retryOptions.MaxDelaySeconds.ToString());
            
            // Save EnableRetryLogging setting
            await UpdateSettingAsync(dbContext, "HttpRetry:EnableRetryLogging", retryOptions.EnableRetryLogging.ToString());

            await dbContext.SaveChangesAsync();

            _logger.LogInformation("HTTP retry settings updated: MaxRetries={MaxRetries}, InitialDelay={InitialDelay}s, MaxDelay={MaxDelay}s, EnableLogging={EnableLogging}",
                retryOptions.MaxRetries, retryOptions.InitialDelaySeconds, retryOptions.MaxDelaySeconds, retryOptions.EnableRetryLogging);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating HTTP retry settings in database");
            throw;
        }
    }

    /// <summary>
    /// Loads HTTP retry settings from the database and updates the application configuration.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task LoadSettingsFromDatabaseAsync()
    {
        try
        {
            // Use the correct context factory
            using var dbContext = await _configContextFactory.CreateDbContextAsync(); 
            
            // Get all retry-related settings
            var settings = await dbContext.GlobalSettings
                .Where(s => s.Key.StartsWith("HttpRetry:"))
                .ToListAsync();

            var currentOptions = _options.CurrentValue;
            
            // Try to parse MaxRetries
            var maxRetriesSetting = settings.FirstOrDefault(s => s.Key == "HttpRetry:MaxRetries");
            if (maxRetriesSetting != null && int.TryParse(maxRetriesSetting.Value, out int maxRetries))
            {
                currentOptions.MaxRetries = maxRetries;
            }
            
            // Try to parse InitialDelaySeconds
            var initialDelaySetting = settings.FirstOrDefault(s => s.Key == "HttpRetry:InitialDelaySeconds");
            if (initialDelaySetting != null && int.TryParse(initialDelaySetting.Value, out int initialDelay))
            {
                currentOptions.InitialDelaySeconds = initialDelay;
            }
            
            // Try to parse MaxDelaySeconds
            var maxDelaySetting = settings.FirstOrDefault(s => s.Key == "HttpRetry:MaxDelaySeconds");
            if (maxDelaySetting != null && int.TryParse(maxDelaySetting.Value, out int maxDelay))
            {
                currentOptions.MaxDelaySeconds = maxDelay;
            }
            
            // Try to parse EnableRetryLogging
            var enableLoggingSetting = settings.FirstOrDefault(s => s.Key == "HttpRetry:EnableRetryLogging");
            if (enableLoggingSetting != null && bool.TryParse(enableLoggingSetting.Value, out bool enableLogging))
            {
                currentOptions.EnableRetryLogging = enableLogging;
            }

            _logger.LogInformation("HTTP retry settings loaded from database: MaxRetries={MaxRetries}, InitialDelay={InitialDelay}s, MaxDelay={MaxDelay}s, EnableLogging={EnableLogging}", 
                currentOptions.MaxRetries, 
                currentOptions.InitialDelaySeconds,
                currentOptions.MaxDelaySeconds,
                currentOptions.EnableRetryLogging);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading HTTP retry settings from database");
        }
    }

    // Update DbContext type in parameter
    private async Task UpdateSettingAsync(ConduitLLM.Configuration.ConfigurationDbContext dbContext, string key, string value) 
    {
        var setting = await dbContext.GlobalSettings.FirstOrDefaultAsync(s => s.Key == key);
        
        if (setting == null)
        {
            // Create new setting if it doesn't exist
            // Use the correct GlobalSetting entity type
            setting = new ConduitLLM.Configuration.Entities.GlobalSetting { Key = key, Value = value }; 
            dbContext.GlobalSettings.Add(setting);
        }
        else
        {
            // Update existing setting
            setting.Value = value;
            dbContext.GlobalSettings.Update(setting);
        }
    }
}
