using ConduitLLM.Configuration.DTOs.VirtualKey;

namespace ConduitLLM.Admin.Interfaces;

/// <summary>
/// Service interface for managing virtual keys through the Admin API
/// </summary>
public interface IAdminVirtualKeyService
{
    /// <summary>
    /// Generates a new virtual key
    /// </summary>
    /// <param name="request">The key generation request</param>
    /// <returns>The generated key response with key string and info</returns>
    Task<CreateVirtualKeyResponseDto> GenerateVirtualKeyAsync(CreateVirtualKeyRequestDto request);
    
    /// <summary>
    /// Gets information about a specific virtual key
    /// </summary>
    /// <param name="id">The ID of the virtual key</param>
    /// <returns>The virtual key information, or null if not found</returns>
    Task<VirtualKeyDto?> GetVirtualKeyInfoAsync(int id);
    
    /// <summary>
    /// Gets a list of all virtual keys
    /// </summary>
    /// <returns>List of virtual key information</returns>
    Task<List<VirtualKeyDto>> ListVirtualKeysAsync();
    
    /// <summary>
    /// Updates an existing virtual key
    /// </summary>
    /// <param name="id">The ID of the virtual key to update</param>
    /// <param name="request">The update request with new properties</param>
    /// <returns>True if the update was successful, false otherwise</returns>
    Task<bool> UpdateVirtualKeyAsync(int id, UpdateVirtualKeyRequestDto request);
    
    /// <summary>
    /// Deletes a virtual key
    /// </summary>
    /// <param name="id">The ID of the virtual key to delete</param>
    /// <returns>True if the deletion was successful, false otherwise</returns>
    Task<bool> DeleteVirtualKeyAsync(int id);
    
    /// <summary>
    /// Resets the spend for a virtual key
    /// </summary>
    /// <param name="id">The ID of the virtual key</param>
    /// <returns>True if the reset was successful, false otherwise</returns>
    Task<bool> ResetSpendAsync(int id);

    /// <summary>
    /// Validates a virtual key
    /// </summary>
    /// <param name="key">The virtual key to validate</param>
    /// <param name="requestedModel">Optional model being requested</param>
    /// <returns>Validation result with information about the key</returns>
    Task<VirtualKeyValidationResult> ValidateVirtualKeyAsync(string key, string? requestedModel = null);
    
    /// <summary>
    /// Updates the spend amount for a virtual key
    /// </summary>
    /// <param name="id">The ID of the virtual key</param>
    /// <param name="cost">The cost to add to the current spend</param>
    /// <returns>True if the update was successful, false otherwise</returns>
    Task<bool> UpdateSpendAsync(int id, decimal cost);
    
    /// <summary>
    /// Checks if the budget period has expired and resets if needed
    /// </summary>
    /// <param name="id">The ID of the virtual key</param>
    /// <returns>Result indicating if a reset was performed</returns>
    Task<BudgetCheckResult> CheckBudgetAsync(int id);
    
    /// <summary>
    /// Gets detailed information about a virtual key for validation purposes
    /// </summary>
    /// <param name="id">The ID of the virtual key</param>
    /// <returns>Virtual key validation information or null if not found</returns>
    Task<VirtualKeyValidationInfoDto?> GetValidationInfoAsync(int id);
}