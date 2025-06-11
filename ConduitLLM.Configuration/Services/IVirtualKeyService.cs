using System.Collections.Generic;
using System.Threading.Tasks;

using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Configuration.Services
{
    /// <summary>
    /// Service for managing virtual keys
    /// </summary>
    public interface IVirtualKeyService
    {
        /// <summary>
        /// Gets all virtual keys
        /// </summary>
        Task<List<VirtualKey>> GetAllVirtualKeysAsync();

        /// <summary>
        /// Gets a virtual key by its ID
        /// </summary>
        /// <param name="id">The ID of the virtual key</param>
        Task<VirtualKey?> GetVirtualKeyByIdAsync(int id);

        /// <summary>
        /// Gets a virtual key by its key value
        /// </summary>
        /// <param name="keyValue">The key value</param>
        Task<VirtualKey?> GetVirtualKeyByKeyValueAsync(string keyValue);

        /// <summary>
        /// Creates a new virtual key
        /// </summary>
        /// <param name="virtualKey">The virtual key to create</param>
        Task<VirtualKey> CreateVirtualKeyAsync(VirtualKey virtualKey);

        /// <summary>
        /// Updates an existing virtual key
        /// </summary>
        /// <param name="virtualKey">The updated virtual key</param>
        Task<VirtualKey> UpdateVirtualKeyAsync(VirtualKey virtualKey);

        /// <summary>
        /// Deletes a virtual key by its ID
        /// </summary>
        /// <param name="id">The ID of the virtual key to delete</param>
        Task DeleteVirtualKeyAsync(int id);

        /// <summary>
        /// Validates if a virtual key is valid for use
        /// </summary>
        /// <param name="keyValue">The key value to validate</param>
        /// <returns>True if the key is valid, otherwise false</returns>
        Task<bool> ValidateVirtualKeyAsync(string keyValue);

        /// <summary>
        /// Resets the spend for a virtual key
        /// </summary>
        /// <param name="id">The ID of the virtual key</param>
        Task ResetSpendAsync(int id);

        /// <summary>
        /// Updates the spend for a virtual key
        /// </summary>
        /// <param name="id">The ID of the virtual key</param>
        /// <param name="additionalSpend">Additional amount to add to the current spend</param>
        Task UpdateSpendAsync(int id, decimal additionalSpend);
    }
}
