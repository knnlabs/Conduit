using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using ConduitLLM.AdminClient.Client;
using ConduitLLM.AdminClient.Exceptions;
using ConduitLLM.AdminClient.Models;
using ConduitLLM.AdminClient.Utils;

namespace ConduitLLM.AdminClient.Services
{
    /// <summary>
    /// Service for managing audio provider configurations, cost settings, and usage analytics.
    /// </summary>
    public class AudioConfigurationService : BaseApiClient
    {
        private const string ProvidersEndpoint = "/api/admin/audio/providers";
        private const string CostsEndpoint = "/api/admin/audio/costs";
        private const string UsageEndpoint = "/api/admin/audio/usage";
        private const string SessionsEndpoint = "/api/admin/audio/sessions";

        /// <summary>
        /// Initializes a new instance of the <see cref="AudioConfigurationService"/> class.
        /// </summary>
        /// <param name="httpClient">The HTTP client to use for requests.</param>
        /// <param name="configuration">The client configuration.</param>
        /// <param name="logger">Optional logger instance.</param>
        /// <param name="cache">Optional memory cache instance.</param>
        public AudioConfigurationService(
            HttpClient httpClient,
            ConduitAdminClientConfiguration configuration,
            ILogger<AudioConfigurationService>? logger = null,
            IMemoryCache? cache = null)
            : base(httpClient, configuration, logger, cache)
        {
        }

        #region Provider Configuration

        /// <summary>
        /// Creates a new audio provider configuration.
        /// </summary>
        /// <param name="request">The provider configuration request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The created provider configuration.</returns>
        /// <exception cref="ValidationException">Thrown when the request is invalid.</exception>
        /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
        public async Task<AudioProviderConfigDto> CreateProviderAsync(
            AudioProviderConfigRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                ValidateProviderRequest(request);
                
                // Creating audio provider configuration

                var response = await PostAsync<AudioProviderConfigDto>(
                    ProvidersEndpoint,
                    request,
                    cancellationToken);

                // Successfully created audio provider
                return response;
            }
            catch (Exception ex) when (!(ex is ConduitAdminException))
            {
                ErrorHandler.HandleException(ex);
                throw;
            }
        }

