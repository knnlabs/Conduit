using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using ConduitLLM.Admin.Extensions;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Core.Extensions;

using Microsoft.Extensions.Logging;

using static ConduitLLM.Core.Extensions.LoggingSanitizer;

namespace ConduitLLM.Admin.Services
{
    /// <summary>
    /// Service implementation for managing provider credentials through the Admin API
    /// </summary>
    public class AdminProviderCredentialService : IAdminProviderCredentialService
    {
        private readonly IProviderCredentialRepository _providerCredentialRepository;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<AdminProviderCredentialService> _logger;

        /// <summary>
        /// Initializes a new instance of the AdminProviderCredentialService
        /// </summary>
        /// <param name="providerCredentialRepository">The provider credential repository</param>
        /// <param name="httpClientFactory">The HTTP client factory for connection testing</param>
        /// <param name="logger">The logger</param>
        public AdminProviderCredentialService(
            IProviderCredentialRepository providerCredentialRepository,
            IHttpClientFactory httpClientFactory,
            ILogger<AdminProviderCredentialService> logger)
        {
            _providerCredentialRepository = providerCredentialRepository ?? throw new ArgumentNullException(nameof(providerCredentialRepository));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<ProviderCredentialDto> CreateProviderCredentialAsync(CreateProviderCredentialDto providerCredential)
        {
            if (providerCredential == null)
            {
                throw new ArgumentNullException(nameof(providerCredential));
            }

            try
            {
                // Check if a credential with the same provider name already exists
                var existingCredential = await _providerCredentialRepository.GetByProviderNameAsync(providerCredential.ProviderName);
                if (existingCredential != null)
                {
                    _logger.LogWarning("Provider credential already exists for provider {ProviderName}", S(providerCredential.ProviderName));
                    throw new InvalidOperationException("A provider credential for this provider already exists");
                }

                // Convert DTO to entity
                var credentialEntity = providerCredential.ToEntity();

                // Save to database
                var id = await _providerCredentialRepository.CreateAsync(credentialEntity);

                // Get the created credential
                var createdCredential = await _providerCredentialRepository.GetByIdAsync(id);
                if (createdCredential == null)
                {
                    _logger.LogError("Failed to retrieve newly created provider credential {ProviderId}", S(id));
                    throw new InvalidOperationException("Failed to retrieve newly created provider credential");
                }

                _logger.LogInformation("Created provider credential for '{ProviderName}'", S(providerCredential.ProviderName));
                return createdCredential.ToDto();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating provider credential for '{ProviderName}'", S(providerCredential.ProviderName));
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<bool> DeleteProviderCredentialAsync(int id)
        {
            try
            {
                var result = await _providerCredentialRepository.DeleteAsync(id);

                if (result)
                {
                    _logger.LogInformation("Deleted provider credential with ID {Id}", S(id));
                }
                else
                {
                    _logger.LogWarning("Provider credential with ID {Id} not found for deletion", S(id));
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting provider credential with ID {Id}", S(id));
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<IEnumerable<ProviderCredentialDto>> GetAllProviderCredentialsAsync()
        {
            try
            {
                var credentials = await _providerCredentialRepository.GetAllAsync();
                return credentials.Select(c => c.ToDto()).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all provider credentials");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<IEnumerable<ProviderDataDto>> GetAllProviderNamesAsync()
        {
            try
            {
                var credentials = await _providerCredentialRepository.GetAllAsync();
                return credentials.Select(c => c.ToProviderDataDto()).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all provider names");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<ProviderCredentialDto?> GetProviderCredentialByIdAsync(int id)
        {
            try
            {
                var credential = await _providerCredentialRepository.GetByIdAsync(id);
                return credential?.ToDto();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting provider credential with ID {Id}", S(id));
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<ProviderCredentialDto?> GetProviderCredentialByNameAsync(string providerName)
        {
            if (string.IsNullOrWhiteSpace(providerName))
            {
                throw new ArgumentException("Provider name cannot be null or empty", nameof(providerName));
            }

            try
            {
                var credential = await _providerCredentialRepository.GetByProviderNameAsync(providerName);
                return credential?.ToDto();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting provider credential for '{ProviderName}'", S(providerName));
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<ProviderConnectionTestResultDto> TestProviderConnectionAsync(ProviderCredentialDto providerCredential)
        {
            if (providerCredential == null)
            {
                throw new ArgumentNullException(nameof(providerCredential));
            }

            try
            {
                var startTime = DateTime.UtcNow;
                var result = new ProviderConnectionTestResultDto
                {
                    Success = false,
                    Message = string.Empty,
                    ErrorDetails = null,
                    ProviderName = providerCredential.ProviderName,
                    Timestamp = DateTime.UtcNow
                };

                // If the credential has an ID, get the actual credential (with the real API key)
                // Otherwise, use the provided credential directly (for testing unsaved providers)
                ProviderCredential actualCredential;
                if (providerCredential.Id > 0)
                {
                    var dbCredential = await _providerCredentialRepository.GetByIdAsync(providerCredential.Id);
                    if (dbCredential == null)
                    {
                        _logger.LogWarning("Provider credential not found {ProviderId}", S(providerCredential.Id));
                        result.Message = "Provider credential not found";
                        result.ErrorDetails = "Provider not found in database";
                        return result;
                    }
                    actualCredential = dbCredential;
                }
                else
                {
                    // For testing unsaved providers, create a temporary credential object
                    actualCredential = new ProviderCredential
                    {
                        ProviderName = providerCredential.ProviderName,
                        ApiKey = providerCredential.ApiKey,
                        BaseUrl = providerCredential.ApiBase,
                        IsEnabled = true
                    };
                }

                // Create an HTTP client
                using var client = _httpClientFactory.CreateClient();

                // Configure timeout
                client.Timeout = TimeSpan.FromSeconds(10);

                // Add authorization header if API key is available
                if (!string.IsNullOrEmpty(actualCredential.ApiKey))
                {
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {actualCredential.ApiKey}");
                }

                // Construct the API URL based on provider type
                var apiUrl = GetHealthCheckUrl(actualCredential);

                // Make the request
                var responseMessage = await client.GetAsync(apiUrl);
                var responseTime = DateTime.UtcNow - startTime;

                // Check the response
                result.Success = responseMessage.IsSuccessStatusCode;
                if (!result.Success)
                {
                    var errorContent = await responseMessage.Content.ReadAsStringAsync();
                    result.Message = $"Status: {(int)responseMessage.StatusCode} {responseMessage.ReasonPhrase}";
                    result.ErrorDetails = !string.IsNullOrEmpty(errorContent) && errorContent.Length <= 1000
                        ? errorContent
                        : "Response content too large to display";
                }
                else
                {
                    // Some providers have public endpoints that return 200 without auth
                    // We need additional validation for these providers
                    var providerName = providerCredential.ProviderName.ToLowerInvariant();

                    switch (providerName)
                    {
                        case "openrouter":
                            _logger.LogInformation("Performing additional OpenRouter authentication check");

                            var openRouterAuthSuccessful = await VerifyOpenRouterAuthenticationAsync(client, actualCredential);

                            if (!openRouterAuthSuccessful)
                            {
                                result.Success = false;
                                result.Message = "Authentication failed";
                                result.ErrorDetails = "Invalid API key - OpenRouter requires a valid API key for making requests";
                                return result;
                            }

                            _logger.LogInformation("OpenRouter authentication check passed");
                            break;

                        case "google":
                        case "gemini":
                            _logger.LogInformation("Performing additional Google/Gemini authentication check");

                            var geminiAuthSuccessful = await VerifyGeminiAuthenticationAsync(client, actualCredential);

                            if (!geminiAuthSuccessful)
                            {
                                result.Success = false;
                                result.Message = "Authentication failed";
                                result.ErrorDetails = "Invalid API key - Google Gemini requires a valid API key for making requests";
                                return result;
                            }

                            _logger.LogInformation("Google/Gemini authentication check passed");
                            break;

                        case "ollama":
                            // Ollama is a local service and doesn't require authentication
                            _logger.LogInformation("Skipping authentication check for Ollama (local service)");
                            break;
                    }

                    var responseTimeMs = responseTime.TotalMilliseconds;
                    result.Message = $"Connected successfully to {providerCredential.ProviderName} API in {responseTimeMs:F2}ms";
                }

                return result;
            }
            catch (TaskCanceledException)
            {
                return new ProviderConnectionTestResultDto
                {
                    Success = false,
                    Message = "The connection timed out",
                    ErrorDetails = "Request exceeded the 10 second timeout",
                    ProviderName = providerCredential.ProviderName,
                    Timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing connection to provider '{ProviderName}'", S(providerCredential.ProviderName));

                return new ProviderConnectionTestResultDto
                {
                    Success = false,
                    Message = $"Connection test failed: {ex.Message}",
                    ErrorDetails = ex.ToString(),
                    ProviderName = providerCredential.ProviderName,
                    Timestamp = DateTime.UtcNow
                };
            }
        }

        /// <inheritdoc />
        public async Task<bool> UpdateProviderCredentialAsync(UpdateProviderCredentialDto providerCredential)
        {
            if (providerCredential == null)
            {
                throw new ArgumentNullException(nameof(providerCredential));
            }

            try
            {
                // Get existing credential
                var existingCredential = await _providerCredentialRepository.GetByIdAsync(providerCredential.Id);
                if (existingCredential == null)
                {
                    _logger.LogWarning("Provider credential with ID {Id} not found", S(providerCredential.Id));
                    return false;
                }

                // Update entity
                existingCredential.UpdateFrom(providerCredential);

                // Save changes
                var result = await _providerCredentialRepository.UpdateAsync(existingCredential);

                if (result)
                {
                    _logger.LogInformation("Updated provider credential with ID {Id}", S(providerCredential.Id));
                }
                else
                {
                    _logger.LogWarning("Failed to update provider credential with ID {Id}", S(providerCredential.Id));
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating provider credential with ID {Id}", S(providerCredential.Id));
                throw;
            }
        }

        /// <summary>
        /// Gets the health check URL for a provider
        /// </summary>
        /// <param name="credential">The provider credential</param>
        /// <returns>The health check URL</returns>
        private string GetHealthCheckUrl(ProviderCredential credential)
        {
            // Default URL
            var baseUrl = credential.BaseUrl;

            // If base URL is not specified, use a default based on provider name
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                baseUrl = GetDefaultBaseUrl(credential.ProviderName);
            }

            // Trim trailing slash if present
            baseUrl = baseUrl?.TrimEnd('/');

            // Construct health check URL based on provider
            return credential.ProviderName.ToLowerInvariant() switch
            {
                // OpenAI-compatible providers
                "openai" => $"{baseUrl}/v1/models",
                "anthropic" => $"{baseUrl}/v1/models",
                "cohere" => $"{baseUrl}/v1/models",
                "mistral" or "mistralai" => $"{baseUrl}/v1/models",
                "groq" => $"{baseUrl}/v1/models",
                "openrouter" => $"{baseUrl}/api/v1/models",
                "fireworks" or "fireworksai" => $"{baseUrl}/v1/models",
                "openai-compatible" or "openaicompatible" => $"{baseUrl}/v1/models",

                // Cloud providers with custom endpoints
                "azure" => $"{baseUrl}/openai/models?api-version=2023-05-15",
                "bedrock" => $"{baseUrl}/", // Bedrock doesn't have a simple health endpoint
                "vertexai" => $"{baseUrl}/", // VertexAI requires complex auth
                "sagemaker" => $"{baseUrl}/", // SageMaker endpoints are custom

                // Other LLM providers
                "google" or "gemini" => $"{baseUrl}/v1beta/models",
                "ollama" => $"{baseUrl}/api/tags",
                "replicate" => $"{baseUrl}/", // Replicate doesn't have a models endpoint
                "huggingface" => $"{baseUrl}/", // HuggingFace doesn't have a generic models endpoint

                // Audio/specialized providers
                "ultravox" => $"{baseUrl}/", // Ultravox is realtime audio
                "elevenlabs" or "eleven-labs" => $"{baseUrl}/v1/user", // ElevenLabs user endpoint for health check

                _ => $"{baseUrl}/models" // Generic endpoint for other providers
            };
        }

        /// <summary>
        /// Verifies OpenRouter authentication by making a minimal authenticated request
        /// </summary>
        /// <param name="client">The HTTP client with auth headers already set</param>
        /// <param name="credential">The provider credential</param>
        /// <returns>True if authentication is valid, false otherwise</returns>
        private async Task<bool> VerifyOpenRouterAuthenticationAsync(HttpClient client, ProviderCredential credential)
        {
            try
            {
                // OpenRouter requires authentication for certain operations
                // We'll make a lightweight request that requires auth but doesn't incur usage costs

                // First, let's try the auth endpoint if available
                var baseUrl = !string.IsNullOrWhiteSpace(credential.BaseUrl)
                    ? credential.BaseUrl
                    : "https://openrouter.ai";
                var authCheckUrl = $"{baseUrl.TrimEnd('/')}/api/v1/auth/key";

                try
                {
                    _logger.LogInformation("Checking OpenRouter auth endpoint");

                    // Try the auth key endpoint first (if it exists)
                    var authResponse = await client.GetAsync(authCheckUrl);

                    _logger.LogInformation("OpenRouter auth endpoint returned status {StatusCode}", S((int)authResponse.StatusCode));

                    if (authResponse.StatusCode == HttpStatusCode.OK)
                    {
                        // If the auth endpoint exists and returns OK, we're authenticated
                        _logger.LogInformation("OpenRouter authentication successful via auth endpoint");
                        return true;
                    }
                    else if (authResponse.StatusCode == HttpStatusCode.Unauthorized ||
                             authResponse.StatusCode == HttpStatusCode.Forbidden)
                    {
                        // Clear authentication failure
                        _logger.LogWarning("OpenRouter authentication failed with status {StatusCode}", S((int)authResponse.StatusCode));
                        return false;
                    }
                }
                catch (Exception)
                {
                    // Auth endpoint might not exist, fall back to completion test
                    _logger.LogInformation("OpenRouter auth endpoint check failed, falling back to completion test");
                }

                // Fallback: Make a minimal generation request that will fail immediately if auth is invalid
                // Use the generation endpoint with parameters that will fail validation but still check auth
                var testRequest = new
                {
                    model = "openrouter/auto",
                    messages = new[]
                    {
                        new { role = "user", content = "test" }
                    },
                    max_tokens = 1,
                    stream = false,
                    // Add a parameter that will cause the request to fail after auth check
                    temperature = -1.0 // Invalid temperature to fail after auth
                };

                var json = JsonSerializer.Serialize(testRequest);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var chatUrl = $"{baseUrl.TrimEnd('/')}/api/v1/chat/completions";
                var response = await client.PostAsync(chatUrl, content);

                // Check the response for authentication errors
                var responseContent = await response.Content.ReadAsStringAsync();

                // OpenRouter returns specific error messages for auth failures
                if (response.StatusCode == HttpStatusCode.Unauthorized ||
                    response.StatusCode == HttpStatusCode.Forbidden ||
                    responseContent.Contains("No auth credentials found", StringComparison.OrdinalIgnoreCase) ||
                    responseContent.Contains("Invalid API key", StringComparison.OrdinalIgnoreCase) ||
                    responseContent.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                // If we get here without auth errors, the key is valid
                // (even if the request failed for other reasons like invalid parameters)
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error verifying OpenRouter authentication");
                // If we can't verify, assume it's invalid to be safe
                return false;
            }
        }

        /// <summary>
        /// Verifies Google Gemini authentication by making a test request
        /// </summary>
        /// <param name="client">The HTTP client with authorization header set</param>
        /// <param name="credential">The provider credential</param>
        /// <returns>True if authentication is valid, false otherwise</returns>
        private async Task<bool> VerifyGeminiAuthenticationAsync(HttpClient client, ProviderCredential credential)
        {
            try
            {
                // Google Gemini requires API key as a query parameter, not in headers
                // We need to make a request that requires authentication
                var baseUrl = !string.IsNullOrWhiteSpace(credential.BaseUrl)
                    ? credential.BaseUrl
                    : "https://generativelanguage.googleapis.com";

                // Try to get a specific model which requires authentication
                var testUrl = $"{baseUrl.TrimEnd('/')}/v1beta/models/gemini-pro?key={credential.ApiKey}";

                // Remove the Bearer token from headers since Gemini uses query param
                client.DefaultRequestHeaders.Authorization = null;

                var response = await client.GetAsync(testUrl);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    _logger.LogInformation("Google Gemini authentication successful");
                    return true;
                }
                else if (response.StatusCode == HttpStatusCode.Unauthorized ||
                         response.StatusCode == HttpStatusCode.Forbidden ||
                         response.StatusCode == HttpStatusCode.BadRequest) // Gemini returns 400 for invalid keys
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Google Gemini authentication failed: {Status} - {Content}",
                        S(response.StatusCode), S(responseContent));
                    return false;
                }

                // If we get another status, log it but assume auth is ok
                _logger.LogInformation("Google Gemini returned unexpected status: {Status}", S(response.StatusCode));
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error verifying Google Gemini authentication");
                return false;
            }
        }

        /// <summary>
        /// Gets the default base URL for a provider
        /// </summary>
        /// <param name="providerName">The provider name</param>
        /// <returns>The default base URL</returns>
        private string GetDefaultBaseUrl(string providerName)
        {
            return providerName.ToLowerInvariant() switch
            {
                "openai" => "https://api.openai.com",
                "anthropic" => "https://api.anthropic.com",
                "cohere" => "https://api.cohere.ai",
                "mistral" or "mistralai" => "https://api.mistral.ai",
                "groq" => "https://api.groq.com",
                "openrouter" => "https://openrouter.ai",
                "ollama" => "http://localhost:11434",
                "google" or "gemini" => "https://generativelanguage.googleapis.com",
                "vertexai" => "https://us-central1-aiplatform.googleapis.com",
                "replicate" => "https://api.replicate.com",
                "fireworks" or "fireworksai" => "https://api.fireworks.ai",
                "huggingface" => "https://api-inference.huggingface.co",
                "bedrock" => "https://bedrock-runtime.us-east-1.amazonaws.com",
                "sagemaker" => "https://runtime.sagemaker.us-east-1.amazonaws.com",
                "azure" => "https://your-resource-name.openai.azure.com",
                "openai-compatible" or "openaicompatible" => "http://localhost:8000",
                "ultravox" => "https://api.ultravox.ai",
                "elevenlabs" or "eleven-labs" => "https://api.elevenlabs.io",
                _ => "https://api.example.com" // Generic fallback
            };
        }
    }
}
