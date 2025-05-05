using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration.DTOs.VirtualKey;
using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.WebUI.Services
{
    /// <summary>
    /// Interface for managing virtual keys using the repository pattern
    /// </summary>
    public interface IVirtualKeyServiceNew
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
        /// Resets the current spend for a virtual key and potentially resets the budget start date.
        /// </summary>
        Task<bool> ResetSpendAsync(int id);

        /// <summary>
        /// Validates a provided virtual key string against stored key hashes.
        /// Checks if the key exists, is enabled, and has not expired.
        /// </summary>
        Task<VirtualKey?> ValidateVirtualKeyAsync(string key, string? requestedModel = null);

        /// <summary>
        /// Updates the spend for a specific virtual key.
        /// </summary>
        Task<bool> UpdateSpendAsync(int keyId, decimal cost);

        /// <summary>
        /// Checks if the budget period for a key has expired based on its duration and start date,
        /// and resets the spend and start date if necessary.
        /// </summary>
        Task<bool> ResetBudgetIfExpiredAsync(int keyId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets detailed info about a virtual key for validation and budget checking.
        /// </summary>
        Task<VirtualKey?> GetVirtualKeyInfoForValidationAsync(int keyId, CancellationToken cancellationToken = default);
    }
}