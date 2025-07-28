using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Configuration.Services;
using ConduitLLM.Configuration.Events;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Exceptions;
using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ConduitLLM.Configuration
{
    /// <summary>
    /// Service for managing provider credentials.
    /// </summary>
    public class ProviderCredentialService : IProviderCredentialService
    {
        private readonly ILogger<ProviderCredentialService> _logger;
        private readonly IProviderCredentialRepository _repository;
        private readonly IProviderKeyCredentialRepository _keyRepository;
        private readonly ProviderKeyCredentialValidator _keyValidator;
        private readonly IPublishEndpoint _publishEndpoint;

        public ProviderCredentialService(
            ILogger<ProviderCredentialService> logger,
            IProviderCredentialRepository repository,
            IProviderKeyCredentialRepository keyRepository,
            ProviderKeyCredentialValidator keyValidator,
            IPublishEndpoint publishEndpoint)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _keyRepository = keyRepository ?? throw new ArgumentNullException(nameof(keyRepository));
            _keyValidator = keyValidator ?? throw new ArgumentNullException(nameof(keyValidator));
            _publishEndpoint = publishEndpoint ?? throw new ArgumentNullException(nameof(publishEndpoint));
        }

        public async Task AddCredentialAsync(ProviderCredential credential)
        {
            if (credential == null)
            {
                throw new ArgumentNullException(nameof(credential));
            }

            _logger.LogInformation("Adding credential for provider: {ProviderType}", credential.ProviderType);
            
            try
            {
                await _repository.CreateAsync(credential);
                _logger.LogInformation("Successfully added credential for provider: {ProviderType}", credential.ProviderType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding credential for provider: {ProviderType}", credential.ProviderType);
                throw;
            }
        }

        public async Task DeleteCredentialAsync(int id)
        {
            _logger.LogInformation("Deleting credential with ID: {Id}", id);
            
            try
            {
                var success = await _repository.DeleteAsync(id);
                if (!success)
                {
                    _logger.LogWarning("Credential with ID {Id} not found for deletion", id);
                }
                else
                {
                    _logger.LogInformation("Successfully deleted credential with ID: {Id}", id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting credential with ID: {Id}", id);
                throw;
            }
        }

        public async Task<List<ProviderCredential>> GetAllCredentialsAsync()
        {
            _logger.LogInformation("Getting all credentials");
            
            try
            {
                var credentials = await _repository.GetAllAsync();
                _logger.LogInformation("Retrieved {Count} credentials", credentials.Count);
                return credentials;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all credentials");
                throw;
            }
        }

        public async Task<ProviderCredential?> GetCredentialByIdAsync(int id)
        {
            _logger.LogInformation("Getting credential by ID: {Id}", id);
            
            try
            {
                var credential = await _repository.GetByIdAsync(id);
                if (credential == null)
                {
                    _logger.LogInformation("Credential with ID {Id} not found", id);
                }
                else
                {
                    _logger.LogInformation("Retrieved credential for provider: {ProviderType}", credential.ProviderType);
                }
                return credential;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting credential by ID: {Id}", id);
                throw;
            }
        }


        public async Task UpdateCredentialAsync(ProviderCredential credential)
        {
            if (credential == null)
            {
                throw new ArgumentNullException(nameof(credential));
            }

            _logger.LogInformation("Updating credential for provider: {ProviderType}", credential.ProviderType);
            
            try
            {
                credential.UpdatedAt = DateTime.UtcNow;
                var success = await _repository.UpdateAsync(credential);
                if (!success)
                {
                    _logger.LogWarning("Failed to update credential for provider: {ProviderType}", credential.ProviderType);
                }
                else
                {
                    _logger.LogInformation("Successfully updated credential for provider: {ProviderType}", credential.ProviderType);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating credential for provider: {ProviderType}", credential.ProviderType);
                throw;
            }
        }

        public async Task<ProviderCredential?> GetCredentialByProviderTypeAsync(ProviderType providerType)
        {
            _logger.LogInformation("Getting credential by provider type: {ProviderType}", providerType);
            
            try
            {
                var credential = await _repository.GetByProviderTypeAsync(providerType);
                if (credential == null)
                {
                    _logger.LogInformation("Credential for provider type {ProviderType} not found", providerType);
                }
                else
                {
                    _logger.LogInformation("Retrieved credential for provider type: {ProviderType}", providerType);
                }
                return credential;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting credential by provider type: {ProviderType}", providerType);
                throw;
            }
        }

        // Provider Key Credential methods
        public async Task<List<ProviderKeyCredential>> GetKeyCredentialsByProviderIdAsync(int providerId)
        {
            _logger.LogInformation("Getting key credentials for provider ID: {ProviderId}", providerId);
            
            try
            {
                return await _keyRepository.GetByProviderIdAsync(providerId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting key credentials for provider ID: {ProviderId}", providerId);
                throw;
            }
        }

        public async Task<ProviderKeyCredential?> GetKeyCredentialByIdAsync(int keyId)
        {
            _logger.LogInformation("Getting key credential by ID: {KeyId}", keyId);
            
            try
            {
                return await _keyRepository.GetByIdAsync(keyId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting key credential by ID: {KeyId}", keyId);
                throw;
            }
        }

        public async Task<ProviderKeyCredential> AddKeyCredentialAsync(int providerId, ProviderKeyCredential keyCredential)
        {
            if (keyCredential == null)
            {
                throw new ArgumentNullException(nameof(keyCredential));
            }

            _logger.LogInformation("Adding key credential for provider ID: {ProviderId}", providerId);
            
            try
            {
                // Validate we can add a new key
                var validationResult = await _keyValidator.ValidateAddKeyAsync(providerId);
                if (!validationResult.IsValid)
                {
                    throw new InvalidOperationException(validationResult.ErrorMessage);
                }

                // If this is the first key or marked as primary, ensure it's the only primary
                var existingKeys = await _keyRepository.GetByProviderIdAsync(providerId);
                if (!existingKeys.Any() || keyCredential.IsPrimary)
                {
                    // Unset any existing primary keys
                    foreach (var existingKey in existingKeys.Where(k => k.IsPrimary))
                    {
                        existingKey.IsPrimary = false;
                        await _keyRepository.UpdateAsync(existingKey);
                    }
                    keyCredential.IsPrimary = true;
                }

                keyCredential.ProviderCredentialId = providerId;
                
                // Check if this API key already exists for this provider
                var allProviderKeys = await _keyRepository.GetByProviderIdAsync(providerId);
                if (allProviderKeys.Any(k => k.ApiKey == keyCredential.ApiKey))
                {
                    var provider = await _repository.GetByIdAsync(providerId);
                    var providerType = provider?.ProviderType ?? ProviderType.OpenAI;
                    _logger.LogWarning("Duplicate API key attempted for provider {ProviderId} ({ProviderType})", 
                        providerId, providerType);
                    throw new DuplicateProviderKeyException(providerType, providerId);
                }
                
                try
                {
                    var created = await _keyRepository.CreateAsync(keyCredential);
                    
                    _logger.LogInformation("Successfully added key credential {KeyId} for provider {ProviderId}", 
                        created.Id, providerId);
                
                // Publish domain event
                await _publishEndpoint.Publish(new ProviderKeyCredentialCreated
                {
                    KeyId = created.Id,
                    ProviderId = providerId,
                    IsPrimary = created.IsPrimary,
                    IsEnabled = created.IsEnabled,
                    Timestamp = DateTime.UtcNow,
                    CorrelationId = Guid.NewGuid()
                });
                    
                    return created;
                }
                catch (DbUpdateException dbEx)
                {
                    if (IsUniqueConstraintViolation(dbEx))
                    {
                        // Get provider type for better error message
                        var provider = await _repository.GetByIdAsync(providerId);
                        var providerType = provider?.ProviderType ?? ProviderType.OpenAI; // fallback
                        
                        _logger.LogWarning("Duplicate API key attempted for provider {ProviderId} ({ProviderType}) - caught from database constraint", 
                            providerId, providerType);
                        
                        throw new DuplicateProviderKeyException(providerType, providerId);
                    }
                    
                    // Re-throw if not a unique constraint violation
                    throw;
                }
            }
            catch (DuplicateProviderKeyException)
            {
                // Re-throw DuplicateProviderKeyException as-is
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding key credential for provider ID: {ProviderId}", providerId);
                throw;
            }
        }

        public async Task<bool> UpdateKeyCredentialAsync(int keyId, ProviderKeyCredential keyCredential)
        {
            if (keyCredential == null)
            {
                throw new ArgumentNullException(nameof(keyCredential));
            }

            _logger.LogInformation("Updating key credential ID: {KeyId}", keyId);
            
            try
            {
                // Validate the update
                if (!keyCredential.IsEnabled && keyCredential.IsPrimary)
                {
                    var validationResult = await _keyValidator.ValidateDisableKeyAsync(keyId);
                    if (!validationResult.IsValid)
                    {
                        throw new InvalidOperationException(validationResult.ErrorMessage);
                    }
                }

                keyCredential.Id = keyId;
                var success = await _keyRepository.UpdateAsync(keyCredential);
                
                if (success)
                {
                    _logger.LogInformation("Successfully updated key credential {KeyId}", keyId);
                    
                    // Publish domain event
                    await _publishEndpoint.Publish(new ProviderKeyCredentialUpdated
                    {
                        KeyId = keyId,
                        ProviderId = keyCredential.ProviderCredentialId,
                        ChangedProperties = new[] { "ApiKey", "BaseUrl", "ApiVersion", "IsEnabled", "IsPrimary" }, // TODO: Track actual changes
                        Timestamp = DateTime.UtcNow,
                        CorrelationId = Guid.NewGuid()
                    });
                }
                else
                {
                    _logger.LogWarning("Failed to update key credential {KeyId} - not found", keyId);
                }
                
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating key credential ID: {KeyId}", keyId);
                throw;
            }
        }

        public async Task<bool> DeleteKeyCredentialAsync(int keyId)
        {
            _logger.LogInformation("Deleting key credential ID: {KeyId}", keyId);
            
            try
            {
                var key = await _keyRepository.GetByIdAsync(keyId);
                if (key == null)
                {
                    _logger.LogWarning("Key credential {KeyId} not found for deletion", keyId);
                    return false;
                }

                // Ensure we don't delete the last enabled key
                var validationResult = await _keyValidator.ValidateProviderHasEnabledKeyAsync(key.ProviderCredentialId);
                if (!validationResult.IsValid && key.IsEnabled)
                {
                    throw new InvalidOperationException("Cannot delete the last enabled key for a provider");
                }

                var success = await _keyRepository.DeleteAsync(keyId);
                
                if (success)
                {
                    _logger.LogInformation("Successfully deleted key credential {KeyId}", keyId);
                    
                    // Publish domain event
                    await _publishEndpoint.Publish(new ProviderKeyCredentialDeleted
                    {
                        KeyId = keyId,
                        ProviderId = key.ProviderCredentialId,
                        Timestamp = DateTime.UtcNow,
                        CorrelationId = Guid.NewGuid()
                    });
                }
                
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting key credential ID: {KeyId}", keyId);
                throw;
            }
        }

        public async Task<bool> SetPrimaryKeyAsync(int providerId, int keyId)
        {
            _logger.LogInformation("Setting primary key {KeyId} for provider {ProviderId}", keyId, providerId);
            
            try
            {
                // Validate the key can be set as primary
                var validationResult = await _keyValidator.ValidateSetPrimaryAsync(keyId);
                if (!validationResult.IsValid)
                {
                    throw new InvalidOperationException(validationResult.ErrorMessage);
                }

                // Get the old primary key before changing
                var oldPrimaryKey = await _keyRepository.GetPrimaryKeyAsync(providerId);
                var oldPrimaryKeyId = oldPrimaryKey?.Id ?? 0;
                
                var success = await _keyRepository.SetPrimaryKeyAsync(providerId, keyId);
                
                if (success)
                {
                    _logger.LogInformation("Successfully set key {KeyId} as primary for provider {ProviderId}", 
                        keyId, providerId);
                    
                    // Publish domain event
                    await _publishEndpoint.Publish(new ProviderKeyCredentialPrimaryChanged
                    {
                        ProviderId = providerId,
                        OldPrimaryKeyId = oldPrimaryKeyId,
                        NewPrimaryKeyId = keyId,
                        Timestamp = DateTime.UtcNow,
                        CorrelationId = Guid.NewGuid()
                    });
                }
                else
                {
                    _logger.LogWarning("Failed to set key {KeyId} as primary - key not found", keyId);
                }
                
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting primary key {KeyId} for provider {ProviderId}", 
                    keyId, providerId);
                throw;
            }
        }

        public async Task<ProviderConnectionTestResultDto> TestProviderKeyCredentialAsync(int providerId, int keyId)
        {
            _logger.LogInformation("Testing key credential {KeyId} for provider {ProviderId}", keyId, providerId);
            
            try
            {
                // Get the key credential
                var key = await _keyRepository.GetByIdAsync(keyId);
                if (key == null || key.ProviderCredentialId != providerId)
                {
                    _logger.LogWarning("Key credential {KeyId} not found for provider {ProviderId}", keyId, providerId);
                    return new ProviderConnectionTestResultDto
                    {
                        Success = false,
                        Message = "Key credential not found",
                        ErrorDetails = $"Key with ID {keyId} was not found for provider {providerId}",
                        ProviderType = ProviderType.OpenAI // Default to OpenAI for unknown
                    };
                }

                // Get the provider
                var provider = await _repository.GetByIdAsync(providerId);
                if (provider == null)
                {
                    _logger.LogWarning("Provider {ProviderId} not found", providerId);
                    return new ProviderConnectionTestResultDto
                    {
                        Success = false,
                        Message = "Provider not found",
                        ErrorDetails = $"Provider with ID {providerId} was not found",
                        ProviderType = ProviderType.OpenAI // Default to OpenAI for unknown
                    };
                }

                // For now, we'll just return a successful test result
                // In a real implementation, this would actually test the key with the provider's API
                return new ProviderConnectionTestResultDto
                {
                    Success = key.IsEnabled,
                    Message = key.IsEnabled ? "Key is enabled and ready for use" : "Key is disabled",
                    ProviderType = provider.ProviderType,
                    Timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing key credential {KeyId} for provider {ProviderId}", 
                    keyId, providerId);
                return new ProviderConnectionTestResultDto
                {
                    Success = false,
                    Message = "Test failed",
                    ErrorDetails = ex.Message,
                    ProviderType = ProviderType.OpenAI, // Default to OpenAI for unknown
                    Timestamp = DateTime.UtcNow
                };
            }
        }

        /// <summary>
        /// Checks if a DbUpdateException is due to a unique constraint violation on the API key
        /// </summary>
        private bool IsUniqueConstraintViolation(DbUpdateException dbEx)
        {
            // Check immediate inner exception
            if (dbEx.InnerException is PostgresException pgEx)
            {
                // PostgreSQL unique constraint violation error code is 23505
                if (pgEx.SqlState == "23505" && 
                    pgEx.ConstraintName == "IX_ProviderKeyCredential_UniqueApiKeyPerProvider")
                {
                    return true;
                }
                
                // Also check if it's any unique constraint violation on our table
                if (pgEx.SqlState == "23505" && pgEx.ConstraintName?.Contains("ProviderKeyCredential") == true)
                {
                    _logger.LogWarning("Unique constraint violation on unexpected constraint: {ConstraintName}", pgEx.ConstraintName);
                    return true; // Still treat as duplicate key
                }
            }
            
            // Sometimes the PostgresException is nested deeper
            var innerEx = dbEx.InnerException;
            while (innerEx != null)
            {
                if (innerEx is PostgresException postgresEx)
                {
                    if (postgresEx.SqlState == "23505" && 
                        postgresEx.ConstraintName == "IX_ProviderKeyCredential_UniqueApiKeyPerProvider")
                    {
                        return true;
                    }
                    
                    // Also check if it's any unique constraint violation on our table
                    if (postgresEx.SqlState == "23505" && postgresEx.ConstraintName?.Contains("ProviderKeyCredential") == true)
                    {
                        _logger.LogWarning("Unique constraint violation on unexpected constraint: {ConstraintName}", postgresEx.ConstraintName);
                        return true; // Still treat as duplicate key
                    }
                }
                innerEx = innerEx.InnerException;
            }
            
            return false;
        }
    }
}
