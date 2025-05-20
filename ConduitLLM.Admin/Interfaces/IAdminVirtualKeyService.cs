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
}