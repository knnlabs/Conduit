using ConduitLLM.Configuration.Entities;

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
        Task<ProviderCredential?> GetCredentialByProviderNameAsync(string providerName); // Example method
    }
}
