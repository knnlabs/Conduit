using ConduitLLM.Functions.Entities;
using ConduitLLM.Functions.Enums;

namespace ConduitLLM.Functions.Interfaces;

/// <summary>
/// Repository interface for managing function credentials
/// </summary>
public interface IFunctionCredentialRepository
{
    /// <summary>
    /// Gets all function credentials
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of all credentials</returns>
    /// <remarks>
    /// DEPRECATED: Use GetAllUnboundedAsync() for unbounded queries,
    /// or GetPaginatedAsync() for bounded pagination.
    /// </remarks>
    [Obsolete("Use GetAllUnboundedAsync() for cache warming/exports, or GetPaginatedAsync() for bounded queries. This method will be removed in a future version.")]
    Task<List<FunctionCredential>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all function credentials WITHOUT pagination. Use ONLY for legitimate batch operations
    /// like cache warming, exports, or migrations.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of all credentials</returns>
    Task<List<FunctionCredential>> GetAllUnboundedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a paginated list of function credentials.
    /// </summary>
    /// <param name="page">Page number (1-based)</param>
    /// <param name="pageSize">Number of items per page</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A tuple containing the items and total count</returns>
    Task<(List<FunctionCredential> Items, int TotalCount)> GetPaginatedAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a function credential by ID
    /// </summary>
    /// <param name="id">The credential ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The function credential or null if not found</returns>
    Task<FunctionCredential?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all credentials for a specific provider type
    /// </summary>
    /// <param name="providerType">The function provider type</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of credentials</returns>
    Task<List<FunctionCredential>> GetByProviderTypeAsync(FunctionProviderType providerType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all enabled credentials for a specific provider type
    /// </summary>
    /// <param name="providerType">The function provider type</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of enabled credentials</returns>
    Task<List<FunctionCredential>> GetEnabledByProviderTypeAsync(FunctionProviderType providerType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the primary credential for a provider type
    /// </summary>
    /// <param name="providerType">The function provider type</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The primary credential or null if not found</returns>
    Task<FunctionCredential?> GetPrimaryCredentialAsync(FunctionProviderType providerType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets credentials by function account group for a provider type
    /// </summary>
    /// <param name="providerType">The function provider type</param>
    /// <param name="functionAccountGroup">The function account group number (0-32)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of credentials in the specified group</returns>
    Task<List<FunctionCredential>> GetByCredentialGroupAsync(FunctionProviderType providerType, short functionAccountGroup, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new function credential
    /// </summary>
    /// <param name="credential">The credential to create</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The ID of the created credential</returns>
    Task<int> CreateAsync(FunctionCredential credential, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing function credential
    /// </summary>
    /// <param name="credential">The credential to update</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the entity was updated</returns>
    Task<bool> UpdateAsync(FunctionCredential credential, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a function credential by ID
    /// </summary>
    /// <param name="id">The credential ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the entity was deleted</returns>
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a credential as primary and unsets any existing primary credential for the provider type
    /// </summary>
    /// <param name="credentialId">The credential ID to set as primary</param>
    /// <param name="providerType">The function provider type</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SetAsPrimaryAsync(int credentialId, FunctionProviderType providerType, CancellationToken cancellationToken = default);
}
