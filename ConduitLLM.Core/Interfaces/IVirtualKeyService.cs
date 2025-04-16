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
}
