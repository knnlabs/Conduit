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
/// Service for configuring HTTP request timeout settings
/// </summary>
public class HttpTimeoutConfigurationService
{
    private readonly IDbContextFactory<ConfigurationDbContext> _dbContextFactory;
    private readonly ILogger<HttpTimeoutConfigurationService> _logger;
    private readonly IOptionsMonitor<TimeoutOptions> _options;

    /// <summary>
    /// Creates a new instance of the HttpTimeoutConfigurationService
    /// </summary>
    /// <param name="dbContextFactory">Factory for creating database contexts</param>
    /// <param name="logger">Logger for logging information</param>
    /// <param name="options">Current timeout options</param>
    public HttpTimeoutConfigurationService(
        IDbContextFactory<ConfigurationDbContext> dbContextFactory,
        ILogger<HttpTimeoutConfigurationService> logger,
        IOptionsMonitor<TimeoutOptions> options)
    {
        _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Gets the current timeout configuration
    /// </summary>
    /// <returns>The current timeout configuration</returns>
    public TimeoutOptions GetTimeoutConfiguration()
    {
        return _options.CurrentValue;
    }

    /// <summary>
    /// Updates the timeout configuration settings in the database
    /// </summary>
    /// <param name="timeoutOptions">The new timeout options to apply</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public async Task UpdateTimeoutConfigurationAsync(TimeoutOptions timeoutOptions)
    {
        if (timeoutOptions == null)
        {
            throw new ArgumentNullException(nameof(timeoutOptions));
        }

        try
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            
            // Update timeout seconds
            await UpdateSettingAsync(dbContext, "HttpTimeout:TimeoutSeconds", timeoutOptions.TimeoutSeconds.ToString());
            
            // Update enable logging
            await UpdateSettingAsync(dbContext, "HttpTimeout:EnableTimeoutLogging", timeoutOptions.EnableTimeoutLogging.ToString());

            await dbContext.SaveChangesAsync();

            _logger.LogInformation("HTTP timeout settings updated: TimeoutSeconds={TimeoutSeconds}, EnableLogging={EnableLogging}",
                timeoutOptions.TimeoutSeconds, timeoutOptions.EnableTimeoutLogging);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating HTTP timeout settings in database");
            throw;
        }
    }

    /// <summary>
    /// Loads HTTP timeout settings from the database and updates the application configuration.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task LoadSettingsFromDatabaseAsync()
    {
        try
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            
            // Get all timeout-related settings
            var settings = await dbContext.GlobalSettings
                .Where(s => s.Key.StartsWith("HttpTimeout:"))
                .ToListAsync();

            var currentOptions = _options.CurrentValue;
            
            // Try to parse TimeoutSeconds
            var timeoutSecondsSetting = settings.FirstOrDefault(s => s.Key == "HttpTimeout:TimeoutSeconds");
            if (timeoutSecondsSetting != null && int.TryParse(timeoutSecondsSetting.Value, out int timeoutSeconds))
            {
                currentOptions.TimeoutSeconds = timeoutSeconds;
            }
            
            // Try to parse EnableTimeoutLogging
            var enableLoggingSetting = settings.FirstOrDefault(s => s.Key == "HttpTimeout:EnableTimeoutLogging");
            if (enableLoggingSetting != null && bool.TryParse(enableLoggingSetting.Value, out bool enableLogging))
            {
                currentOptions.EnableTimeoutLogging = enableLogging;
            }

            _logger.LogInformation("HTTP timeout settings loaded from database: TimeoutSeconds={TimeoutSeconds}s, EnableLogging={EnableLogging}", 
                currentOptions.TimeoutSeconds, 
                currentOptions.EnableTimeoutLogging);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading HTTP timeout settings from database");
        }
    }

    private async Task UpdateSettingAsync(ConfigurationDbContext dbContext, string key, string value)
    {
        var setting = await dbContext.GlobalSettings.FirstOrDefaultAsync(s => s.Key == key);
        
        if (setting == null)
        {
            // Create new setting if it doesn't exist
            setting = new GlobalSetting { Key = key, Value = value };
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
