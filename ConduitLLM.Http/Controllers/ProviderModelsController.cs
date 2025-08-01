using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Interfaces.Configuration;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static ConduitLLM.Core.Extensions.LoggingSanitizer;

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
        private readonly IModelListService _modelListService;
        private readonly ILogger<ProviderModelsController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProviderModelsController"/> class.
        /// </summary>
        /// <param name="dbContextFactory">Factory for creating database contexts.</param>
        /// <param name="modelListService">Service for retrieving model lists from providers.</param>
        /// <param name="logger">Logger for diagnostic information.</param>
        public ProviderModelsController(
            IDbContextFactory<ConfigurationDbContext> dbContextFactory,
            IModelListService modelListService,
            ILogger<ProviderModelsController> logger)
        {
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
            _modelListService = modelListService ?? throw new ArgumentNullException(nameof(modelListService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets available models for a specified provider
        /// </summary>
        /// <param name="providerId">ID of the provider</param>
        /// <param name="forceRefresh">Whether to bypass cache and force refresh</param>
        /// <returns>List of available model IDs</returns>
        [HttpGet("{providerId:int}")]
        [ProducesResponseType(typeof(List<string>), 200)]
        [ProducesResponseType(typeof(object), 404)]
        [ProducesResponseType(typeof(object), 500)]
        public async Task<IActionResult> GetProviderModels(
            int providerId,
            [FromQuery] bool forceRefresh = false)
        {
            try
            {
                _logger.LogInformation("Getting models for provider {ProviderId} (forceRefresh: {ForceRefresh})",
                    providerId, forceRefresh);

                // Get the provider credentials from the database
                await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
                
                var provider = await dbContext.Providers
                    .Include(p => p.ProviderKeyCredentials)
                    .FirstOrDefaultAsync(p => p.Id == providerId);

                if (provider == null)
                {
                    _logger.LogWarning("Provider with ID {ProviderId} not found", providerId);
                    return NotFound(new { error = $"Provider with ID {providerId} not found" });
                }

                // Get the primary key or first enabled key from ProviderKeyCredentials
                var primaryKey = provider.ProviderKeyCredentials?
                    .FirstOrDefault(k => k.IsPrimary && k.IsEnabled) ??
                    provider.ProviderKeyCredentials?.FirstOrDefault(k => k.IsEnabled);

                if (primaryKey == null || string.IsNullOrEmpty(primaryKey.ApiKey))
                {
                    _logger.LogWarning("API key missing for provider {ProviderId}", providerId);
                    return BadRequest(new { error = "API key is required to retrieve models" });
                }

                // Use the model list service to get models
                var models = await _modelListService.GetModelsForProviderAsync(provider, primaryKey, forceRefresh);

                // Sort the models alphabetically for better UX
                var sortedModels = models
                    .OrderBy(m => m, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                _logger.LogInformation("Retrieved {ModelsCount} models for provider {ProviderId}",
                    sortedModels.Count, providerId);

                return Ok(sortedModels);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving models for provider {ProviderId}", providerId);
                return StatusCode(500, new { error = $"Failed to retrieve models: {ex.Message}" });
            }
        }
    }
}
