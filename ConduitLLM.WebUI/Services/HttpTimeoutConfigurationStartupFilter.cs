using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ConduitLLM.WebUI.Interfaces;

namespace ConduitLLM.WebUI.Services
{
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
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
                    var adminApiClient = scope.ServiceProvider.GetRequiredService<IAdminApiClient>();
                    
                    try
                    {
                        // Initialize default configuration if needed via the API
                        var initialized = Task.Run(async () => 
                            await adminApiClient.InitializeHttpTimeoutConfigurationAsync()
                        ).GetAwaiter().GetResult();
                        
                        if (initialized)
                        {
                            _logger.LogInformation("HTTP timeout configuration initialized successfully");
                        }
                        
                        // Now load the settings into the application options
                        var timeoutConfigService = scope.ServiceProvider.GetRequiredService<IHttpTimeoutConfigurationService>();
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
}