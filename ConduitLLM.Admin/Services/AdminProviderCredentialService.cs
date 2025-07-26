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
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Core.Extensions;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Services;

using MassTransit;
using Microsoft.Extensions.Logging;

using static ConduitLLM.Core.Extensions.LoggingSanitizer;

namespace ConduitLLM.Admin.Services
{
    /// <summary>
    /// Service implementation for managing provider credentials through the Admin API
    /// </summary>
    public class AdminProviderCredentialService : EventPublishingServiceBase, IAdminProviderCredentialService
    {
        private readonly IProviderCredentialRepository _providerCredentialRepository;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<AdminProviderCredentialService> _logger;
        private readonly ConduitLLM.Configuration.IProviderCredentialService _configProviderCredentialService;

        /// <summary>
        /// Initializes a new instance of the AdminProviderCredentialService
        /// </summary>
        /// <param name="providerCredentialRepository">The provider credential repository</param>
        /// <param name="httpClientFactory">The HTTP client factory for connection testing</param>
        /// <param name="configProviderCredentialService">The configuration provider credential service</param>
        /// <param name="publishEndpoint">Optional event publishing endpoint (null if MassTransit not configured)</param>
        /// <param name="logger">The logger</param>
        public AdminProviderCredentialService(
            IProviderCredentialRepository providerCredentialRepository,
            IHttpClientFactory httpClientFactory,
            ConduitLLM.Configuration.IProviderCredentialService configProviderCredentialService,
            IPublishEndpoint? publishEndpoint,
            ILogger<AdminProviderCredentialService> logger)
            : base(publishEndpoint, logger)
        {
            _providerCredentialRepository = providerCredentialRepository ?? throw new ArgumentNullException(nameof(providerCredentialRepository));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _configProviderCredentialService = configProviderCredentialService ?? throw new ArgumentNullException(nameof(configProviderCredentialService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // Log event publishing configuration status
            LogEventPublishingConfiguration(nameof(AdminProviderCredentialService));
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
                var existingCredential = await _providerCredentialRepository.GetByProviderTypeAsync(providerCredential.ProviderType);
                if (existingCredential != null)
                {
                    _logger.LogWarning("Provider credential already exists for provider {ProviderType}", providerCredential.ProviderType.ToString().Replace(Environment.NewLine, ""));
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
                    _logger.LogError("Failed to retrieve newly created provider credential {ProviderId}", id);
                    throw new InvalidOperationException("Failed to retrieve newly created provider credential");
                }

                _logger.LogInformation("Created provider credential for '{ProviderType}'", providerCredential.ProviderType.ToString().Replace(Environment.NewLine, ""));


                // Publish ProviderCredentialUpdated event (creation is treated as an update)
                await PublishEventAsync(
                    new ProviderCredentialUpdated
                    {
                        ProviderId = createdCredential.Id,
                        ProviderType = createdCredential.ProviderType,
                        IsEnabled = createdCredential.IsEnabled,
                        ChangedProperties = new[] { "ProviderType", "BaseUrl", "IsEnabled" }, // All properties for creation
                        CorrelationId = Guid.NewGuid().ToString()
                    },
                    $"create provider credential {createdCredential.Id}",
                    new { ProviderName = createdCredential.ProviderType.ToString() });

                return createdCredential.ToDto();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating provider credential for '{ProviderType}'", providerCredential.ProviderType.ToString().Replace(Environment.NewLine, ""));
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<bool> DeleteProviderCredentialAsync(int id)
        {
            try
            {
                // Get provider info before deletion for event publishing
                var providerToDelete = await _providerCredentialRepository.GetByIdAsync(id);
                
                var result = await _providerCredentialRepository.DeleteAsync(id);

                if (result)
                {
                    _logger.LogInformation("Deleted provider credential with ID {Id}", id);

                    // Publish ProviderCredentialDeleted event
                    if (providerToDelete != null)
                    {
                        await PublishEventAsync(
                            new ProviderCredentialDeleted
                            {
                                ProviderId = providerToDelete.Id,
                                ProviderType = providerToDelete.ProviderType,
                                CorrelationId = Guid.NewGuid().ToString()
                            },
                            $"delete provider credential {providerToDelete.Id}",
                            new { ProviderName = providerToDelete.ProviderType.ToString() });
                    }
                    else
                    {
                        _logger.LogDebug("Provider credential not found before deletion - skipping ProviderCredentialDeleted event for ID {Id}", id);
                    }
                }
                else
                {
                    _logger.LogWarning("Provider credential with ID {Id} not found for deletion", id);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting provider credential with ID {Id}", id);
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
                _logger.LogError(ex, "Error getting provider credential with ID {Id}", id);
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
                // Parse provider name to enum
                if (!Enum.TryParse<ProviderType>(providerName, true, out var providerType))
                {
                    return null;
                }
                
                var credential = await _providerCredentialRepository.GetByProviderTypeAsync(providerType);
                return credential?.ToDto();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting provider credential for '{ProviderName}'", providerName.Replace(Environment.NewLine, ""));
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<ProviderConnectionTestResultDto> TestProviderConnectionAsync(ProviderCredentialDto providerCredential)
        {
            // lgtm [cs/user-controlled-bypass]
            if (providerCredential == null)
            {
                throw new ArgumentNullException(nameof(providerCredential));
            }

            try
            {
                _logger.LogInformation("Testing provider connection for {ProviderType}", providerCredential.ProviderType);
                
                var startTime = DateTime.UtcNow;
                var result = new ProviderConnectionTestResultDto
                {
                    Success = false,
                    Message = string.Empty,
                    ErrorDetails = null,
                    ProviderType = providerCredential.ProviderType,
                    Timestamp = DateTime.UtcNow
                };

                // For testing, we need to get the API key from ProviderKeyCredentials
                string? apiKey = null;
                string? baseUrl = providerCredential.BaseUrl;
                string providerName = providerCredential.ProviderType.ToString();
                
                if (providerCredential.Id > 0)
                {
                    var dbCredential = await _providerCredentialRepository.GetByIdAsync(providerCredential.Id);
                    if (dbCredential == null)
                    {
                        _logger.LogWarning("Provider credential not found {ProviderId}", providerCredential.Id);
                        result.Message = "Provider credential not found";
                        result.ErrorDetails = "Provider not found in database";
                        return result;
                    }
                    
                    // Get the primary key or first enabled key from ProviderKeyCredentials
                    var primaryKey = dbCredential.ProviderKeyCredentials?
                        .FirstOrDefault(k => k.IsPrimary && k.IsEnabled) ??
                        dbCredential.ProviderKeyCredentials?.FirstOrDefault(k => k.IsEnabled);
                        
                    if (primaryKey == null)
                    {
                        _logger.LogWarning("No enabled API keys found for provider {ProviderId}", providerCredential.Id);
                        result.Message = "No API keys configured";
                        result.ErrorDetails = "Provider has no enabled API keys";
                        return result;
                    }
                    
                    apiKey = primaryKey.ApiKey;
                    baseUrl = !string.IsNullOrEmpty(providerCredential.BaseUrl) ? providerCredential.BaseUrl : 
                              !string.IsNullOrEmpty(primaryKey.BaseUrl) ? primaryKey.BaseUrl : dbCredential.BaseUrl;
                    providerName = providerCredential.ProviderType.ToString();
                }
                else
                {
                    // For testing unsaved providers, we can't test without an API key
                    _logger.LogWarning("Cannot test unsaved provider without API key");
                    result.Message = "Cannot test provider";
                    result.ErrorDetails = "Provider must be saved with an API key before testing";
                    return result;
                }

                // Create an HTTP client
                using var client = _httpClientFactory.CreateClient();

                // Configure timeout
                client.Timeout = TimeSpan.FromSeconds(10);

                // Add authorization header if API key is available
                if (!string.IsNullOrEmpty(apiKey))
                {
                    // Use provider-specific authentication headers
                    if (providerName?.ToLowerInvariant() == "anthropic")
                    {
                        client.DefaultRequestHeaders.Add("x-api-key", apiKey);
                        client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
                    }
                    else
                    {
                        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
                    }
                }

                // Special handling for providers that don't support GET requests
                var providerNameLower = providerName?.ToLowerInvariant();
                TimeSpan responseTime;
                
                if (providerNameLower == "minimax" || providerNameLower == "anthropic")
                {
                    // MiniMax and Anthropic don't have a GET health check endpoint, skip to special handling
                    result.Success = true;
                    responseTime = TimeSpan.Zero; // Will be updated after actual test
                }
                else
                {
                    // Construct the API URL based on provider type
                    // Create a temporary credential object for GetHealthCheckUrl
                    var tempCredential = new ProviderCredential
                    {
                        ProviderType = providerCredential.ProviderType,
                        BaseUrl = baseUrl
                    };
                    var apiUrl = GetHealthCheckUrl(tempCredential);
                    
                    _logger.LogInformation("Testing provider {ProviderType} at URL: {ApiUrl}", providerCredential.ProviderType, apiUrl);

                    // Make the request
                    var responseMessage = await client.GetAsync(apiUrl);
                    responseTime = DateTime.UtcNow - startTime;

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
                }
                
                if (result.Success)
                {
                    // Some providers have public endpoints that return 200 without auth
                    // We need additional validation for these providers
                    // providerNameLower is already defined above

                    switch (providerNameLower)
                    {
                        case "openai":
                            _logger.LogInformation("Performing additional OpenAI authentication check");

                            // OpenAI's /v1/models endpoint returns 200 OK even without auth
                            // We need to check if we actually get models back with proper auth
                            var openAIAuthSuccessful = await VerifyOpenAIAuthenticationAsync(client, apiKey, baseUrl);

                            if (!openAIAuthSuccessful)
                            {
                                result.Success = false;
                                result.Message = "Authentication failed";
                                result.ErrorDetails = "Invalid API key - OpenAI requires a valid API key for accessing models";
                                return result;
                            }

                            _logger.LogInformation("OpenAI authentication check passed");
                            break;

                        case "openrouter":
                            _logger.LogInformation("Performing additional OpenRouter authentication check");

                            var openRouterAuthSuccessful = await VerifyOpenRouterAuthenticationAsync(client, apiKey, baseUrl);

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

                            var geminiAuthSuccessful = await VerifyGeminiAuthenticationAsync(client, apiKey, baseUrl);

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

                        case "anthropic":
                            _logger.LogInformation("Performing Anthropic authentication check via messages endpoint");
                            
                            var anthropicAuthSuccessful = await VerifyAnthropicAuthenticationAsync(client, apiKey, baseUrl);
                            responseTime = DateTime.UtcNow - startTime; // Update response time after actual check
                            
                            if (!anthropicAuthSuccessful)
                            {
                                result.Success = false;
                                result.Message = "Authentication failed";
                                result.ErrorDetails = "Invalid API key - Anthropic requires a valid x-api-key header for making requests";
                                return result;
                            }
                            
                            _logger.LogInformation("Anthropic authentication check passed");
                            break;
                            
                        case "minimax":
                            _logger.LogInformation("Performing MiniMax authentication check via chat completion");
                            
                            var miniMaxAuthSuccessful = await VerifyMiniMaxAuthenticationAsync(client, apiKey, baseUrl);
                            responseTime = DateTime.UtcNow - startTime; // Update response time after actual check
                            
                            if (!miniMaxAuthSuccessful)
                            {
                                result.Success = false;
                                result.Message = "Authentication failed";
                                result.ErrorDetails = "Invalid API key - MiniMax requires a valid API key for making requests";
                                return result;
                            }
                            
                            _logger.LogInformation("MiniMax authentication check passed");
                            break;
                    }

                    var responseTimeMs = responseTime.TotalMilliseconds;
                    result.Message = $"Connected successfully to {providerCredential.ProviderType} API in {responseTimeMs:F2}ms";
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
                    ProviderType = providerCredential.ProviderType,
                    Timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing connection to provider '{ProviderType}'", providerCredential.ProviderType.ToString().Replace(Environment.NewLine, ""));

                return new ProviderConnectionTestResultDto
                {
                    Success = false,
                    Message = $"Connection test failed: {ex.Message}",
                    ErrorDetails = ex.ToString(),
                    ProviderType = providerCredential.ProviderType,
                    Timestamp = DateTime.UtcNow
                };
            }
        }

        /// <summary>
        /// Tests a provider connection with a specific API key
        /// </summary>
        private async Task<ProviderConnectionTestResultDto> TestProviderConnectionWithKeyAsync(string providerName, string apiKey, string? baseUrl)
        {
            // Parse provider name to ProviderType first
            if (!Enum.TryParse<ProviderType>(providerName, true, out var providerType))
            {
                return new ProviderConnectionTestResultDto
                {
                    Success = false,
                    Message = "Invalid provider name",
                    ErrorDetails = $"Unknown provider: {providerName}",
                    Timestamp = DateTime.UtcNow
                };
            }
            
            try
            {
                _logger.LogInformation("Testing provider connection for {ProviderName} with specific key", providerName);
                
                var startTime = DateTime.UtcNow;
                
                var result = new ProviderConnectionTestResultDto
                {
                    Success = false,
                    Message = string.Empty,
                    ErrorDetails = null,
                    ProviderType = providerType,
                    Timestamp = DateTime.UtcNow
                };

                // Create an HTTP client
                using var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(10);

                // Add authorization header if API key is available
                if (!string.IsNullOrEmpty(apiKey))
                {
                    // Use provider-specific authentication headers
                    if (providerName?.ToLowerInvariant() == "anthropic")
                    {
                        client.DefaultRequestHeaders.Add("x-api-key", apiKey);
                        client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
                    }
                    else
                    {
                        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
                    }
                }

                // Special handling for providers that don't support GET requests
                var providerNameLower = providerName?.ToLowerInvariant();
                TimeSpan responseTime;
                
                if (providerNameLower == "minimax" || providerNameLower == "anthropic")
                {
                    // MiniMax and Anthropic don't have a GET health check endpoint
                    result.Success = true;
                    responseTime = TimeSpan.Zero;
                }
                else
                {
                    // Construct the API URL based on provider type
                    // Provider type was already parsed at the beginning of the method
                    
                    var tempCredential = new ProviderCredential
                    {
                        ProviderType = providerType,
                        BaseUrl = baseUrl
                    };
                    var apiUrl = GetHealthCheckUrl(tempCredential);
                    
                    _logger.LogInformation("Testing provider {ProviderName} at URL: {ApiUrl}", providerName, apiUrl);

                    // Make the request
                    var responseMessage = await client.GetAsync(apiUrl);
                    responseTime = DateTime.UtcNow - startTime;

                    // Check the response
                    result.Success = responseMessage.IsSuccessStatusCode;
                    if (!result.Success)
                    {
                        result.Message = $"API returned status code: {responseMessage.StatusCode}";
                        result.ErrorDetails = $"Response: {await responseMessage.Content.ReadAsStringAsync()}";
                        result.ResponseTimeMs = responseTime.TotalMilliseconds;
                        return result;
                    }
                }

                // Additional validation for specific providers
                if (result.Success)
                {
                    switch (providerNameLower)
                    {
                        case "openai":
                            _logger.LogInformation("Performing additional OpenAI authentication check");
                            var openAIAuthSuccessful = await VerifyOpenAIAuthenticationAsync(client, apiKey, baseUrl);
                            if (!openAIAuthSuccessful)
                            {
                                result.Success = false;
                                result.Message = "Authentication failed";
                                result.ErrorDetails = "Invalid API key - OpenAI requires a valid API key for accessing models";
                                return result;
                            }
                            break;

                        case "openrouter":
                            _logger.LogInformation("Performing additional OpenRouter authentication check");
                            var openRouterAuthSuccessful = await VerifyOpenRouterAuthenticationAsync(client, apiKey, baseUrl);
                            if (!openRouterAuthSuccessful)
                            {
                                result.Success = false;
                                result.Message = "Authentication failed";
                                result.ErrorDetails = "Invalid API key - OpenRouter requires a valid API key for making requests";
                                return result;
                            }
                            break;

                        case "google":
                        case "gemini":
                            _logger.LogInformation("Performing additional Google/Gemini authentication check");
                            var geminiAuthSuccessful = await VerifyGeminiAuthenticationAsync(client, apiKey, baseUrl);
                            if (!geminiAuthSuccessful)
                            {
                                result.Success = false;
                                result.Message = "Authentication failed";
                                result.ErrorDetails = "Invalid API key - Google Gemini requires a valid API key for making requests";
                                return result;
                            }
                            break;

                        case "anthropic":
                            _logger.LogInformation("Performing Anthropic authentication check via messages endpoint");
                            var anthropicAuthSuccessful = await VerifyAnthropicAuthenticationAsync(client, apiKey, baseUrl);
                            responseTime = DateTime.UtcNow - startTime;
                            if (!anthropicAuthSuccessful)
                            {
                                result.Success = false;
                                result.Message = "Authentication failed";
                                result.ErrorDetails = "Invalid API key - Anthropic requires a valid x-api-key header for making requests";
                                return result;
                            }
                            break;

                        case "minimax":
                            _logger.LogInformation("Performing MiniMax authentication check via chat completion");
                            var miniMaxAuthSuccessful = await VerifyMiniMaxAuthenticationAsync(client, apiKey, baseUrl);
                            responseTime = DateTime.UtcNow - startTime;
                            if (!miniMaxAuthSuccessful)
                            {
                                result.Success = false;
                                result.Message = "Authentication failed";
                                result.ErrorDetails = "Invalid API key - MiniMax requires a valid API key for making requests";
                                return result;
                            }
                            break;
                    }

                    var responseTimeMs = responseTime.TotalMilliseconds;
                    result.Message = $"Connection successful (Response time: {responseTimeMs:F0}ms)";
                    result.ResponseTimeMs = responseTimeMs;
                }

                return result;
            }
            catch (HttpRequestException httpEx)
            {
                _logger.LogError(httpEx, "HTTP request error testing provider connection for {ProviderName}", providerName);
                return new ProviderConnectionTestResultDto
                {
                    Success = false,
                    Message = $"Network error: {httpEx.Message}",
                    ErrorDetails = httpEx.ToString(),
                    ProviderType = providerType,
                    Timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing provider connection for {ProviderName}", providerName);
                return new ProviderConnectionTestResultDto
                {
                    Success = false,
                    Message = $"Connection test failed: {ex.Message}",
                    ErrorDetails = ex.ToString(),
                    ProviderType = providerType,
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
                    _logger.LogWarning("Provider credential with ID {Id} not found", providerCredential.Id);
                    return false;
                }

                // Track changed properties for event publishing
                var changedProperties = new List<string>();
                
                // Check what properties are actually changing
                // Note: ProviderName cannot be changed in updates, so we don't check it
                    
                if (providerCredential.BaseUrl != null && existingCredential.BaseUrl != providerCredential.BaseUrl)
                    changedProperties.Add(nameof(existingCredential.BaseUrl));
                    
                if (existingCredential.IsEnabled != providerCredential.IsEnabled)
                    changedProperties.Add(nameof(existingCredential.IsEnabled));
                    
                // Note: Organization is not mapped to the entity yet
                // It is only in the DTO, so we skip it for now

                // Update entity
                existingCredential.UpdateFrom(providerCredential);

                // Save changes
                var result = await _providerCredentialRepository.UpdateAsync(existingCredential);

                if (result)
                {
                    _logger.LogInformation("Updated provider credential with ID {Id}", providerCredential.Id);

                    // Publish ProviderCredentialUpdated event if there were actual changes
                    if (changedProperties.Count > 0)
                    {
                        await PublishEventAsync(
                            new ProviderCredentialUpdated
                            {
                                ProviderId = existingCredential.Id,
                                ProviderType = existingCredential.ProviderType,
                                IsEnabled = existingCredential.IsEnabled,
                                ChangedProperties = changedProperties.ToArray(),
                                CorrelationId = Guid.NewGuid().ToString()
                            },
                            $"update provider credential {existingCredential.Id}",
                            new { ProviderName = existingCredential.ProviderType.ToString(), ChangedProperties = string.Join(", ", changedProperties) });
                    }
                    else
                    {
                        _logger.LogDebug("No changes detected for provider credential {ProviderId} - skipping event publishing", providerCredential.Id);
                    }
                }
                else
                {
                    _logger.LogWarning("Failed to update provider credential with ID {Id}", providerCredential.Id);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating provider credential with ID {Id}", providerCredential.Id);
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
                baseUrl = GetDefaultBaseUrl(credential.ProviderType.ToString());
            }

            // Trim trailing slash if present
            baseUrl = baseUrl?.TrimEnd('/');

            // Construct health check URL based on provider
            return credential.ProviderType.ToString().ToLowerInvariant() switch
            {
                // OpenAI-compatible providers
                "openai" => baseUrl?.EndsWith("/v1") == true ? $"{baseUrl}/models" : $"{baseUrl}/v1/models",
                "anthropic" => baseUrl?.EndsWith("/v1") == true ? $"{baseUrl}/models" : $"{baseUrl}/v1/models",
                "cohere" => baseUrl?.EndsWith("/v1") == true ? $"{baseUrl}/models" : $"{baseUrl}/v1/models",
                "mistral" or "mistralai" => baseUrl?.EndsWith("/v1") == true ? $"{baseUrl}/models" : $"{baseUrl}/v1/models",
                "groq" => baseUrl?.EndsWith("/v1") == true ? $"{baseUrl}/models" : $"{baseUrl}/v1/models",
                "openrouter" => baseUrl?.EndsWith("/api/v1") == true ? $"{baseUrl}/models" : $"{baseUrl}/api/v1/models",
                "fireworks" or "fireworksai" => baseUrl?.EndsWith("/v1") == true ? $"{baseUrl}/models" : $"{baseUrl}/v1/models",
                "openai-compatible" or "openaicompatible" => baseUrl?.EndsWith("/v1") == true ? $"{baseUrl}/models" : $"{baseUrl}/v1/models",

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
                
                // MiniMax - use models endpoint with v1 prefix
                "minimax" => baseUrl?.EndsWith("/v1") == true ? $"{baseUrl}/chat/completions" : $"{baseUrl}/v1/chat/completions",

                _ => $"{baseUrl}/models" // Generic endpoint for other providers
            };
        }

        /// <summary>
        /// Verifies OpenRouter authentication by making a minimal authenticated request
        /// </summary>
        /// <param name="client">The HTTP client with auth headers already set</param>
        /// <param name="apiKey">The API key</param>
        /// <param name="baseUrl">The base URL</param>
        /// <returns>True if authentication is valid, false otherwise</returns>
        private async Task<bool> VerifyOpenRouterAuthenticationAsync(HttpClient client, string? apiKey, string? baseUrl)
        {
            try
            {
                // OpenRouter requires authentication for certain operations
                // We'll make a lightweight request that requires auth but doesn't incur usage costs

                // First, let's try the auth endpoint if available
                var apiBaseUrl = !string.IsNullOrWhiteSpace(baseUrl)
                    ? baseUrl
                    : "https://openrouter.ai";
                var authCheckUrl = $"{apiBaseUrl.TrimEnd('/')}/api/v1/auth/key";

                try
                {
                    _logger.LogInformation("Checking OpenRouter auth endpoint");

                    // Try the auth key endpoint first (if it exists)
                    var authResponse = await client.GetAsync(authCheckUrl);

                    _logger.LogInformation("OpenRouter auth endpoint returned status {StatusCode}", (int)authResponse.StatusCode);

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
                        _logger.LogWarning("OpenRouter authentication failed with status {StatusCode}", (int)authResponse.StatusCode);
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

                var chatUrl = $"{apiBaseUrl.TrimEnd('/')}/api/v1/chat/completions";
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
        /// <param name="apiKey">The API key</param>
        /// <param name="baseUrl">The base URL</param>
        /// <returns>True if authentication is valid, false otherwise</returns>
        private async Task<bool> VerifyGeminiAuthenticationAsync(HttpClient client, string? apiKey, string? baseUrl)
        {
            try
            {
                // Google Gemini requires API key as a query parameter, not in headers
                // We need to make a request that requires authentication
                var apiBaseUrl = !string.IsNullOrWhiteSpace(baseUrl)
                    ? baseUrl
                    : "https://generativelanguage.googleapis.com";

                // Try to get a specific model which requires authentication
                var testUrl = $"{apiBaseUrl.TrimEnd('/')}/v1beta/models/gemini-pro?key={apiKey}";

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
                        response.StatusCode, responseContent.Replace(Environment.NewLine, ""));
                    return false;
                }

                // If we get another status, log it but assume auth is ok
                _logger.LogInformation("Google Gemini returned unexpected status: {Status}", response.StatusCode);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error verifying Google Gemini authentication");
                return false;
            }
        }

        /// <summary>
        /// Verifies OpenAI authentication by making a test request
        /// </summary>
        /// <param name="client">The HTTP client with authorization header set</param>
        /// <param name="apiKey">The API key</param>
        /// <param name="baseUrl">The base URL</param>
        /// <returns>True if authentication is valid, false otherwise</returns>
        private async Task<bool> VerifyOpenAIAuthenticationAsync(HttpClient client, string? apiKey, string? baseUrl)
        {
            try
            {
                // OpenAI's /v1/models endpoint can return data even without proper auth
                // We need to make a request that definitely requires authentication
                
                var apiBaseUrl = !string.IsNullOrWhiteSpace(baseUrl)
                    ? baseUrl.TrimEnd('/')
                    : "https://api.openai.com";

                // Remove /v1 if it's already in the base URL
                if (apiBaseUrl.EndsWith("/v1"))
                {
                    apiBaseUrl = apiBaseUrl.Substring(0, apiBaseUrl.Length - 3);
                }

                // First, try to list models and check the response carefully
                var modelsUrl = $"{apiBaseUrl}/v1/models";
                var modelsResponse = await client.GetAsync(modelsUrl);
                
                if (modelsResponse.StatusCode == HttpStatusCode.Unauthorized || 
                    modelsResponse.StatusCode == HttpStatusCode.Forbidden)
                {
                    var responseContent = await modelsResponse.Content.ReadAsStringAsync();
                    _logger.LogWarning("OpenAI authentication failed: {Status} - {Content}",
                        modelsResponse.StatusCode, responseContent.Replace(Environment.NewLine, ""));
                    return false;
                }
                
                if (modelsResponse.StatusCode == HttpStatusCode.OK)
                {
                    var content = await modelsResponse.Content.ReadAsStringAsync();
                    _logger.LogInformation("OpenAI models endpoint returned 200 OK. Response length: {Length}, First 500 chars: {Content}", 
                        content.Length, 
                        content.Substring(0, Math.Min(500, content.Length)));
                    
                    // Check for error response in the content
                    if (content.Contains("\"error\"") && 
                        (content.Contains("invalid_api_key") || 
                         content.Contains("Incorrect API key") ||
                         content.Contains("Invalid authentication")))
                    {
                        _logger.LogWarning("OpenAI returned error in response body indicating invalid API key");
                        return false;
                    }
                    
                    // Parse the response to check if we have actual model data
                    try
                    {
                        using var doc = JsonDocument.Parse(content);
                        var root = doc.RootElement;
                        
                        // Check if we have a data array with actual models
                        if (root.TryGetProperty("data", out var dataElement) && 
                            dataElement.GetArrayLength() > 0)
                        {
                            // Verify that at least one model has an ID
                            foreach (var model in dataElement.EnumerateArray())
                            {
                                if (model.TryGetProperty("id", out var idElement) && 
                                    !string.IsNullOrWhiteSpace(idElement.GetString()))
                                {
                                    _logger.LogInformation("OpenAI authentication successful - found valid model data");
                                    return true;
                                }
                            }
                        }
                        
                        _logger.LogWarning("OpenAI models response has no valid model data");
                        return false;
                    }
                    catch (JsonException)
                    {
                        _logger.LogWarning("OpenAI models response is not valid JSON");
                        return false;
                    }
                }

                // For any other status codes, the authentication is invalid
                _logger.LogWarning("OpenAI returned unexpected status: {Status} - treating as invalid", modelsResponse.StatusCode);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error verifying OpenAI authentication");
                // If we can't verify, assume it's invalid to be safe
                return false;
            }
        }

        /// <summary>
        /// Verifies MiniMax authentication by making a minimal chat completion request
        /// </summary>
        /// <param name="client">The HTTP client with authorization header set</param>
        /// <param name="apiKey">The API key</param>
        /// <param name="baseUrl">The base URL</param>
        /// <returns>True if authentication is valid, false otherwise</returns>
        private async Task<bool> VerifyMiniMaxAuthenticationAsync(HttpClient client, string? apiKey, string? baseUrl)
        {
            try
            {
                var apiBaseUrl = !string.IsNullOrWhiteSpace(baseUrl)
                    ? baseUrl
                    : "https://api.minimax.io";

                // MiniMax doesn't have a GET endpoint, so we need to make a minimal POST request
                var testRequest = new
                {
                    model = "abab6.5-chat",
                    messages = new[]
                    {
                        new { role = "user", content = "test" }
                    },
                    max_tokens = 1,
                    stream = false
                };

                var json = JsonSerializer.Serialize(testRequest);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var chatUrl = $"{apiBaseUrl.TrimEnd('/')}/v1/chat/completions";
                var response = await client.PostAsync(chatUrl, content);

                _logger.LogInformation("MiniMax test request returned status {StatusCode}", (int)response.StatusCode);

                if (response.StatusCode == HttpStatusCode.Unauthorized ||
                    response.StatusCode == HttpStatusCode.Forbidden)
                {
                    return false;
                }

                // Even if the request fails for other reasons (like insufficient credits),
                // if we didn't get an auth error, the key is valid
                if (response.StatusCode == HttpStatusCode.OK || 
                    response.StatusCode == HttpStatusCode.BadRequest ||
                    response.StatusCode == HttpStatusCode.PaymentRequired ||
                    response.StatusCode == HttpStatusCode.NotAcceptable ||
                    response.StatusCode == HttpStatusCode.PreconditionFailed)
                {
                    return true;
                }

                // Log unexpected status codes
                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("MiniMax returned unexpected status {StatusCode}: {Content}", 
                    (int)response.StatusCode, responseContent);

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error verifying MiniMax authentication");
                return false;
            }
        }

        /// <summary>
        /// Verifies Anthropic authentication by making a test request to the messages endpoint
        /// </summary>
        /// <param name="client">The HTTP client with auth headers already set</param>
        /// <param name="apiKey">The API key</param>
        /// <param name="baseUrl">The base URL</param>
        /// <returns>True if authentication is valid, false otherwise</returns>
        private async Task<bool> VerifyAnthropicAuthenticationAsync(HttpClient client, string? apiKey, string? baseUrl)
        {
            try
            {
                // Anthropic doesn't have a models endpoint, so we'll make a minimal messages request
                // that will fail immediately if auth is invalid
                var apiBaseUrl = !string.IsNullOrWhiteSpace(baseUrl)
                    ? baseUrl
                    : "https://api.anthropic.com";

                var testRequest = new
                {
                    model = "claude-3-haiku-20240307", // Use the cheapest model
                    messages = new[]
                    {
                        new { role = "user", content = "Hi" }
                    },
                    max_tokens = 1, // Minimal tokens to reduce cost
                    // Add an invalid parameter to make the request fail after auth check
                    temperature = 2.0 // Invalid temperature (max is 1.0)
                };

                var json = JsonSerializer.Serialize(testRequest);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var messagesUrl = $"{apiBaseUrl.TrimEnd('/')}/v1/messages";
                var response = await client.PostAsync(messagesUrl, content);

                _logger.LogInformation("Anthropic auth check returned status {StatusCode}", (int)response.StatusCode);

                // Check for authentication-specific errors
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Anthropic authentication failed: {Response}", responseContent);
                    return false;
                }

                // If we get a BadRequest (likely due to invalid temperature), auth is valid
                if (response.StatusCode == HttpStatusCode.BadRequest)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    // Check if it's a parameter validation error (which means auth passed)
                    if (responseContent.Contains("temperature") || responseContent.Contains("invalid_request_error"))
                    {
                        _logger.LogInformation("Anthropic authentication successful (request failed on validation as expected)");
                        return true;
                    }
                }

                // Any other 2xx response also indicates valid auth
                if ((int)response.StatusCode >= 200 && (int)response.StatusCode < 300)
                {
                    return true;
                }

                // For any other status, log and consider auth invalid
                _logger.LogWarning("Unexpected response from Anthropic: {StatusCode}", response.StatusCode);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error verifying Anthropic authentication");
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
                "groq" => "https://api.groq.com/openai",
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
                "minimax" => "https://api.minimax.io",
                _ => "https://api.example.com" // Generic fallback
            };
        }

        /// <inheritdoc />
        public async Task<IEnumerable<ProviderKeyCredentialDto>> GetProviderKeyCredentialsAsync(int providerId)
        {
            try
            {
                var keys = await _configProviderCredentialService.GetKeyCredentialsByProviderIdAsync(providerId);
                
                return keys.Select(k => new ProviderKeyCredentialDto
                {
                    Id = k.Id,
                    ProviderCredentialId = k.ProviderCredentialId,
                    ProviderAccountGroup = k.ProviderAccountGroup,
                    ApiKey = MaskApiKey(k.ApiKey),
                    BaseUrl = k.BaseUrl,
                    Organization = k.Organization,
                    IsPrimary = k.IsPrimary,
                    IsEnabled = k.IsEnabled,
                    KeyName = k.KeyName,
                    CreatedAt = k.CreatedAt,
                    UpdatedAt = k.UpdatedAt
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting key credentials for provider {ProviderId}", providerId);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<ProviderKeyCredentialDto?> GetProviderKeyCredentialAsync(int keyId)
        {
            try
            {
                var key = await _configProviderCredentialService.GetKeyCredentialByIdAsync(keyId);

                if (key == null)
                {
                    return null;
                }

                return new ProviderKeyCredentialDto
                {
                    Id = key.Id,
                    ProviderCredentialId = key.ProviderCredentialId,
                    ProviderAccountGroup = key.ProviderAccountGroup,
                    ApiKey = MaskApiKey(key.ApiKey),
                    BaseUrl = key.BaseUrl,
                    Organization = key.Organization,
                    IsPrimary = key.IsPrimary,
                    IsEnabled = key.IsEnabled,
                    KeyName = key.KeyName,
                    CreatedAt = key.CreatedAt,
                    UpdatedAt = key.UpdatedAt
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting key credential {KeyId}", keyId);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<ProviderKeyCredentialDto> CreateProviderKeyCredentialAsync(int providerId, CreateProviderKeyCredentialDto keyCredential)
        {
            if (keyCredential == null)
            {
                throw new ArgumentNullException(nameof(keyCredential));
            }

            try
            {
                var keyEntity = new ProviderKeyCredential
                {
                    ProviderAccountGroup = keyCredential.ProviderAccountGroup,
                    ApiKey = keyCredential.ApiKey,
                    BaseUrl = keyCredential.BaseUrl,
                    Organization = keyCredential.Organization,
                    IsPrimary = keyCredential.IsPrimary,
                    IsEnabled = keyCredential.IsEnabled,
                    KeyName = keyCredential.KeyName
                };

                var created = await _configProviderCredentialService.AddKeyCredentialAsync(providerId, keyEntity);
                
                _logger.LogInformation("Created key credential {KeyId} for provider {ProviderId}", created.Id, providerId);

                return new ProviderKeyCredentialDto
                {
                    Id = created.Id,
                    ProviderCredentialId = created.ProviderCredentialId,
                    ProviderAccountGroup = created.ProviderAccountGroup,
                    ApiKey = MaskApiKey(created.ApiKey),
                    BaseUrl = created.BaseUrl,
                    Organization = created.Organization,
                    IsPrimary = created.IsPrimary,
                    IsEnabled = created.IsEnabled,
                    KeyName = created.KeyName,
                    CreatedAt = created.CreatedAt,
                    UpdatedAt = created.UpdatedAt
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating key credential for provider {ProviderId}", providerId);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<bool> UpdateProviderKeyCredentialAsync(int keyId, UpdateProviderKeyCredentialDto keyCredential)
        {
            if (keyCredential == null)
            {
                throw new ArgumentNullException(nameof(keyCredential));
            }

            try
            {
                // Get the existing key to preserve values not being updated
                var existing = await _configProviderCredentialService.GetKeyCredentialByIdAsync(keyId);
                if (existing == null)
                {
                    _logger.LogWarning("Key credential {KeyId} not found for update", keyId);
                    return false;
                }

                // Update only the fields that are provided
                if (!string.IsNullOrEmpty(keyCredential.ApiKey))
                    existing.ApiKey = keyCredential.ApiKey;
                    
                existing.BaseUrl = keyCredential.BaseUrl;
                existing.Organization = keyCredential.Organization;
                existing.IsEnabled = keyCredential.IsEnabled;
                existing.KeyName = keyCredential.KeyName;

                var success = await _configProviderCredentialService.UpdateKeyCredentialAsync(keyId, existing);
                
                if (success)
                {
                    _logger.LogInformation("Updated key credential {KeyId}", keyId);
                }
                else
                {
                    _logger.LogWarning("Failed to update key credential {KeyId}", keyId);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating key credential {KeyId}", keyId);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<bool> DeleteProviderKeyCredentialAsync(int keyId)
        {
            try
            {
                var success = await _configProviderCredentialService.DeleteKeyCredentialAsync(keyId);
                
                if (success)
                {
                    _logger.LogInformation("Deleted key credential {KeyId}", keyId);
                }
                else
                {
                    _logger.LogWarning("Key credential {KeyId} not found for deletion", keyId);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting key credential {KeyId}", keyId);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<bool> SetPrimaryKeyAsync(int providerId, int keyId)
        {
            try
            {
                var success = await _configProviderCredentialService.SetPrimaryKeyAsync(providerId, keyId);
                
                if (success)
                {
                    _logger.LogInformation("Set key {KeyId} as primary for provider {ProviderId}", keyId, providerId);
                }
                else
                {
                    _logger.LogWarning("Failed to set key {KeyId} as primary - key not found", keyId);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting primary key {KeyId} for provider {ProviderId}", keyId, providerId);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<ProviderConnectionTestResultDto> TestProviderKeyCredentialAsync(int providerId, int keyId)
        {
            try
            {
                // Get the specific key
                var provider = await _providerCredentialRepository.GetByIdAsync(providerId);
                if (provider == null)
                {
                    return new ProviderConnectionTestResultDto
                    {
                        Success = false,
                        Message = "Provider not found",
                        ErrorDetails = $"Provider with ID {providerId} was not found",
                        ProviderType = ProviderType.OpenAI, // Default for unknown
                        Timestamp = DateTime.UtcNow
                    };
                }

                var key = provider.ProviderKeyCredentials?.FirstOrDefault(k => k.Id == keyId);
                if (key == null)
                {
                    return new ProviderConnectionTestResultDto
                    {
                        Success = false,
                        Message = "Key not found",
                        ErrorDetails = $"Key with ID {keyId} was not found for provider {provider.ProviderType}",
                        ProviderType = provider.ProviderType,
                        Timestamp = DateTime.UtcNow
                    };
                }

                // Use the key's API key directly for testing
                var apiKey = key.ApiKey;
                var baseUrl = key.BaseUrl ?? provider.BaseUrl;
                var providerName = provider.ProviderType.ToString();
                
                if (string.IsNullOrEmpty(apiKey))
                {
                    return new ProviderConnectionTestResultDto
                    {
                        Success = false,
                        Message = "Key has no API key configured",
                        ErrorDetails = "The selected key credential does not have an API key",
                        ProviderType = provider.ProviderType,
                        Timestamp = DateTime.UtcNow
                    };
                }
                
                // Call TestProviderConnectionWithKeyAsync with the specific key
                return await TestProviderConnectionWithKeyAsync(providerName, apiKey, baseUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing key credential {KeyId} for provider {ProviderId}", keyId, providerId);
                return new ProviderConnectionTestResultDto
                {
                    Success = false,
                    Message = "Test failed",
                    ErrorDetails = ex.ToString(),
                    ProviderType = ProviderType.OpenAI, // Default for unknown
                    Timestamp = DateTime.UtcNow
                };
            }
        }

        /// <summary>
        /// Masks an API key for display
        /// </summary>
        /// <param name="apiKey">The API key to mask</param>
        /// <returns>The masked API key</returns>
        private static string? MaskApiKey(string? apiKey)
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                return apiKey;
            }

            if (apiKey.Length <= 8)
            {
                return new string('*', apiKey.Length);
            }

            return $"{apiKey.Substring(0, 4)}...{apiKey.Substring(apiKey.Length - 4)}";
        }
    }
}
