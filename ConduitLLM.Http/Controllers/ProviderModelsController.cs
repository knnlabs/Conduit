using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ConduitLLM.Configuration;
using ConduitLLM.Providers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Http.Controllers
{
    /// <summary>
    /// Controller for retrieving provider model information
    /// </summary>
    [ApiController]
    [Route("api/provider-models")]
    public class ProviderModelsController : ControllerBase
    {
        private readonly IDbContextFactory<ConfigurationDbContext> _dbContextFactory;
        private readonly ModelListService _modelListService;
        private readonly ILogger<ProviderModelsController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProviderModelsController"/> class.
        /// </summary>
        /// <param name="dbContextFactory">Factory for creating database contexts.</param>
        /// <param name="modelListService">Service for retrieving model lists from providers.</param>
        /// <param name="logger">Logger for diagnostic information.</param>
        public ProviderModelsController(
            IDbContextFactory<ConfigurationDbContext> dbContextFactory,
            ModelListService modelListService,
            ILogger<ProviderModelsController> logger)
        {
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
            _modelListService = modelListService ?? throw new ArgumentNullException(nameof(modelListService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets available models for a specified provider
        /// </summary>
        /// <param name="providerName">Name of the provider</param>
        /// <param name="forceRefresh">Whether to bypass cache and force refresh</param>
        /// <returns>List of available model IDs</returns>
        [HttpGet("{providerName}")]
        [ProducesResponseType(typeof(List<string>), 200)]
        [ProducesResponseType(typeof(object), 404)]
        [ProducesResponseType(typeof(object), 500)]
        public async Task<IActionResult> GetProviderModels(
            string providerName, 
            [FromQuery] bool forceRefresh = false)
        {
            try
            {
                _logger.LogInformation("Getting models for provider {ProviderName} (forceRefresh: {ForceRefresh})", 
                    providerName, forceRefresh);
                
                // Get the provider credentials from the database
                await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
                // EF Core can't translate StringComparison.OrdinalIgnoreCase, use ToLower() instead
                var providerNameLower = providerName.ToLower();
                var provider = await dbContext.ProviderCredentials
                    .FirstOrDefaultAsync(p => p.ProviderName.ToLower() == providerNameLower);

                if (provider == null)
                {
                    _logger.LogWarning("Provider '{ProviderName}' not found", providerName);
                    return NotFound(new { error = $"Provider '{providerName}' not found" });
                }

                if (string.IsNullOrEmpty(provider.ApiKey))
                {
                    _logger.LogWarning("API key missing for provider '{ProviderName}'", providerName);
                    return BadRequest(new { error = "API key is required to retrieve models" });
                }

                // Convert DB entity to ProviderCredentials
                var providerCredentials = new ProviderCredentials
                {
                    ProviderName = provider.ProviderName,
                    ApiKey = provider.ApiKey,
                    ApiBase = provider.BaseUrl,
                    ApiVersion = provider.ApiVersion
                };
                
                // Use the model list service to get models
                var models = await _modelListService.GetModelsForProviderAsync(providerCredentials, forceRefresh);
                
                // Sort the models alphabetically for better UX
                var sortedModels = models
                    .OrderBy(m => m, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                
                _logger.LogInformation("Retrieved {ModelsCount} models for provider {ProviderName}", 
                    sortedModels.Count, providerName);
                    
                return Ok(sortedModels);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving models for provider {ProviderName}", providerName);
                return StatusCode(500, new { error = $"Failed to retrieve models: {ex.Message}" });
            }
        }
    }
}