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
    private readonly IDbContextFactory<ConfigurationDbContext> _dbContextFactory;
    private readonly ILogger<HttpRetryConfigurationService> _logger;
    private readonly IOptionsMonitor<RetryOptions> _options;

    public HttpRetryConfigurationService(
        IDbContextFactory<ConfigurationDbContext> dbContextFactory,
        ILogger<HttpRetryConfigurationService> logger,
        IOptionsMonitor<RetryOptions> options)
    {
        _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Loads HTTP retry settings from the database and updates the application configuration.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task LoadSettingsFromDatabaseAsync()
    {
        try
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            
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
}
