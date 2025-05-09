using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.WebUI.Data;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.WebUI.Services
{
    /// <summary>
    /// Provides functionality to check the status of LLM providers.
    /// </summary>
    public class ProviderStatusService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IDbContextFactory<ConduitLLM.Configuration.ConfigurationDbContext> _configContextFactory;
        private readonly ILogger<ProviderStatusService> _logger;
        private readonly IProviderHealthRepository? _providerHealthRepository;
        private readonly Stopwatch _stopwatch = new Stopwatch();

        /// <summary>
        /// Initializes a new instance of the ProviderStatusService class.
        /// </summary>
        /// <param name="httpClientFactory">The HTTP client factory</param>
        /// <param name="configContextFactory">The database context factory</param>
        /// <param name="logger">The logger</param>
        /// <param name="providerHealthRepository">Optional repository for recording health checks</param>
        public ProviderStatusService(
            IHttpClientFactory httpClientFactory,
            IDbContextFactory<ConduitLLM.Configuration.ConfigurationDbContext> configContextFactory, 
            ILogger<ProviderStatusService> logger,
            IProviderHealthRepository? providerHealthRepository = null)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _configContextFactory = configContextFactory ?? throw new ArgumentNullException(nameof(configContextFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _providerHealthRepository = providerHealthRepository;
        }

        /// <summary>
        /// Check the status of all configured providers
        /// </summary>
        /// <returns>Dictionary mapping provider names to their status</returns>
        public async Task<Dictionary<string, ProviderStatus>> CheckAllProvidersStatusAsync()
        {
            var result = new Dictionary<string, ProviderStatus>();
            
            try
            {
                using var dbContext = await _configContextFactory.CreateDbContextAsync();
                var providers = await dbContext.ProviderCredentials.ToListAsync();
                
                foreach (var provider in providers)
                {
                    try
                    {
                        var status = await CheckProviderStatusAsync(provider);
                        result[provider.ProviderName] = status;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error checking status for provider {ProviderName}", provider.ProviderName);
                        result[provider.ProviderName] = new ProviderStatus 
                        { 
                            IsOnline = false,
                            StatusMessage = $"Error: {ex.Message}",
                            ErrorCategory = CategorizeError(ex.Message),
                            ResponseTimeMs = 0
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving providers from database");
            }
            
            return result;
        }

        /// <summary>
        /// Check the status of a specific provider
        /// </summary>
        /// <param name="provider">The provider credentials to check</param>
        /// <param name="timeoutSeconds">Optional timeout in seconds (default is 10)</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>The status of the provider</returns>
        public async Task<ProviderStatus> CheckProviderStatusAsync(
            ProviderCredential provider,
            int timeoutSeconds = 10,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(provider.ApiKey))
            {
                return new ProviderStatus
                {
                    Status = ProviderStatus.StatusType.Unknown,
                    StatusMessage = "No API key configured",
                    LastCheckedUtc = DateTime.UtcNow,
                    ErrorCategory = "Configuration"
                };
            }

            try
            {
                _stopwatch.Restart();

                // Get the appropriate endpoint for this provider
                var requestUri = GetProviderTestEndpoint(provider);
                if (string.IsNullOrEmpty(requestUri))
                {
                    _stopwatch.Stop();
                    return new ProviderStatus
                    {
                        Status = ProviderStatus.StatusType.Unknown,
                        StatusMessage = "Unknown provider or no endpoint configured",
                        LastCheckedUtc = DateTime.UtcNow,
                        ErrorCategory = "Configuration",
                        ResponseTimeMs = _stopwatch.ElapsedMilliseconds
                    };
                }

                // Create a minimal request to test the API
                using var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);

                string? customEndpointUrl = await GetCustomEndpointUrlAsync(provider.ProviderName);
                string actualEndpoint = customEndpointUrl ?? requestUri;

                ProviderStatus status;

                // For Gemini, the API key is passed as a query parameter instead of an Authorization header
                if (provider.ProviderName.Equals("Gemini", StringComparison.OrdinalIgnoreCase))
                {
                    // Add the API key as a query parameter
                    var separator = actualEndpoint.Contains("?") ? "&" : "?";
                    var requestUriWithKey = $"{actualEndpoint}{separator}key={provider.ApiKey}";
                    
                    using var request = new HttpRequestMessage(HttpMethod.Get, requestUriWithKey);
                    using var response = await client.SendAsync(request, cancellationToken);
                    _stopwatch.Stop();

                    // Only consider 2xx status codes as success for validation
                    if (response.IsSuccessStatusCode) 
                    {
                        status = new ProviderStatus
                        {
                            Status = ProviderStatus.StatusType.Online,
                            StatusMessage = "Connected",
                            LastCheckedUtc = DateTime.UtcNow,
                            ResponseTimeMs = _stopwatch.ElapsedMilliseconds,
                            EndpointUrl = actualEndpoint
                        };
                    }
                    else
                    {
                        var content = await response.Content.ReadAsStringAsync(cancellationToken);
                        string errorMessage = $"API Error: {response.StatusCode} - {content}";
                        string errorCategory = CategorizeErrorFromStatusCode(response.StatusCode);
                        
                        status = new ProviderStatus
                        {
                            Status = ProviderStatus.StatusType.Offline,
                            StatusMessage = errorMessage,
                            LastCheckedUtc = DateTime.UtcNow,
                            ErrorCategory = errorCategory,
                            ResponseTimeMs = _stopwatch.ElapsedMilliseconds,
                            EndpointUrl = actualEndpoint
                        };
                    }
                }
                else
                {
                    // Special case: OpenRouter does not validate API keys at /api/v1/models
                    if (provider.ProviderName.Equals("OpenRouter", StringComparison.OrdinalIgnoreCase))
                    {
                        _stopwatch.Stop();
                        status = new ProviderStatus
                        {
                            Status = ProviderStatus.StatusType.Unknown,
                            StatusMessage = "OpenRouter does not validate API keys at the /api/v1/models endpoint. Status cannot be determined.",
                            LastCheckedUtc = DateTime.UtcNow,
                            ErrorCategory = null,
                            ResponseTimeMs = _stopwatch.ElapsedMilliseconds,
                            EndpointUrl = actualEndpoint
                        };
                    }
                    else
                    {
                        // Standard Bearer token authorization for other providers
                        using var request = new HttpRequestMessage(HttpMethod.Get, actualEndpoint);
                        request.Headers.Add("Authorization", $"Bearer {provider.ApiKey}");
                        
                        // Most model endpoints are GET requests
                        using var response = await client.SendAsync(request, cancellationToken);
                        _stopwatch.Stop();

                        // Only consider 2xx status codes as success for validation
                        if (response.IsSuccessStatusCode)
                        {
                            // Special handling for OpenRouter: check response body for error
                            if (provider.ProviderName.Equals("OpenRouter", StringComparison.OrdinalIgnoreCase))
                            {
                                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                                try
                                {
                                    using var doc = JsonDocument.Parse(content);
                                    if (doc.RootElement.TryGetProperty("error", out var errorProp))
                                    {
                                        string errorMessage = $"API Error: {errorProp.GetRawText()}";
                                        status = new ProviderStatus
                                        {
                                            Status = ProviderStatus.StatusType.Offline,
                                            StatusMessage = errorMessage,
                                            LastCheckedUtc = DateTime.UtcNow,
                                            ErrorCategory = "API",
                                            ResponseTimeMs = _stopwatch.ElapsedMilliseconds,
                                            EndpointUrl = actualEndpoint
                                        };
                                        return status;
                                    }
                                }
                                catch (JsonException)
                                {
                                    // Ignore parse errors, treat as valid if no error property
                                }
                            }
                            
                            status = new ProviderStatus
                            {
                                Status = ProviderStatus.StatusType.Online,
                                StatusMessage = "Connected",
                                LastCheckedUtc = DateTime.UtcNow,
                                ResponseTimeMs = _stopwatch.ElapsedMilliseconds,
                                EndpointUrl = actualEndpoint
                            };
                        }
                        else
                        {
                            var content = await response.Content.ReadAsStringAsync(cancellationToken);
                            string errorMessage = $"API Error: {response.StatusCode} - {content}";
                            string errorCategory = CategorizeErrorFromStatusCode(response.StatusCode);
                            
                            status = new ProviderStatus
                            {
                                Status = ProviderStatus.StatusType.Offline,
                                StatusMessage = errorMessage,
                                LastCheckedUtc = DateTime.UtcNow,
                                ErrorCategory = errorCategory,
                                ResponseTimeMs = _stopwatch.ElapsedMilliseconds,
                                EndpointUrl = actualEndpoint
                            };
                        }
                    }
                }

                // If we have a repository, record this health check
                if (_providerHealthRepository != null)
                {
                    try
                    {
                        var record = new ProviderHealthRecord
                        {
                            ProviderName = provider.ProviderName,
                            Status = (ProviderHealthRecord.StatusType)status.Status, // Direct mapping between enums
                            StatusMessage = status.StatusMessage,
                            TimestampUtc = status.LastCheckedUtc,
                            ResponseTimeMs = status.ResponseTimeMs,
                            ErrorCategory = status.Status == ProviderStatus.StatusType.Online ? null : status.ErrorCategory,
                            ErrorDetails = status.Status == ProviderStatus.StatusType.Online ? null : status.StatusMessage,
                            EndpointUrl = status.EndpointUrl
                        };

                        await _providerHealthRepository.SaveStatusAsync(record);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error recording provider health check");
                    }
                }

                return status;
            }
            catch (TaskCanceledException)
            {
                _stopwatch.Stop();
                var status = new ProviderStatus
                {
                    Status = ProviderStatus.StatusType.Offline,
                    StatusMessage = "Connection timeout",
                    LastCheckedUtc = DateTime.UtcNow,
                    ErrorCategory = "Timeout",
                    ResponseTimeMs = _stopwatch.ElapsedMilliseconds,
                    EndpointUrl = GetProviderTestEndpoint(provider)
                };
                
                // Record the health check
                await RecordHealthCheckAsync(provider, status);
                
                return status;
            }
            catch (HttpRequestException ex)
            {
                _stopwatch.Stop();
                var status = new ProviderStatus
                {
                    Status = ProviderStatus.StatusType.Offline,
                    StatusMessage = $"Network Error: {ex.Message}",
                    LastCheckedUtc = DateTime.UtcNow,
                    ErrorCategory = "Network",
                    ResponseTimeMs = _stopwatch.ElapsedMilliseconds,
                    EndpointUrl = GetProviderTestEndpoint(provider)
                };
                
                // Record the health check
                await RecordHealthCheckAsync(provider, status);
                
                return status;
            }
            catch (Exception ex)
            {
                _stopwatch.Stop();
                string errorCategory = CategorizeError(ex.Message);
                var status = new ProviderStatus
                {
                    Status = ProviderStatus.StatusType.Offline,
                    StatusMessage = $"Error: {ex.Message}",
                    LastCheckedUtc = DateTime.UtcNow,
                    ErrorCategory = errorCategory,
                    ResponseTimeMs = _stopwatch.ElapsedMilliseconds,
                    EndpointUrl = GetProviderTestEndpoint(provider)
                };
                
                // Record the health check
                await RecordHealthCheckAsync(provider, status);
                
                return status;
            }
        }

        /// <summary>
        /// Gets the appropriate endpoint URL for a provider to test connectivity
        /// </summary>
        private string GetProviderTestEndpoint(ProviderCredential provider) 
        {
            // Use BaseUrl property from the Configuration entity
            _logger.LogDebug("GetProviderTestEndpoint called for ProviderName: '{ProviderName}', BaseUrl: '{BaseUrl}'", provider.ProviderName, provider.BaseUrl); 
            var baseUrl = provider.BaseUrl; // Use BaseUrl
            var providerNameLower = provider.ProviderName.ToLowerInvariant(); // Convert to lowercase for case-insensitive comparison
            _logger.LogDebug("Calculated providerNameLower: '{ProviderNameLower}'", providerNameLower);

            // If the provider doesn't have a custom API base, use the default
            if (string.IsNullOrEmpty(baseUrl))
            {
                switch (providerNameLower) // Use lowercase name
                {
                    case "openai": // Lowercase
                        return "https://api.openai.com/v1/models";
                    case "anthropic": // Lowercase
                        return "https://api.anthropic.com/v1/models";
                    case "cohere": // Lowercase
                        return "https://api.cohere.ai/v1/models";
                    case "gemini": // Lowercase
                    case "google": // Lowercase - Default generic Google to Gemini
                        return "https://generativelanguage.googleapis.com/v1beta/models";
                    case "fireworks": // Lowercase
                        return "https://api.fireworks.ai/inference/v1/models";
                    case "openrouter": // Lowercase
                        // Revert to using the models endpoint for validation
                        return "https://openrouter.ai/api/v1/models"; 
                    case "cerebras": // Lowercase
                        return "https://cerebras.cloud/api/v1/models";
                    case "aws bedrock":
                        return "https://bedrock-runtime.us-east-1.amazonaws.com/model";
                    case "sagemaker":
                        return "https://runtime.sagemaker.us-east-1.amazonaws.com/endpoints";
                    case "vertexai":
                        return "https://us-central1-aiplatform.googleapis.com/v1/projects/-/locations/us-central1/models";
                    case "huggingface":
                        return "https://api-inference.huggingface.co/models";
                    case "groq":
                        return "https://api.groq.com/openai/v1/models";
                    case "mistral":
                        return "https://api.mistral.ai/v1/models";
                    // Explicitly handle generic Azure/AWS - require BaseUrl
                    case "azure":
                    case "aws":
                        _logger.LogWarning("Provider '{ProviderName}' requires a BaseUrl to be configured for connection testing.", provider.ProviderName);
                        return string.Empty;
                    default:
                        // Return empty string for unknown providers
                        _logger.LogWarning("Unknown provider '{ProviderName}' encountered in GetProviderTestEndpoint without a BaseUrl.", provider.ProviderName);
                        return string.Empty;
                }
            }
            
            // Remove trailing slash if present
            baseUrl = baseUrl.TrimEnd('/');
            
            // Determine the appropriate endpoint based on the provider
            switch (providerNameLower) // Use lowercase name
            {
                case "openai": // Lowercase
                    return $"{baseUrl}/v1/models";
                case "anthropic": // Lowercase
                    return $"{baseUrl}/v1/models";
                case "cohere": // Lowercase
                    return $"{baseUrl}/v1/models";
                case "gemini": // Lowercase
                    return $"{baseUrl}/v1beta/models";
                case "fireworks": // Lowercase
                    return $"{baseUrl}/inference/v1/models";
                case "openrouter": // Lowercase
                    // Revert to using the models endpoint for validation
                    return $"{baseUrl}/api/v1/models";
                case "cerebras": // Lowercase
                    return $"{baseUrl}/api/v1/models";
                case "aws bedrock":
                    return $"{baseUrl}/model";
                case "sagemaker":
                    return $"{baseUrl}/endpoints";
                case "vertexai":
                    return $"{baseUrl}/v1/projects/-/locations/us-central1/models";
                case "huggingface":
                    return $"{baseUrl}/models";
                case "groq":
                    return $"{baseUrl}/openai/v1/models";
                case "mistral":
                    return $"{baseUrl}/v1/models";
                default:
                    return $"{baseUrl}/v1/models";
            }
        }
        
        /// <summary>
        /// Gets a custom endpoint URL for a provider if configured in the health monitoring settings
        /// </summary>
        private async Task<string?> GetCustomEndpointUrlAsync(string providerName)
        {
            if (_providerHealthRepository == null)
            {
                return null;
            }
            
            try
            {
                var config = await _providerHealthRepository.GetConfigurationAsync(providerName);
                return config?.CustomEndpointUrl;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting custom endpoint URL for provider {ProviderName}", providerName);
                return null;
            }
        }
        
        /// <summary>
        /// Records a health check in the repository
        /// </summary>
        private async Task RecordHealthCheckAsync(ProviderCredential provider, ProviderStatus status)
        {
            if (_providerHealthRepository == null)
            {
                return;
            }

            try
            {
                var record = new ProviderHealthRecord
                {
                    ProviderName = provider.ProviderName,
                    Status = (ProviderHealthRecord.StatusType)status.Status, // Direct mapping between enums
                    StatusMessage = status.StatusMessage,
                    TimestampUtc = status.LastCheckedUtc,
                    ResponseTimeMs = status.ResponseTimeMs,
                    ErrorCategory = status.Status == ProviderStatus.StatusType.Online ? null : status.ErrorCategory,
                    ErrorDetails = status.Status == ProviderStatus.StatusType.Online ? null : status.StatusMessage,
                    EndpointUrl = status.EndpointUrl
                };

                await _providerHealthRepository.SaveStatusAsync(record);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording provider health check");
            }
        }
        
        /// <summary>
        /// Categorizes an error message into a standard error category
        /// </summary>
        private string CategorizeError(string errorMessage)
        {
            if (string.IsNullOrEmpty(errorMessage))
            {
                return "Unknown";
            }
            
            if (errorMessage.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
                errorMessage.Contains("timed out", StringComparison.OrdinalIgnoreCase))
            {
                return "Timeout";
            }
            else if (errorMessage.Contains("network", StringComparison.OrdinalIgnoreCase) ||
                     errorMessage.Contains("dns", StringComparison.OrdinalIgnoreCase) ||
                     errorMessage.Contains("connect", StringComparison.OrdinalIgnoreCase))
            {
                return "Network";
            }
            else if (errorMessage.Contains("authentication", StringComparison.OrdinalIgnoreCase) ||
                     errorMessage.Contains("auth", StringComparison.OrdinalIgnoreCase) ||
                     errorMessage.Contains("unauthorized", StringComparison.OrdinalIgnoreCase) ||
                     errorMessage.Contains("authoriz", StringComparison.OrdinalIgnoreCase) ||
                     errorMessage.Contains("api key", StringComparison.OrdinalIgnoreCase) ||
                     errorMessage.Contains("401", StringComparison.OrdinalIgnoreCase))
            {
                return "Authentication";
            }
            else if (errorMessage.Contains("rate limit", StringComparison.OrdinalIgnoreCase) ||
                     errorMessage.Contains("too many", StringComparison.OrdinalIgnoreCase) ||
                     errorMessage.Contains("429", StringComparison.OrdinalIgnoreCase))
            {
                return "RateLimit";
            }
            else if (errorMessage.Contains("server", StringComparison.OrdinalIgnoreCase) ||
                     errorMessage.Contains("5", StringComparison.OrdinalIgnoreCase) ||
                     errorMessage.Contains("503", StringComparison.OrdinalIgnoreCase) ||
                     errorMessage.Contains("502", StringComparison.OrdinalIgnoreCase) ||
                     errorMessage.Contains("500", StringComparison.OrdinalIgnoreCase))
            {
                return "ServerError";
            }
            else if (errorMessage.Contains("configuration", StringComparison.OrdinalIgnoreCase) ||
                     errorMessage.Contains("config", StringComparison.OrdinalIgnoreCase) ||
                     errorMessage.Contains("setting", StringComparison.OrdinalIgnoreCase))
            {
                return "Configuration";
            }
            
            return "Unknown";
        }
        
        /// <summary>
        /// Categorizes an error from an HTTP status code
        /// </summary>
        private string CategorizeErrorFromStatusCode(System.Net.HttpStatusCode statusCode)
        {
            int code = (int)statusCode;
            
            if (code == 401 || code == 403)
            {
                return "Authentication";
            }
            else if (code == 429)
            {
                return "RateLimit";
            }
            else if (code >= 500)
            {
                return "ServerError";
            }
            else if (code >= 400)
            {
                return "ClientError";
            }
            
            return "Unknown";
        }
    }

    /// <summary>
    /// Represents the status of a provider
    /// </summary>
    public class ProviderStatus
    {
        /// <summary>
        /// Status type for a provider
        /// </summary>
        public enum StatusType
        {
            /// <summary>
            /// Provider is online
            /// </summary>
            Online = 0,

            /// <summary>
            /// Provider is offline
            /// </summary>
            Offline = 1,

            /// <summary>
            /// Provider status cannot be determined
            /// </summary>
            Unknown = 2
        }
        
        /// <summary>
        /// The status of the provider
        /// </summary>
        public StatusType Status { get; set; } = StatusType.Unknown;
        
        /// <summary>
        /// Whether the provider is online (maintained for compatibility)
        /// </summary>
        public bool IsOnline 
        { 
            get => Status == StatusType.Online;
            set => Status = value ? StatusType.Online : StatusType.Offline;
        }
        
        /// <summary>
        /// The status message for the provider
        /// </summary>
        public string StatusMessage { get; set; } = string.Empty;
        
        /// <summary>
        /// When the status was last checked
        /// </summary>
        public DateTime LastCheckedUtc { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// The response time of the health check in milliseconds
        /// </summary>
        public double ResponseTimeMs { get; set; }
        
        /// <summary>
        /// The category of error if the provider is offline
        /// </summary>
        public string? ErrorCategory { get; set; }
        
        /// <summary>
        /// The endpoint URL that was checked
        /// </summary>
        public string? EndpointUrl { get; set; }
    }
}
