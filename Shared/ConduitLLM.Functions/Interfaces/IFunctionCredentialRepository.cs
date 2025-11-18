using ConduitLLM.Functions.Entities;

namespace ConduitLLM.Functions.Interfaces;

/// <summary>
/// Repository interface for managing function credentials
/// </summary>
public interface IFunctionCredentialRepository
{
    /// <summary>
    /// Gets a function credential by ID
    /// </summary>
    /// <param name="id">The credential ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The function credential or null if not found</returns>
    Task<FunctionCredential?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all credentials for a specific function configuration
    /// </summary>
    /// <param name="functionConfigurationId">The function configuration ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of credentials</returns>
    Task<List<FunctionCredential>> GetByFunctionConfigurationIdAsync(int functionConfigurationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all enabled credentials for a specific function configuration
    /// </summary>
    /// <param name="functionConfigurationId">The function configuration ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of enabled credentials</returns>
    Task<List<FunctionCredential>> GetEnabledByFunctionConfigurationIdAsync(int functionConfigurationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the primary credential for a function configuration
    /// </summary>
    /// <param name="functionConfigurationId">The function configuration ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The primary credential or null if not found</returns>
    Task<FunctionCredential?> GetPrimaryCredentialAsync(int functionConfigurationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets credentials by function account group
    /// </summary>
    /// <param name="functionConfigurationId">The function configuration ID</param>
    /// <param name="functionAccountGroup">The function account group number (0-32)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of credentials in the specified group</returns>
    Task<List<FunctionCredential>> GetByCredentialGroupAsync(int functionConfigurationId, short functionAccountGroup, CancellationToken cancellationToken = default);

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
    Task UpdateAsync(FunctionCredential credential, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a function credential by ID
    /// </summary>
    /// <param name="id">The credential ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a credential as primary and unsets any existing primary credential
    /// </summary>
    /// <param name="credentialId">The credential ID to set as primary</param>
    /// <param name="functionConfigurationId">The function configuration ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SetAsPrimaryAsync(int credentialId, int functionConfigurationId, CancellationToken cancellationToken = default);
}
