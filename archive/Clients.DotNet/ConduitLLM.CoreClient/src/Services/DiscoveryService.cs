using ConduitLLM.CoreClient.Client;
using ConduitLLM.CoreClient.Constants;
using ConduitLLM.CoreClient.Models;
using ConduitLLM.CoreClient.Utils;
using ConduitLLM.CoreClient.Exceptions;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.CoreClient.Services
{
    /// <summary>
    /// Service for discovering model capabilities and provider features.
    /// </summary>
    public class DiscoveryService
    {
        private readonly BaseClient _client;
        private readonly ILogger<DiscoveryService>? _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DiscoveryService"/> class.
        /// </summary>
        /// <param name="client">The base client instance.</param>
        /// <param name="logger">Optional logger instance.</param>
        public DiscoveryService(BaseClient client, ILogger<DiscoveryService>? logger = null)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _logger = logger;
        }

        /// <summary>
        /// Gets all discovered models and their capabilities.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The models response containing all discovered models.</returns>
        public async Task<ModelsDiscoveryResponse> GetModelsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger?.LogDebug("Getting all discovered models");
                var response = await _client.GetForServiceAsync<ModelsDiscoveryResponse>("/v1/discovery/models", cancellationToken: cancellationToken);
                _logger?.LogDebug("Retrieved {ModelCount} models", response.Data?.Count ?? 0);
                return response;
            }
            catch (Exception ex) when (!(ex is ConduitCoreException))
            {
                ErrorHandler.HandleException(ex);
                throw;
            }
        }

        /// <summary>
        /// Gets models for a specific provider.
        /// </summary>
        /// <param name="provider">The provider name.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The provider models response.</returns>
        public async Task<ProviderModelsDiscoveryResponse> GetProviderModelsAsync(string provider, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(provider))
            {
                throw new ArgumentException("Provider name is required", nameof(provider));
            }

            try
            {
                _logger?.LogDebug("Getting models for provider {Provider}", provider);
                var response = await _client.GetForServiceAsync<ProviderModelsDiscoveryResponse>(
                    $"/v1/discovery/providers/{Uri.EscapeDataString(provider)}/models", 
                    cancellationToken: cancellationToken);
                _logger?.LogDebug("Retrieved {ModelCount} models for provider {Provider}", response.Data?.Count ?? 0, provider);
                return response;
            }
            catch (Exception ex) when (!(ex is ConduitCoreException))
            {
                ErrorHandler.HandleException(ex);
                throw;
            }
        }

        /// <summary>
        /// Tests if a model supports a specific capability.
        /// </summary>
        /// <param name="model">The model name.</param>
        /// <param name="capability">The capability to test.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The capability test response.</returns>
        public async Task<CapabilityTestResponse> TestModelCapabilityAsync(
            string model, 
            ModelCapability capability, 
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(model))
            {
                throw new ArgumentException("Model name is required", nameof(model));
            }

            try
            {
                _logger?.LogDebug("Testing capability {Capability} for model {Model}", capability, model);
                var response = await _client.GetForServiceAsync<CapabilityTestResponse>(
                    $"/v1/discovery/models/{Uri.EscapeDataString(model)}/capabilities/{capability}", 
                    cancellationToken: cancellationToken);
                _logger?.LogDebug("Model {Model} {Supports} capability {Capability}", model, response.Supported ? "supports" : "does not support", capability);
                return response;
            }
            catch (Exception ex) when (!(ex is ConduitCoreException))
            {
                ErrorHandler.HandleException(ex);
                throw;
            }
        }

        /// <summary>
        /// Tests multiple model capabilities in a single request.
        /// </summary>
        /// <param name="request">The bulk capability test request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Results for all requested capability tests.</returns>
        public async Task<BulkCapabilityTestResponse> TestBulkCapabilitiesAsync(
            BulkCapabilityTestRequest request, 
            CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (request.Tests == null || request.Tests.Count == 0)
            {
                throw new ArgumentException("At least one test is required", nameof(request));
            }

            try
            {
                _logger?.LogDebug("Testing {TestCount} capability tests in bulk", request.Tests.Count);
                var response = await _client.PostForServiceAsync<BulkCapabilityTestResponse>(
                    "/v1/discovery/bulk/capabilities", 
                    request, 
                    cancellationToken);
                _logger?.LogDebug("Bulk test complete: {SuccessCount} successful, {FailCount} failed", response.SuccessfulTests, response.FailedTests);
                return response;
            }
            catch (Exception ex) when (!(ex is ConduitCoreException))
            {
                ErrorHandler.HandleException(ex);
                throw;
            }
        }

        /// <summary>
        /// Gets discovery information for multiple models in a single request.
        /// </summary>
        /// <param name="request">The bulk discovery request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Discovery information for all requested models.</returns>
        public async Task<BulkModelDiscoveryResponse> GetBulkModelsAsync(
            BulkModelDiscoveryRequest request, 
            CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (request.Models == null || request.Models.Count == 0)
            {
                throw new ArgumentException("At least one model is required", nameof(request));
            }

            try
            {
                _logger?.LogDebug("Getting discovery information for {ModelCount} models", request.Models.Count);
                var response = await _client.PostForServiceAsync<BulkModelDiscoveryResponse>(
                    "/v1/discovery/bulk/models", 
                    request, 
                    cancellationToken);
                _logger?.LogDebug("Bulk discovery complete: {FoundCount} found, {NotFoundCount} not found", response.FoundModels, response.NotFoundModels);
                return response;
            }
            catch (Exception ex) when (!(ex is ConduitCoreException))
            {
                ErrorHandler.HandleException(ex);
                throw;
            }
        }

        /// <summary>
        /// Refreshes the capability cache for all providers.
        /// Requires admin/master key access.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Task representing the async operation.</returns>
        public async Task RefreshCapabilitiesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger?.LogDebug("Refreshing model capabilities cache");
                await _client.PostForServiceAsync<object>("/v1/discovery/refresh", null, cancellationToken);
                _logger?.LogDebug("Model capabilities cache refreshed successfully");
            }
            catch (Exception ex) when (!(ex is ConduitCoreException))
            {
                ErrorHandler.HandleException(ex);
                throw;
            }
        }
    }
}