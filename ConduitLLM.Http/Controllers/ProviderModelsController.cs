using ConduitLLM.Configuration;

using Microsoft.AspNetCore.Mvc;
using ConduitLLM.Configuration.DTOs;
using Microsoft.EntityFrameworkCore;

namespace ConduitLLM.Http.Controllers
{
    /// <summary>
    /// Controller for retrieving provider model information
    /// </summary>
    [ApiController]
    [Route("api/provider-models")]
    public class ProviderModelsController : ControllerBase
    {
        private readonly IDbContextFactory<ConduitDbContext> _dbContextFactory;
        private readonly ILogger<ProviderModelsController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProviderModelsController"/> class.
        /// </summary>
        /// <param name="dbContextFactory">Factory for creating database contexts.</param>
        /// <param name="logger">Logger for diagnostic information.</param>
        public ProviderModelsController(
            IDbContextFactory<ConduitDbContext> dbContextFactory,
            ILogger<ProviderModelsController> logger)
        {
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets models that are compatible with a specified provider based on provider type
        /// </summary>
        /// <param name="providerId">ID of the provider</param>
        /// <returns>List of model identifiers that can be used with this provider</returns>
        [HttpGet("{providerId:int}")]
        [ProducesResponseType(typeof(List<string>), 200)]
        [ProducesResponseType(typeof(object), 404)]
        public async Task<IActionResult> GetProviderModels(int providerId)
        {
            try
            {
                _logger.LogInformation("Getting compatible models for provider {ProviderId}", providerId);

                await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
                
                // Get the provider to determine its type
                var provider = await dbContext.Providers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == providerId);

                if (provider == null)
                {
                    _logger.LogWarning("Provider with ID {ProviderId} not found", providerId);
                    return NotFound(new ErrorResponseDto($"Provider with ID {providerId} not found"));
                }

                // Get all models that have the appropriate capabilities for this provider type
                // For now, we'll return model identifiers that are commonly used with this provider type
                var query = dbContext.Models
                    .Include(m => m.Capabilities)
                    .Include(m => m.Identifiers)
                    .Where(m => m.IsActive);

                // Filter models based on provider type capabilities
                switch (provider.ProviderType)
                {
                    case ProviderType.OpenAI:
                    case ProviderType.OpenAICompatible:
                        query = query.Where(m => m.Capabilities.SupportsChat || 
                                                 m.Capabilities.SupportsImageGeneration ||
                                                 m.Capabilities.SupportsEmbeddings);
                        break;
                    
                    case ProviderType.Replicate:
                        // Replicate supports various model types including video
                        query = query.Where(m => m.Capabilities.SupportsImageGeneration || 
                                                 m.Capabilities.SupportsVideoGeneration ||
                                                 m.Capabilities.SupportsChat);
                        break;
                    
                    
                    case ProviderType.Groq:
                    case ProviderType.Cerebras:
                    case ProviderType.SambaNova:
                    case ProviderType.Fireworks:
                        // Fast inference providers typically support chat models
                        query = query.Where(m => m.Capabilities.SupportsChat);
                        break;
                    
                    default:
                        // For other providers, return all active models
                        break;
                }

                var models = await query
                    .AsNoTracking()
                    .ToListAsync();

                // Get the model identifiers that are most commonly used
                // Prefer identifiers that match the provider type if available
                var modelIdentifiers = new List<string>();
                
                // Map provider type to common provider strings
                var providerName = provider.ProviderType.ToString().ToLowerInvariant();
                
                foreach (var model in models)
                {
                    // First, check if there's a provider-specific identifier
                    var providerSpecificId = model.Identifiers
                        .FirstOrDefault(i => !string.IsNullOrEmpty(i.Provider) && 
                                           i.Provider.Equals(providerName, StringComparison.OrdinalIgnoreCase));
                    
                    if (providerSpecificId != null)
                    {
                        modelIdentifiers.Add(providerSpecificId.Identifier);
                    }
                    else if (model.Identifiers.Any())
                    {
                        // Prefer primary identifier if available
                        var primaryId = model.Identifiers.FirstOrDefault(i => i.IsPrimary);
                        if (primaryId != null)
                        {
                            modelIdentifiers.Add(primaryId.Identifier);
                        }
                        else
                        {
                            // Use the first available identifier
                            modelIdentifiers.Add(model.Identifiers.First().Identifier);
                        }
                    }
                    else
                    {
                        // Fall back to the model name
                        modelIdentifiers.Add(model.Name.ToLowerInvariant().Replace(" ", "-"));
                    }
                }

                // Sort alphabetically for better UX
                var sortedIdentifiers = modelIdentifiers
                    .Distinct()
                    .OrderBy(m => m, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                _logger.LogInformation("Found {ModelsCount} compatible models for provider {ProviderId} (type: {ProviderType})",
                    sortedIdentifiers.Count, providerId, provider.ProviderType);

                return Ok(sortedIdentifiers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving models for provider {ProviderId}", providerId);
                return StatusCode(500, new ErrorResponseDto($"Failed to retrieve models: {ex.Message}"));
            }
        }
    }
}
