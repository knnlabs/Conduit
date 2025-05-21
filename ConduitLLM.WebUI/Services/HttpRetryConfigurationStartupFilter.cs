using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ConduitLLM.WebUI.Interfaces;

namespace ConduitLLM.WebUI.Services
{
    /// <summary>
    /// Startup filter to initialize HTTP retry settings from the Admin API during application startup.
    /// </summary>
    public class HttpRetryConfigurationStartupFilter : IStartupFilter
    {
        private readonly ILogger<HttpRetryConfigurationStartupFilter> _logger;

        /// <summary>
        /// Initializes a new instance of the HttpRetryConfigurationStartupFilter class
        /// </summary>
        /// <param name="logger">Logger for tracking startup operations</param>
        public HttpRetryConfigurationStartupFilter(ILogger<HttpRetryConfigurationStartupFilter> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            return builder =>
            {
                // Apply the retry configuration during startup
                using (var scope = builder.ApplicationServices.CreateScope())
                {
                    var adminApiClient = scope.ServiceProvider.GetRequiredService<IAdminApiClient>();
                    
                    try
                    {
                        // Initialize default configuration if needed via the API
                        var initialized = Task.Run(async () => 
                            await adminApiClient.InitializeHttpRetryConfigurationAsync()
                        ).GetAwaiter().GetResult();
                        
                        if (initialized)
                        {
                            _logger.LogInformation("HTTP retry configuration initialized successfully");
                        }
                        
                        // Now load the settings into the application options
                        var retryConfigService = scope.ServiceProvider.GetRequiredService<IHttpRetryConfigurationService>();
                        Task.Run(async () => await retryConfigService.LoadSettingsFromDatabaseAsync()).GetAwaiter().GetResult();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error initializing HTTP retry configuration");
                    }
                }

                // Call the next step in the pipeline
                next(builder);
            };
        }
    }
}