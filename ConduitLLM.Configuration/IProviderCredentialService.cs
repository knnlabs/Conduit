using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.DTOs;

namespace ConduitLLM.Configuration
{
    /// <summary>
    /// Interface for managing provider credentials.
    /// Placeholder implementation.
    /// </summary>
    public interface IProviderCredentialService
    {
        Task<ProviderCredential?> GetCredentialByIdAsync(int id);
        Task<List<ProviderCredential>> GetAllCredentialsAsync();
        Task AddCredentialAsync(ProviderCredential credential);
        Task UpdateCredentialAsync(ProviderCredential credential);
        Task DeleteCredentialAsync(int id);
        Task<ProviderCredential?> GetCredentialByProviderTypeAsync(ProviderType providerType);
        
        // Provider Key Credential methods
        Task<List<ProviderKeyCredential>> GetKeyCredentialsByProviderIdAsync(int providerId);
        Task<ProviderKeyCredential?> GetKeyCredentialByIdAsync(int keyId);
        Task<ProviderKeyCredential> AddKeyCredentialAsync(int providerId, ProviderKeyCredential keyCredential);
        Task<bool> UpdateKeyCredentialAsync(int keyId, ProviderKeyCredential keyCredential);
        Task<bool> DeleteKeyCredentialAsync(int keyId);
        Task<bool> SetPrimaryKeyAsync(int providerId, int keyId);
        Task<ProviderConnectionTestResultDto> TestProviderKeyCredentialAsync(int providerId, int keyId);
    }
}
