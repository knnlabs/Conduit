using ConduitLLM.CoreClient.Client;
using ConduitLLM.CoreClient.Utils;
using ConduitLLM.CoreClient.Exceptions;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.CoreClient.Services
{
    /// <summary>
    /// Service for retrieving provider model information.
    /// </summary>
    public class ProviderModelsService
    {
        private readonly BaseClient _client;
        private readonly ILogger<ProviderModelsService>? _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProviderModelsService"/> class.
        /// </summary>
        /// <param name="client">The base client instance.</param>
        /// <param name="logger">Optional logger instance.</param>
        public ProviderModelsService(BaseClient client, ILogger<ProviderModelsService>? logger = null)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _logger = logger;
        }

        /// <summary>
        /// Gets available models for a specified provider.
        /// </summary>
        /// <param name="providerName">Name of the provider.</param>
        /// <param name="forceRefresh">Whether to bypass cache and force refresh.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of available model IDs.</returns>
        public async Task<List<string>> GetProviderModelsAsync(
            string providerName, 
            bool forceRefresh = false,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(providerName))
            {
                throw new ArgumentException("Provider name is required", nameof(providerName));
            }

            try
            {
                _logger?.LogDebug("Getting models for provider {ProviderName} (forceRefresh: {ForceRefresh})", providerName, forceRefresh);
                
                var url = $"/api/provider-models/{Uri.EscapeDataString(providerName)}";
                if (forceRefresh)
                {
                    url += "?forceRefresh=true";
                }

                var response = await _client.GetForServiceAsync<List<string>>(url, cancellationToken: cancellationToken);
                _logger?.LogDebug("Retrieved {ModelCount} models for provider {ProviderName}", response?.Count ?? 0, providerName);
                return response ?? new List<string>();
            }
            catch (Exception ex) when (!(ex is ConduitCoreException))
            {
                ErrorHandler.HandleException(ex);
                throw;
            }
        }
    }
}