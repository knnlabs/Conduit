using ConduitLLM.Configuration.DTOs.VirtualKey;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Extended virtual key service interface for Core/Http layer with additional validation and DTO support
    /// </summary>
    public interface IVirtualKeyService
    {
        /// <summary>
        /// Generates a new virtual key
        /// </summary>
        Task<CreateVirtualKeyResponseDto> GenerateVirtualKeyAsync(CreateVirtualKeyRequestDto request);

        /// <summary>
        /// Gets virtual key information by ID
        /// </summary>
        Task<VirtualKeyDto?> GetVirtualKeyInfoAsync(int id);

        /// <summary>
        /// Lists all virtual keys
        /// </summary>
        Task<List<VirtualKeyDto>> ListVirtualKeysAsync();

        /// <summary>
        /// Updates a virtual key
        /// </summary>
        Task<bool> UpdateVirtualKeyAsync(int id, UpdateVirtualKeyRequestDto request);

        /// <summary>
        /// Deletes a virtual key
        /// </summary>
        Task<bool> DeleteVirtualKeyAsync(int id);

        /// <summary>
        /// Resets the spend for a virtual key
        /// </summary>
        Task<bool> ResetSpendAsync(int id);

        /// <summary>
        /// Validates a virtual key for authentication without checking balance
        /// </summary>
        Task<VirtualKeyValidationOutcome> ValidateVirtualKeyForAuthenticationAsync(string key, string? requestedModel = null);

        /// <summary>
        /// Validates a virtual key for a balance-protected operation with optional model support
        /// </summary>
        Task<VirtualKeyValidationOutcome> ValidateVirtualKeyAsync(string key, string? requestedModel = null);

        /// <summary>
        /// Updates the spend for a virtual key
        /// </summary>
        Task<bool> UpdateSpendAsync(int keyId, decimal cost);

        /// <summary>
        /// Gets virtual key information for validation purposes
        /// </summary>
        Task<VirtualKey?> GetVirtualKeyInfoForValidationAsync(int keyId, CancellationToken cancellationToken = default);
    }
}
