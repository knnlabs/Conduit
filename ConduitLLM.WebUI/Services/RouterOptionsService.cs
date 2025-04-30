using System;
using System.Text.Json;
using System.Threading.Tasks;

using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Data;
using ConduitLLM.Configuration.Options;
using ConduitLLM.WebUI.Interfaces;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.WebUI.Services
{
    /// <summary>
    /// Service for managing router options
    /// </summary>
    public class RouterOptionsService
    {
        private readonly IDbContextFactory<ConfigurationDbContext> _dbContextFactory;
        private readonly IGlobalSettingService _settingService;
        private readonly ILogger<RouterOptionsService> _logger;
        private const string ROUTER_OPTIONS_KEY = "RouterOptions";

        /// <summary>
        /// Creates a new instance of RouterOptionsService
        /// </summary>
        public RouterOptionsService(
            IDbContextFactory<ConfigurationDbContext> dbContextFactory,
            IGlobalSettingService settingService,
            ILogger<RouterOptionsService> logger)
        {
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
            _settingService = settingService ?? throw new ArgumentNullException(nameof(settingService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets the current router options
        /// </summary>
        public async Task<RouterOptions> GetRouterOptionsAsync()
        {
            try
            {
                var optionsJson = await _settingService.GetSettingAsync(ROUTER_OPTIONS_KEY);
                
                if (!string.IsNullOrEmpty(optionsJson))
                {
                    return JsonSerializer.Deserialize<RouterOptions>(optionsJson) ?? new RouterOptions();
                }
                
                return new RouterOptions();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving router options");
                return new RouterOptions();
            }
        }

        /// <summary>
        /// Saves the router options
        /// </summary>
        public async Task<bool> SaveRouterOptionsAsync(RouterOptions options)
        {
            try
            {
                // Preserve the options as a JSON string
                var optionsJson = JsonSerializer.Serialize(options);
                
                // Save via the global settings service
                await _settingService.SetSettingAsync(ROUTER_OPTIONS_KEY, optionsJson);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving router options");
                return false;
            }
        }

        /// <summary>
        /// Sets the router enabled state
        /// </summary>
        public async Task<bool> SetRouterEnabledAsync(bool enabled)
        {
            try
            {
                var options = await GetRouterOptionsAsync();
                
                // Update the enabled state
                options.Enabled = enabled;
                
                // Save the updated options
                return await SaveRouterOptionsAsync(options);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting router enabled state");
                return false;
            }
        }

        /// <summary>
        /// Gets the router enabled state
        /// </summary>
        public async Task<bool> GetRouterEnabledAsync()
        {
            try
            {
                var options = await GetRouterOptionsAsync();
                return options.Enabled;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting router enabled state");
                return false;
            }
        }
    }
}
