using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.DTOs;

namespace ConduitLLM.Configuration.Interfaces
{
    /// <summary>
    /// Interface for managing providers.
    /// Placeholder implementation.
    /// </summary>
    public interface IProviderService
    {
        Task<Provider?> GetProviderByIdAsync(int id);
        Task<Provider?> GetByIdAsync(int id); // Alias for consistency with AudioCapabilityDetector
        Task<List<Provider>> GetAllProvidersAsync();
        Task<List<Provider>> GetAllEnabledProvidersAsync(); // For AudioRouter
        Task AddProviderAsync(Provider provider);
        Task UpdateProviderAsync(Provider provider);
        Task DeleteProviderAsync(int id);
        
        // Provider Key Credential methods
        Task<List<ProviderKeyCredential>> GetAllCredentialsAsync();
        Task<List<ProviderKeyCredential>> GetKeyCredentialsByProviderIdAsync(int providerId);
        Task<ProviderKeyCredential?> GetKeyCredentialByIdAsync(int keyId);
        Task<ProviderKeyCredential> AddKeyCredentialAsync(int providerId, ProviderKeyCredential keyCredential);
        Task<bool> UpdateKeyCredentialAsync(int keyId, ProviderKeyCredential keyCredential);
        Task<bool> DeleteKeyCredentialAsync(int keyId);
        Task<bool> SetPrimaryKeyAsync(int providerId, int keyId);
        Task<ProviderConnectionTestResultDto> TestProviderKeyCredentialAsync(int providerId, int keyId);
    }
}
