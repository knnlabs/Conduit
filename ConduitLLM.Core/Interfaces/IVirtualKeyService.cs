using ConduitLLM.Configuration.DTOs.VirtualKey; 
using ConduitLLM.Configuration.Entities; 

namespace ConduitLLM.Core.Interfaces;

/// <summary>
/// Defines the contract for managing virtual API keys.
/// </summary>
public interface IVirtualKeyService
{
    /// <summary>
    /// Generates a new virtual key and saves its hash to the database.
    /// </summary>
    Task<CreateVirtualKeyResponseDto> GenerateVirtualKeyAsync(CreateVirtualKeyRequestDto request);

    /// <summary>
    /// Retrieves information about a specific virtual key.
    /// </summary>
    Task<VirtualKeyDto?> GetVirtualKeyInfoAsync(int id);

    /// <summary>
    /// Retrieves a list of all virtual keys.
    /// </summary>
    Task<List<VirtualKeyDto>> ListVirtualKeysAsync();

    /// <summary>
    /// Updates an existing virtual key.
    /// </summary>
    Task<bool> UpdateVirtualKeyAsync(int id, UpdateVirtualKeyRequestDto request);

    /// <summary>
    /// Deletes a virtual key by its ID.
    /// </summary>
    Task<bool> DeleteVirtualKeyAsync(int id);

    /// <summary>
    /// Resets the current spend for a virtual key.
    /// </summary>
    Task<bool> ResetSpendAsync(int id);

    /// <summary>
    /// Validates a provided virtual key string against stored key hashes.
    /// Checks budget, expiry, and enabled status.
    /// </summary>
    /// <param name="key">The virtual key string to validate</param>
    /// <param name="requestedModel">Optional model being requested, to check against allowed models</param>
    /// <returns>The valid VirtualKey entity if valid, otherwise null.</returns>
    Task<VirtualKey?> ValidateVirtualKeyAsync(string key, string? requestedModel = null);

    /// <summary>
    /// Updates the spend for a specific virtual key.
    /// </summary>
    Task<bool> UpdateSpendAsync(int keyId, decimal cost);

    /// <summary>
    /// Checks if the budget period for a key has expired based on its duration and start date,
    /// and resets the spend and start date if necessary.
    /// </summary>
    /// <param name="keyId">The ID of the virtual key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the budget was reset, false otherwise.</returns>
    Task<bool> ResetBudgetIfExpiredAsync(int keyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets detailed info about a virtual key for validation and budget checking.
    /// </summary>
    /// <param name="keyId">The ID of the virtual key to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The virtual key entity if found, otherwise null.</returns>
    Task<ConduitLLM.Configuration.Entities.VirtualKey?> GetVirtualKeyInfoForValidationAsync(int keyId, CancellationToken cancellationToken = default);
}