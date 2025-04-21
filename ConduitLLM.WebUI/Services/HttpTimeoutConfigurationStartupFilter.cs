using ConduitLLM.Providers.Configuration;
using ConduitLLM.WebUI.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ConduitLLM.WebUI.Services;

/// <summary>
/// Startup filter that ensures HTTP timeout configuration is properly loaded at application startup
/// </summary>
public class HttpTimeoutConfigurationStartupFilter : IStartupFilter
{
    private readonly ILogger<HttpTimeoutConfigurationStartupFilter> _logger;

    /// <summary>
    /// Creates a new instance of the HttpTimeoutConfigurationStartupFilter
    /// </summary>
    /// <param name="logger">Logger for logging startup information</param>
    public HttpTimeoutConfigurationStartupFilter(ILogger<HttpTimeoutConfigurationStartupFilter> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Configures the application startup to include timeout configuration initialization
    /// </summary>
    /// <param name="next">The next action in the startup pipeline</param>
    /// <returns>An Action that configures the application</returns>
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return builder =>
        {
            // Apply the timeout configuration during startup
            using (var scope = builder.ApplicationServices.CreateScope())
            {
                // Resolve the CORRECT DbContext factory
                var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ConduitLLM.Configuration.ConfigurationDbContext>>(); 
                
                try
                {
                    // Check if the timeout configuration exists, and create defaults if it doesn't
                    var dbContext = Task.Run(async () => await dbContextFactory.CreateDbContextAsync()).GetAwaiter().GetResult();
                    using (dbContext)
                    {
                        var timeoutSecondsKey = "HttpTimeout:TimeoutSeconds";
                        var enableLoggingKey = "HttpTimeout:EnableTimeoutLogging";
                        
                        var keys = new[] { timeoutSecondsKey, enableLoggingKey };
                        var existingSettings = Task.Run(async () => 
                            await dbContext.GlobalSettings
                                .Where(s => keys.Contains(s.Key))
                                .ToListAsync()
                        ).GetAwaiter().GetResult();
                        
                        bool hasTimeoutSeconds = existingSettings.Any(s => s.Key == timeoutSecondsKey);
                        bool hasEnableLogging = existingSettings.Any(s => s.Key == enableLoggingKey);
                        
                        // Create default settings if they don't exist
                        if (!hasTimeoutSeconds)
                        {
                            // Use the correct GlobalSetting entity type
                            dbContext.GlobalSettings.Add(new ConduitLLM.Configuration.Entities.GlobalSetting 
                            {
                                Key = timeoutSecondsKey,
                                Value = "100" // Default timeout of 100 seconds
                            });
                        }
                        
                        if (!hasEnableLogging)
                        {
                            // Use the correct GlobalSetting entity type
                            dbContext.GlobalSettings.Add(new ConduitLLM.Configuration.Entities.GlobalSetting 
                            {
                                Key = enableLoggingKey,
                                Value = "true" // Enable logging by default
                            });
                        }
                        
                        if (!hasTimeoutSeconds || !hasEnableLogging)
                        {
                            Task.Run(async () => await dbContext.SaveChangesAsync()).GetAwaiter().GetResult();
                            _logger.LogInformation("Created default HTTP timeout configuration");
                        }
                        else
                        {
                            _logger.LogInformation("HTTP timeout configuration already exists in database");
                        }
                    }
                    
                    // Now use the service to load the settings from database into the options
                    var timeoutConfigService = scope.ServiceProvider.GetRequiredService<HttpTimeoutConfigurationService>();
                    Task.Run(async () => await timeoutConfigService.LoadSettingsFromDatabaseAsync()).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error initializing HTTP timeout configuration");
                }
            }

            // Call the next step in the pipeline
            next(builder);
        };
    }
}
