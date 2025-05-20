using ConduitLLM.Admin.Extensions;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

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
                    throw new InvalidOperationException($"A provider credential for '{providerCredential.ProviderName}' already exists");
                }

                // Convert DTO to entity
                var credentialEntity = providerCredential.ToEntity();

                // Save to database
                var id = await _providerCredentialRepository.CreateAsync(credentialEntity);

                // Get the created credential
                var createdCredential = await _providerCredentialRepository.GetByIdAsync(id);
                if (createdCredential == null)
                {
                    throw new InvalidOperationException($"Failed to retrieve newly created provider credential with ID {id}");
                }

                _logger.LogInformation("Created provider credential for '{ProviderName}'", providerCredential.ProviderName);
                return createdCredential.ToDto();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating provider credential for '{ProviderName}'", providerCredential.ProviderName);
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
                    _logger.LogInformation("Deleted provider credential with ID {Id}", id);
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
                var credential = await _providerCredentialRepository.GetByProviderNameAsync(providerName);
                return credential?.ToDto();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting provider credential for '{ProviderName}'", providerName);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<ProviderConnectionTestResult> TestProviderConnectionAsync(ProviderCredentialDto providerCredential)
        {
            if (providerCredential == null)
            {
                throw new ArgumentNullException(nameof(providerCredential));
            }

            try
            {
                var startTime = DateTime.UtcNow;
                var result = new ProviderConnectionTestResult
                {
                    IsSuccess = false,
                    ErrorMessage = null,
                    Details = null
                };

                // Get the actual credential (with the real API key)
                var actualCredential = await _providerCredentialRepository.GetByIdAsync(providerCredential.Id);
                if (actualCredential == null)
                {
                    result.ErrorMessage = $"Provider credential with ID {providerCredential.Id} not found";
                    return result;
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
                result.ResponseTimeMs = responseTime.TotalMilliseconds;

                // Check the response
                result.IsSuccess = responseMessage.IsSuccessStatusCode;
                if (!result.IsSuccess)
                {
                    var errorContent = await responseMessage.Content.ReadAsStringAsync();
                    result.ErrorMessage = $"Status: {(int)responseMessage.StatusCode} {responseMessage.ReasonPhrase}";
                    result.Details = !string.IsNullOrEmpty(errorContent) && errorContent.Length <= 1000 
                        ? errorContent 
                        : "Response content too large to display";
                }
                else
                {
                    result.Details = $"Connected successfully to {providerCredential.ProviderName} API in {result.ResponseTimeMs:F2}ms";
                }

                return result;
            }
            catch (TaskCanceledException)
            {
                return new ProviderConnectionTestResult
                {
                    IsSuccess = false,
                    ErrorMessage = "The connection timed out",
                    ResponseTimeMs = 10000  // Default to 10s for timeout
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing connection to provider '{ProviderName}'", providerCredential.ProviderName);
                
                return new ProviderConnectionTestResult
                {
                    IsSuccess = false,
                    ErrorMessage = ex.Message,
                    Details = ex.ToString(),
                    ResponseTimeMs = 0
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

                // Update entity
                existingCredential.UpdateFrom(providerCredential);

                // Save changes
                var result = await _providerCredentialRepository.UpdateAsync(existingCredential);

                if (result)
                {
                    _logger.LogInformation("Updated provider credential with ID {Id}", providerCredential.Id);
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
            if (string.IsNullOrEmpty(baseUrl))
            {
                baseUrl = GetDefaultBaseUrl(credential.ProviderName);
            }
            
            // Trim trailing slash if present
            baseUrl = baseUrl?.TrimEnd('/');
            
            // Construct health check URL based on provider
            return credential.ProviderName.ToLowerInvariant() switch
            {
                "openai" => $"{baseUrl}/v1/models",
                "anthropic" => $"{baseUrl}/v1/models",
                "cohere" => $"{baseUrl}/v1/models",
                "mistral" => $"{baseUrl}/v1/models",
                "groq" => $"{baseUrl}/v1/models",
                "bedrock" => $"{baseUrl}/models", // AWS Bedrock
                "google" => $"{baseUrl}/v1/models", // Gemini
                "azure" => $"{baseUrl}/openai/models?api-version=2023-05-15",
                "ollama" => $"{baseUrl}/api/tags",
                _ => $"{baseUrl}/models" // Generic endpoint for other providers
            };
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
                "mistral" => "https://api.mistral.ai",
                "groq" => "https://api.groq.com",
                "ollama" => "http://localhost:11434",
                _ => "https://api.example.com" // Generic fallback
            };
        }
    }
}