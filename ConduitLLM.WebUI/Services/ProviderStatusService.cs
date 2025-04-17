using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
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
        private readonly IDbContextFactory<ConfigurationDbContext> _dbContextFactory;
        private readonly ILogger<ProviderStatusService> _logger;

        public ProviderStatusService(
            IHttpClientFactory httpClientFactory,
            IDbContextFactory<ConfigurationDbContext> dbContextFactory,
            ILogger<ProviderStatusService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _dbContextFactory = dbContextFactory;
            _logger = logger;
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
                using var dbContext = await _dbContextFactory.CreateDbContextAsync();
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
                            StatusMessage = $"Error: {ex.Message}"
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving providers from database");
                // Return empty dictionary rather than throwing
            }
            
            return result;
        }

        /// <summary>
        /// Check the status of a specific provider
        /// </summary>
        /// <param name="provider">The provider credentials to check</param>
        /// <returns>The status of the provider</returns>
        public async Task<ProviderStatus> CheckProviderStatusAsync(DbProviderCredentials provider)
        {
            if (string.IsNullOrEmpty(provider.ApiKey))
            {
                return new ProviderStatus
                {
                    IsOnline = false,
                    StatusMessage = "No API key configured",
                    LastCheckedUtc = DateTime.UtcNow
                };
            }

            try
            {
                // Get the appropriate endpoint for this provider
                var requestUri = GetProviderTestEndpoint(provider);
                if (string.IsNullOrEmpty(requestUri))
                {
                    return new ProviderStatus
                    {
                        IsOnline = false,
                        StatusMessage = "Unknown provider or no endpoint configured",
                        LastCheckedUtc = DateTime.UtcNow
                    };
                }

                // Create a minimal request to test the API
                using var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(5); // Set a short timeout

                // For Gemini, the API key is passed as a query parameter instead of an Authorization header
                if (provider.ProviderName.Equals("Gemini", StringComparison.OrdinalIgnoreCase))
                {
                    // Add the API key as a query parameter
                    var separator = requestUri.Contains("?") ? "&" : "?";
                    var requestUriWithKey = $"{requestUri}{separator}key={provider.ApiKey}";
                    
                    using var request = new HttpRequestMessage(HttpMethod.Get, requestUriWithKey);
                    using var response = await client.SendAsync(request);

                    // Only consider 2xx status codes as success for validation
                    if (response.IsSuccessStatusCode) 
                    {
                        return new ProviderStatus
                        {
                            IsOnline = true,
                            StatusMessage = "Connected",
                            LastCheckedUtc = DateTime.UtcNow
                        };
                    }
                    else
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        return new ProviderStatus
                        {
                            IsOnline = false,
                            StatusMessage = $"API Error: {response.StatusCode} - {content}",
                            LastCheckedUtc = DateTime.UtcNow
                        };
                    }
                }
                else
                {
                    // Special case: OpenRouter does not validate API keys at /api/v1/models
                    if (provider.ProviderName.Equals("OpenRouter", StringComparison.OrdinalIgnoreCase))
                    {
                        return new ProviderStatus
                        {
                            IsOnline = false,
                            StatusMessage = "WARNING: OpenRouter does not validate API keys at the /api/v1/models endpoint. Key validity cannot be confirmed.",
                            LastCheckedUtc = DateTime.UtcNow
                        };
                    }
                    // Standard Bearer token authorization for other providers
                    using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
                    request.Headers.Add("Authorization", $"Bearer {provider.ApiKey}");
                    
                    // Most model endpoints are GET requests
                    using var response = await client.SendAsync(request);

                    // Only consider 2xx status codes as success for validation
                    if (response.IsSuccessStatusCode)
                    {
                        // Special handling for OpenRouter: check response body for error
                        if (provider.ProviderName.Equals("OpenRouter", StringComparison.OrdinalIgnoreCase))
                        {
                            var content = await response.Content.ReadAsStringAsync();
                            try
                            {
                                using var doc = JsonDocument.Parse(content);
                                if (doc.RootElement.TryGetProperty("error", out var errorProp))
                                {
                                    return new ProviderStatus
                                    {
                                        IsOnline = false,
                                        StatusMessage = $"API Error: {errorProp.GetRawText()}",
                                        LastCheckedUtc = DateTime.UtcNow
                                    };
                                }
                            }
                            catch (JsonException)
                            {
                                // Ignore parse errors, treat as valid if no error property
                            }
                        }
                        return new ProviderStatus
                        {
                            IsOnline = true,
                            StatusMessage = "Connected",
                            LastCheckedUtc = DateTime.UtcNow
                        };
                    }
                    else
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        return new ProviderStatus
                        {
                            IsOnline = false,
                            StatusMessage = $"API Error: {response.StatusCode} - {content}",
                            LastCheckedUtc = DateTime.UtcNow
                        };
                    }
                }
            }
            catch (TaskCanceledException)
            {
                return new ProviderStatus
                {
                    IsOnline = false,
                    StatusMessage = "Connection timeout",
                    LastCheckedUtc = DateTime.UtcNow
                };
            }
            catch (HttpRequestException ex)
            {
                return new ProviderStatus
                {
                    IsOnline = false,
                    StatusMessage = $"Network Error: {ex.Message}",
                    LastCheckedUtc = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                return new ProviderStatus
                {
                    IsOnline = false,
                    StatusMessage = $"Error: {ex.Message}",
                    LastCheckedUtc = DateTime.UtcNow
                };
            }
        }

        /// <summary>
        /// Gets the appropriate endpoint URL for a provider to test connectivity
        /// </summary>
        private string GetProviderTestEndpoint(DbProviderCredentials provider)
        {
            _logger.LogDebug("GetProviderTestEndpoint called for ProviderName: '{ProviderName}', ApiBase: '{ApiBase}'", provider.ProviderName, provider.ApiBase);
            var baseUrl = provider.ApiBase;
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
                    default:
                        // Return empty string for unknown providers
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
    }

    /// <summary>
    /// Represents the status of a provider
    /// </summary>
    public class ProviderStatus
    {
        /// <summary>
        /// Whether the provider is online
        /// </summary>
        public bool IsOnline { get; set; }
        
        /// <summary>
        /// The status message for the provider
        /// </summary>
        public string StatusMessage { get; set; } = string.Empty;
        
        /// <summary>
        /// When the status was last checked
        /// </summary>
        public DateTime LastCheckedUtc { get; set; } = DateTime.UtcNow;
    }
}