        /// <summary>
        /// Gets all audio provider configurations.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of provider configurations.</returns>
        /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
        public async Task<List<AudioProviderConfigDto>> GetProvidersAsync(
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Retrieving all audio provider configurations

                var response = await GetAsync<List<AudioProviderConfigDto>>(
                    ProvidersEndpoint,
                    cancellationToken);

                // Retrieved audio provider configurations
                return response;
            }
            catch (Exception ex) when (!(ex is ConduitAdminException))
            {
                ErrorHandler.HandleException(ex);
                throw;
            }
        }

        /// <summary>
        /// Tests the connectivity and configuration of an audio provider.
        /// </summary>
        /// <param name="providerId">The provider ID to test.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The test result.</returns>
        /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
        public async Task<AudioProviderTestResult> TestProviderAsync(
            string providerId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(providerId))
                    throw new ValidationException("Provider ID is required", "providerId");

                // Testing audio provider

                var endpoint = $"{ProvidersEndpoint}/{Uri.EscapeDataString(providerId)}/test";
                var response = await PostAsync<AudioProviderTestResult>(
                    endpoint,
                    new { },
                    cancellationToken);

                // Provider test completed
                
                return response;
            }
            catch (Exception ex) when (!(ex is ConduitAdminException))
            {
                ErrorHandler.HandleException(ex);
                throw;
            }
        }

        #endregion

        #region Cost Configuration

        /// <summary>
        /// Creates a new audio cost configuration.
        /// </summary>
        /// <param name="request">The cost configuration request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The created cost configuration.</returns>
        /// <exception cref="ValidationException">Thrown when the request is invalid.</exception>
        /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
        public async Task<AudioCostConfigDto> CreateCostConfigAsync(
            AudioCostConfigRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                ValidateCostConfigRequest(request);
                
                // Creating audio cost configuration

                var response = await PostAsync<AudioCostConfigDto>(
                    CostsEndpoint,
                    request,
                    cancellationToken);

                // Successfully created audio cost configuration
                return response;
            }
            catch (Exception ex) when (!(ex is ConduitAdminException))
            {
                ErrorHandler.HandleException(ex);
                throw;
            }
        }

        /// <summary>
        /// Gets all audio cost configurations.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of cost configurations.</returns>
        /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
        public async Task<List<AudioCostConfigDto>> GetCostConfigsAsync(
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Retrieving all audio cost configurations

                var response = await GetAsync<List<AudioCostConfigDto>>(
                    CostsEndpoint,
                    cancellationToken);

                // Retrieved audio cost configurations
                return response;
            }
            catch (Exception ex) when (!(ex is ConduitAdminException))
            {
                ErrorHandler.HandleException(ex);
                throw;
            }
        }

        #endregion

        #region Usage Analytics

        /// <summary>
        /// Gets audio usage data with optional filtering.
        /// </summary>
        /// <param name="startDate">Optional start date filter.</param>
        /// <param name="endDate">Optional end date filter.</param>
        /// <param name="virtualKey">Optional virtual key filter.</param>
        /// <param name="provider">Optional provider filter.</param>
        /// <param name="operationType">Optional operation type filter.</param>
        /// <param name="page">Page number for pagination (1-based).</param>
        /// <param name="pageSize">Number of items per page.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Paginated list of usage data.</returns>
        /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
        public async Task<PagedResponse<AudioUsageDto>> GetUsageAsync(
            DateTime? startDate = null,
            DateTime? endDate = null,
            string? virtualKey = null,
            string? provider = null,
            string? operationType = null,
            int page = 1,
            int pageSize = 50,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Retrieving audio usage data with filters

                var queryParams = new List<string>();
                
                if (startDate.HasValue)
                    queryParams.Add($"startDate={startDate.Value:yyyy-MM-ddTHH:mm:ssZ}");
                
                if (endDate.HasValue)
                    queryParams.Add($"endDate={endDate.Value:yyyy-MM-ddTHH:mm:ssZ}");
                
                if (!string.IsNullOrWhiteSpace(virtualKey))
                    queryParams.Add($"virtualKey={Uri.EscapeDataString(virtualKey)}");
                
                if (!string.IsNullOrWhiteSpace(provider))
                    queryParams.Add($"provider={Uri.EscapeDataString(provider)}");
                
                if (!string.IsNullOrWhiteSpace(operationType))
                    queryParams.Add($"operationType={Uri.EscapeDataString(operationType)}");
                
                queryParams.Add($"page={page}");
                queryParams.Add($"pageSize={pageSize}");

                var endpoint = $"{UsageEndpoint}?{string.Join("&", queryParams)}";
                var response = await GetAsync<PagedResponse<AudioUsageDto>>(
                    endpoint,
                    cancellationToken);

                // Retrieved usage records
                
                return response;
            }
            catch (Exception ex) when (!(ex is ConduitAdminException))
            {
                ErrorHandler.HandleException(ex);
                throw;
            }
        }

        /// <summary>
        /// Gets audio usage summary for a date range.
        /// </summary>
        /// <param name="startDate">Start date for the summary.</param>
        /// <param name="endDate">End date for the summary.</param>
        /// <param name="virtualKey">Optional virtual key filter.</param>
        /// <param name="provider">Optional provider filter.</param>
        /// <param name="operationType">Optional operation type filter.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Usage summary data.</returns>
        /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
        public async Task<AudioUsageSummaryDto> GetUsageSummaryAsync(
            DateTime startDate,
            DateTime endDate,
            string? virtualKey = null,
            string? provider = null,
            string? operationType = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Retrieving audio usage summary

                var queryParams = new List<string>
                {
                    $"startDate={startDate:yyyy-MM-ddTHH:mm:ssZ}",
                    $"endDate={endDate:yyyy-MM-ddTHH:mm:ssZ}"
                };
                
                if (!string.IsNullOrWhiteSpace(virtualKey))
                    queryParams.Add($"virtualKey={Uri.EscapeDataString(virtualKey)}");
                
                if (!string.IsNullOrWhiteSpace(provider))
                    queryParams.Add($"provider={Uri.EscapeDataString(provider)}");
                
                if (!string.IsNullOrWhiteSpace(operationType))
                    queryParams.Add($"operationType={Uri.EscapeDataString(operationType)}");

                var endpoint = $"{UsageEndpoint}/summary?{string.Join("&", queryParams)}";
                var response = await GetAsync<AudioUsageSummaryDto>(
                    endpoint,
                    cancellationToken);

                // Retrieved usage summary
                
                return response;
            }
            catch (Exception ex) when (!(ex is ConduitAdminException))
            {
                ErrorHandler.HandleException(ex);
                throw;
            }
        }

        #endregion

        #region Real-time Sessions

        /// <summary>
        /// Gets all active real-time audio sessions.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of active sessions.</returns>
        /// <exception cref="ConduitAdminException">Thrown when the API request fails.</exception>
        public async Task<List<RealtimeSessionDto>> GetActiveSessionsAsync(
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Retrieving active audio sessions

                var response = await GetAsync<List<RealtimeSessionDto>>(
                    SessionsEndpoint,
                    cancellationToken);

                // Retrieved active audio sessions
                return response;
            }
            catch (Exception ex) when (!(ex is ConduitAdminException))
            {
                ErrorHandler.HandleException(ex);
                throw;
            }
        }

        #endregion

        #region Validation

        private static void ValidateProviderRequest(AudioProviderConfigRequest request)
        {
            if (request == null)
                throw new ValidationException("Provider configuration request cannot be null", "request");

            if (string.IsNullOrWhiteSpace(request.Name))
                throw new ValidationException("Provider name is required", "name");

            if (string.IsNullOrWhiteSpace(request.BaseUrl))
                throw new ValidationException("Base URL is required", "baseUrl");

            if (string.IsNullOrWhiteSpace(request.ApiKey))
                throw new ValidationException("API key is required", "apiKey");

            if (!Uri.TryCreate(request.BaseUrl, UriKind.Absolute, out var uri) ||
                (uri.Scheme != "http" && uri.Scheme != "https"))
            {
                throw new ValidationException("Base URL must be a valid HTTP or HTTPS URL", "baseUrl");
            }

            if (request.TimeoutSeconds <= 0 || request.TimeoutSeconds > 300)
                throw new ValidationException("Timeout must be between 1 and 300 seconds", "timeoutSeconds");

            if (request.Priority < 1)
                throw new ValidationException("Priority must be at least 1", "priority");
        }

        private static void ValidateCostConfigRequest(AudioCostConfigRequest request)
        {
            if (request == null)
                throw new ValidationException("Cost configuration request cannot be null", "request");

            if (string.IsNullOrWhiteSpace(request.ProviderId))
                throw new ValidationException("Provider ID is required", "providerId");

            if (string.IsNullOrWhiteSpace(request.OperationType))
                throw new ValidationException("Operation type is required", "operationType");

            if (string.IsNullOrWhiteSpace(request.UnitType))
                throw new ValidationException("Unit type is required", "unitType");

            if (request.CostPerUnit < 0)
                throw new ValidationException("Cost per unit cannot be negative", "costPerUnit");

            if (request.EffectiveFrom.HasValue && request.EffectiveTo.HasValue &&
                request.EffectiveFrom >= request.EffectiveTo)
            {
                throw new ValidationException("Effective from date must be before effective to date", "effectiveFrom");
            }
        }

        #endregion
    }
}