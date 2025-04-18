using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.WebUI.Services;

/// <summary>
/// Startup filter to initialize HTTP retry settings from the database during application startup.
/// </summary>
public class HttpRetryConfigurationStartupFilter : IStartupFilter
{
    private readonly ILogger<HttpRetryConfigurationStartupFilter> _logger;

    public HttpRetryConfigurationStartupFilter(ILogger<HttpRetryConfigurationStartupFilter> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            // Initialize HTTP retry settings after the application is fully configured
            app.Use(async (context, nextMiddleware) =>
            {
                try
                {
                    // Only run this once at the start
                    if (context.Request.Path == "/")
                    {
                        var httpRetryConfigService = context.RequestServices.GetRequiredService<HttpRetryConfigurationService>();
                        _logger.LogInformation("Initializing HTTP retry settings from database");
                        await httpRetryConfigService.LoadSettingsFromDatabaseAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error initializing HTTP retry settings");
                }

                await nextMiddleware();
            });

            next(app);
        };
    }
}
