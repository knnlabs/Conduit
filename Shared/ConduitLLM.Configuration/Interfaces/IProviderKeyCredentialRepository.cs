using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Configuration.Interfaces
{
    /// <summary>
    /// Repository interface for ProviderKeyCredential operations.
    /// Extends IRepositoryBase for standard CRUD operations and adds domain-specific methods.
    /// </summary>
    public interface IProviderKeyCredentialRepository : IRepositoryBase<ProviderKeyCredential, int>
    {
        /// <summary>
        /// Get all key credentials across all providers
        /// </summary>
        /// <remarks>This method is obsolete. Use GetPaginatedAsync instead for better performance.</remarks>
        [Obsolete("Use GetPaginatedAsync instead. This method loads all records into memory and will be removed in a future version.")]
        Task<List<ProviderKeyCredential>> GetAllAsync();

        /// <summary>
        /// Get all key credentials for a provider
        /// </summary>
        /// <remarks>This method is obsolete. Use GetByProviderIdPaginatedAsync instead for better performance.</remarks>
        [Obsolete("Use GetByProviderIdPaginatedAsync instead. This method loads all records into memory and will be removed in a future version.")]
        Task<List<ProviderKeyCredential>> GetByProviderIdAsync(int providerId);

        /// <summary>
        /// Get key credentials for a provider with pagination
        /// </summary>
        /// <param name="providerId">The provider ID</param>
        /// <param name="pageNumber">The page number (1-based)</param>
        /// <param name="pageSize">The number of items per page</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A tuple with the list of credentials and the total count</returns>
        Task<(List<ProviderKeyCredential> Items, int TotalCount)> GetByProviderIdPaginatedAsync(
            int providerId,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Get the primary key credential for a provider
        /// </summary>
        Task<ProviderKeyCredential?> GetPrimaryKeyAsync(int providerId);

        /// <summary>
        /// Get all enabled key credentials for a provider
        /// </summary>
        Task<List<ProviderKeyCredential>> GetEnabledKeysByProviderIdAsync(int providerId);

        /// <summary>
        /// Set a key as primary (and unset others)
        /// </summary>
        Task<bool> SetPrimaryKeyAsync(int providerId, int keyId);

        /// <summary>
        /// Check if a provider has any key credentials
        /// </summary>
        Task<bool> HasKeyCredentialsAsync(int providerId);

        /// <summary>
        /// Count key credentials for a provider
        /// </summary>
        Task<int> CountByProviderIdAsync(int providerId);
    }
}
