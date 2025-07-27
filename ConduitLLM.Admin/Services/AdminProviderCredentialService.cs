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
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Services;

using MassTransit;
using Microsoft.Extensions.Logging;
using ConduitLLM.Providers;

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
        private readonly ILLMClientFactory _llmClientFactory;
        private readonly ILoggerFactory _loggerFactory;

        /// <summary>
        /// Initializes a new instance of the AdminProviderCredentialService
        /// </summary>
        /// <param name="providerCredentialRepository">The provider credential repository</param>
        /// <param name="httpClientFactory">The HTTP client factory for connection testing</param>
        /// <param name="configProviderCredentialService">The configuration provider credential service</param>
        /// <param name="llmClientFactory">The LLM client factory for creating provider instances</param>
        /// <param name="loggerFactory">The logger factory for creating loggers</param>
        /// <param name="publishEndpoint">Optional event publishing endpoint (null if MassTransit not configured)</param>
        /// <param name="logger">The logger</param>
        public AdminProviderCredentialService(
            IProviderCredentialRepository providerCredentialRepository,
            IHttpClientFactory httpClientFactory,
            ConduitLLM.Configuration.IProviderCredentialService configProviderCredentialService,
            ILLMClientFactory llmClientFactory,
            ILoggerFactory loggerFactory,
            IPublishEndpoint? publishEndpoint,
            ILogger<AdminProviderCredentialService> logger)
            : base(publishEndpoint, logger)
        {
            _providerCredentialRepository = providerCredentialRepository ?? throw new ArgumentNullException(nameof(providerCredentialRepository));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _configProviderCredentialService = configProviderCredentialService ?? throw new ArgumentNullException(nameof(configProviderCredentialService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _llmClientFactory = llmClientFactory ?? throw new ArgumentNullException(nameof(llmClientFactory));
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            
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


        /// <summary>
        /// Tests a provider connection using the factory pattern and provider-specific authentication verification.
        /// This is the new method that replaces the switch-based authentication logic.
        /// </summary>
        /// <param name="providerCredential">The provider credential to test</param>
        /// <returns>A result indicating success or failure with details</returns>
        public async Task<ProviderConnectionTestResultDto> TestProviderConnectionAsync(ProviderCredentialDto providerCredential)
        {
            if (providerCredential == null)
            {
                throw new ArgumentNullException(nameof(providerCredential));
            }

            try
            {
                _logger.LogInformation("Testing provider connection for {ProviderType} using factory pattern", providerCredential.ProviderType);
                
                var startTime = DateTime.UtcNow;
                var result = new ProviderConnectionTestResultDto
                {
                    Success = false,
                    Message = string.Empty,
                    ErrorDetails = null,
                    ProviderType = providerCredential.ProviderType,
                    Timestamp = DateTime.UtcNow
                };

                // Get API key and base URL
                string? apiKey = null;
                string? baseUrl = providerCredential.BaseUrl;
                
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
                    
                    // Update the provider type from the database record
                    providerCredential.ProviderType = dbCredential.ProviderType;
                    result.ProviderType = dbCredential.ProviderType;
                }
                else
                {
                    // For testing unsaved providers, we can't test without an API key
                    _logger.LogWarning("Cannot test unsaved provider without API key");
                    result.Message = "Cannot test provider";
                    result.ErrorDetails = "Provider must be saved with an API key before testing";
                    return result;
                }

                // Create test credentials
                var tempCredentials = new ProviderCredentials
                {
                    ApiKey = apiKey,
                    BaseUrl = baseUrl,
                    ProviderType = providerCredential.ProviderType
                };

                // Use the factory's new CreateTestClient method
                var client = _llmClientFactory.CreateTestClient(tempCredentials);
                
                // Check if the client implements IAuthenticationVerifiable
                if (client is IAuthenticationVerifiable authVerifiable)
                {
                    // Use the provider's own authentication verification
                    // Pass null for baseUrl if it's empty to allow providers to use their defaults
                    var authResult = await authVerifiable.VerifyAuthenticationAsync(
                        apiKey, 
                        string.IsNullOrWhiteSpace(baseUrl) ? null : baseUrl);
                    
                    result.Success = authResult.IsSuccess;
                    result.Message = authResult.Message;
                    result.ErrorDetails = authResult.ErrorDetails;
                    
                    if (authResult.ResponseTimeMs.HasValue)
                    {
                        result.Message = $"{authResult.Message} (Response time: {authResult.ResponseTimeMs.Value:F2}ms)";
                    }
                }
                else
                {
                    // Fallback error for providers that don't implement IAuthenticationVerifiable
                    _logger.LogWarning("Provider {ProviderType} does not implement IAuthenticationVerifiable", providerCredential.ProviderType);
                    result.Success = false;
                    result.Message = "Provider does not support authentication verification";
                    result.ErrorDetails = $"The {providerCredential.ProviderType} provider has not implemented authentication verification";
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing connection to provider '{ProviderType}'", providerCredential.ProviderType);

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
        /// Tests a provider connection using the factory pattern and provider-specific authentication verification.
        /// This overload accepts a TestProviderConnectionDto for testing unsaved credentials.
        /// </summary>
        public async Task<ProviderConnectionTestResultDto> TestProviderConnectionAsync(TestProviderConnectionDto testRequest)
        {
            if (testRequest == null)
            {
                throw new ArgumentNullException(nameof(testRequest));
            }

            try
            {
                _logger.LogInformation("Testing provider connection for {ProviderType} using factory pattern", testRequest.ProviderType);
                
                var startTime = DateTime.UtcNow;
                var result = new ProviderConnectionTestResultDto
                {
                    Success = false,
                    Message = string.Empty,
                    ErrorDetails = null,
                    ProviderType = testRequest.ProviderType,
                    Timestamp = DateTime.UtcNow
                };

                // Get API key and base URL
                var apiKey = testRequest.ApiKey;
                var baseUrl = testRequest.BaseUrl;
                
                if (string.IsNullOrEmpty(apiKey))
                {
                    _logger.LogWarning("No API key provided for testing {ProviderType}", testRequest.ProviderType);
                    result.Message = "API key is required";
                    result.ErrorDetails = "Cannot test provider without an API key";
                    return result;
                }

                // Create temporary credentials for testing
                var tempCredentials = new ProviderCredentials
                {
                    ApiKey = apiKey,
                    BaseUrl = baseUrl,
                    ProviderType = testRequest.ProviderType
                };
                
                // Use the factory's new CreateTestClient method
                var client = _llmClientFactory.CreateTestClient(tempCredentials);
                
                // Check if the client implements IAuthenticationVerifiable
                if (client is IAuthenticationVerifiable authVerifiable)
                {
                    // Use the provider's own authentication verification
                    // Pass null for baseUrl if it's empty to allow providers to use their defaults
                    var authResult = await authVerifiable.VerifyAuthenticationAsync(
                        apiKey, 
                        string.IsNullOrWhiteSpace(baseUrl) ? null : baseUrl);
                    
                    result.Success = authResult.IsSuccess;
                    result.Message = authResult.Message;
                    result.ErrorDetails = authResult.ErrorDetails;
                    
                    if (authResult.ResponseTimeMs.HasValue)
                    {
                        result.Message = $"{authResult.Message} (Response time: {authResult.ResponseTimeMs.Value:F2}ms)";
                    }
                }
                else
                {
                    // Fallback error for providers that don't implement IAuthenticationVerifiable
                    _logger.LogWarning("Provider {ProviderType} does not implement IAuthenticationVerifiable", testRequest.ProviderType);
                    result.Success = false;
                    result.Message = "Provider does not support authentication verification";
                    result.ErrorDetails = $"The {testRequest.ProviderType} provider has not implemented authentication verification";
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing connection to provider '{ProviderType}'", testRequest.ProviderType);

                return new ProviderConnectionTestResultDto
                {
                    Success = false,
                    Message = $"Connection test failed: {ex.Message}",
                    ErrorDetails = ex.ToString(),
                    ProviderType = testRequest.ProviderType,
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
                
                _logger.LogInformation("Testing key {KeyId} for provider {ProviderId} ({ProviderType}). BaseUrl from key: '{KeyBaseUrl}', from provider: '{ProviderBaseUrl}', final: '{BaseUrl}'", 
                    keyId, providerId, provider.ProviderType, key.BaseUrl, provider.BaseUrl, baseUrl);
                
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
                
                // Create test credentials for the specific key
                var testCredentials = new ProviderCredentials
                {
                    ApiKey = apiKey,
                    BaseUrl = baseUrl,
                    ProviderType = provider.ProviderType
                };
                
                // Use the factory's new CreateTestClient method
                var startTime = DateTime.UtcNow;
                var client = _llmClientFactory.CreateTestClient(testCredentials);
                
                // Verify authentication
                if (client is IAuthenticationVerifiable authVerifiable)
                {
                    var authResult = await authVerifiable.VerifyAuthenticationAsync(
                        apiKey, 
                        string.IsNullOrWhiteSpace(baseUrl) ? null : baseUrl);
                    
                    return new ProviderConnectionTestResultDto
                    {
                        Success = authResult.IsSuccess,
                        Message = authResult.ResponseTimeMs.HasValue 
                            ? $"{authResult.Message} (Response time: {authResult.ResponseTimeMs.Value:F2}ms)"
                            : authResult.Message,
                        ErrorDetails = authResult.ErrorDetails,
                        ProviderType = provider.ProviderType,
                        Timestamp = DateTime.UtcNow
                    };
                }
                else
                {
                    // This should not happen if all providers implement IAuthenticationVerifiable
                    _logger.LogWarning("Provider {ProviderType} does not implement IAuthenticationVerifiable", provider.ProviderType);
                    return new ProviderConnectionTestResultDto
                    {
                        Success = false,
                        Message = "Provider does not support authentication verification",
                        ErrorDetails = $"The {provider.ProviderType} provider has not implemented authentication verification",
                        ProviderType = provider.ProviderType,
                        Timestamp = DateTime.UtcNow
                    };
                }
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
