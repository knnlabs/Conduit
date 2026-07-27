using ConduitLLM.Providers.Common.Models;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers.SambaNova
{
    /// <summary>
    /// SambaNovaClient partial class containing model listing functionality.
    /// </summary>
    public partial class SambaNovaClient
    {
        /// <summary>
        /// Gets available models for SambaNova.
        /// </summary>
        /// <param name="apiKey">Optional API key to override the one in credentials.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A list of available SambaNova models.</returns>
        /// <remarks>
        /// SambaNova does not provide a public models endpoint, so this returns the
        /// curated static list maintained in <c>SambaNovaModels</c>.
        /// </remarks>
        public override Task<List<ExtendedModelInfo>> GetModelsAsync(
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(SambaNovaModels);
        }

        /// <summary>
        /// Lists the models available from SambaNova.
        /// </summary>
        /// <param name="apiKey">Optional API key override to use instead of the client's configured key.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A list of available model IDs.</returns>
        /// <remarks>
        /// This implementation returns model IDs from our static configuration since
        /// SambaNova may not provide a public models endpoint.
        /// </remarks>
        public override async Task<List<string>> ListModelsAsync(
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                Logger.LogDebug("Listing SambaNova models from static configuration");
                
                // Get the full model info
                var models = await GetModelsAsync(apiKey, cancellationToken);
                
                // Return just the model IDs
                var modelIds = models.Select(m => m.Id).ToList();
                
                Logger.LogInformation("Returning {Count} SambaNova model IDs", modelIds.Count);
                return modelIds;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error listing SambaNova models, returning fallback list");
                // Return fallback model IDs
                return SambaNovaModels.Select(m => m.Id).ToList();
            }
        }
    }
}