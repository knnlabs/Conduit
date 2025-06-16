using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Configuration
{
    /// <summary>
    /// Service for managing provider credentials.
    /// </summary>
    public class ProviderCredentialService : IProviderCredentialService
    {
        private readonly ILogger<ProviderCredentialService> _logger;
        private readonly IProviderCredentialRepository _repository;

        public ProviderCredentialService(
            ILogger<ProviderCredentialService> logger,
            IProviderCredentialRepository repository)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public async Task AddCredentialAsync(ProviderCredential credential)
        {
            if (credential == null)
            {
                throw new ArgumentNullException(nameof(credential));
            }

            _logger.LogInformation("Adding credential for provider: {ProviderName}", credential.ProviderName);
            
            try
            {
                await _repository.CreateAsync(credential);
                _logger.LogInformation("Successfully added credential for provider: {ProviderName}", credential.ProviderName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding credential for provider: {ProviderName}", credential.ProviderName);
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
                    _logger.LogInformation("Retrieved credential for provider: {ProviderName}", credential.ProviderName);
                }
                return credential;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting credential by ID: {Id}", id);
                throw;
            }
        }

        public async Task<ProviderCredential?> GetCredentialByProviderNameAsync(string providerName)
        {
            if (string.IsNullOrWhiteSpace(providerName))
            {
                throw new ArgumentException("Provider name cannot be null or empty", nameof(providerName));
            }

            _logger.LogInformation("Getting credential by provider name: {ProviderName}", providerName);
            
            try
            {
                var credential = await _repository.GetByProviderNameAsync(providerName);
                if (credential == null)
                {
                    _logger.LogInformation("Credential for provider {ProviderName} not found", providerName);
                }
                else
                {
                    _logger.LogInformation("Retrieved credential for provider: {ProviderName}", providerName);
                }
                return credential;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting credential by provider name: {ProviderName}", providerName);
                throw;
            }
        }

        public async Task UpdateCredentialAsync(ProviderCredential credential)
        {
            if (credential == null)
            {
                throw new ArgumentNullException(nameof(credential));
            }

            _logger.LogInformation("Updating credential for provider: {ProviderName}", credential.ProviderName);
            
            try
            {
                credential.UpdatedAt = DateTime.UtcNow;
                var success = await _repository.UpdateAsync(credential);
                if (!success)
                {
                    _logger.LogWarning("Failed to update credential for provider: {ProviderName}", credential.ProviderName);
                }
                else
                {
                    _logger.LogInformation("Successfully updated credential for provider: {ProviderName}", credential.ProviderName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating credential for provider: {ProviderName}", credential.ProviderName);
                throw;
            }
        }
    }
}
