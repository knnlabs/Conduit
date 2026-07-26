using ConduitLLM.Configuration;
using ConduitLLM.Core.Models;
using ConduitLLM.Gateway.Filters;

using Microsoft.EntityFrameworkCore;

namespace ConduitLLM.Gateway.Endpoints
{
    /// <summary>
    /// Controller for retrieving provider model information
    /// </summary>
    public class ProviderModelsEndpoints : GatewayEndpointHandlerBase
    {
        private readonly IDbContextFactory<ConduitDbContext> _dbContextFactory;

        /// <summary>
        /// Initializes the Provider Models endpoint handler.
        /// </summary>
        /// <param name="dbContextFactory">Factory for creating database contexts.</param>
        /// <param name="httpContextAccessor">Accessor for the current request context.</param>
        /// <param name="logger">Logger for diagnostic information.</param>
        public ProviderModelsEndpoints(
            IDbContextFactory<ConduitDbContext> dbContextFactory,
            IHttpContextAccessor httpContextAccessor,
            ILogger<ProviderModelsEndpoints> logger)
            : base(null, httpContextAccessor, logger)
        {
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        }

        /// <summary>
        /// Gets models that are compatible with a specified provider based on provider type
        /// </summary>
        /// <param name="providerId">ID of the provider</param>
        /// <returns>List of model identifiers that can be used with this provider</returns>
        public async Task<IResult> GetProviderModels(int providerId)
        {
            Logger.LogInformation("Getting compatible models for provider {ProviderId}", providerId);

            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

            // Get the provider to determine its type
            var provider = await dbContext.Providers
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == providerId);

            if (provider == null)
            {
                Logger.LogWarning("Provider with ID {ProviderId} not found", providerId);
                return OpenAIError(404, $"Provider with ID {providerId} not found", "not_found", "not_found_error");
            }

            // Associations are the source of truth for provider compatibility. This avoids
            // guessing from broad operation flags (for example, video input is not video generation).
            var modelIdentifiers = await dbContext.ModelProviderTypeAssociations
                .AsNoTracking()
                .Where(a => a.IsEnabled &&
                            a.Model.IsActive &&
                            (a.Provider == provider.ProviderType || a.Provider == null))
                .Select(a => a.Identifier)
                .ToListAsync();

            // Sort alphabetically for better UX
            var sortedIdentifiers = modelIdentifiers
                .Distinct()
                .OrderBy(m => m, StringComparer.OrdinalIgnoreCase)
                .ToList();

            Logger.LogInformation("Found {ModelsCount} compatible models for provider {ProviderId} (type: {ProviderType})",
                sortedIdentifiers.Count, providerId, provider.ProviderType);

            return Ok(sortedIdentifiers);
        }
    }
}
