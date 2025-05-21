using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration.DTOs.VirtualKey;

namespace ConduitLLM.WebUI.Interfaces
{
    /// <summary>
    /// Interface for managing virtual keys using the repository pattern
    /// </summary>
    /// <remarks>
    /// The VirtualKeyService is responsible for creating, reading, updating, deleting, and validating 
    /// virtual API keys that are used for authentication and authorization in the LLM API. 
    /// 
    /// Virtual keys provide several features:
    /// - Authentication for API requests
    /// - Budget tracking and spending limits
    /// - Rate limiting
    /// - Model restrictions
    /// - Expiration dates
    /// </remarks>
    public interface IVirtualKeyService
    {
        /// <summary>
        /// Generates a new virtual key and saves its hash to the database.
        /// </summary>
        /// <param name="request">The request DTO containing properties for the new virtual key including 
        /// name, allowed models, budget limits, expiration, and rate limiting.</param>
        /// <returns>A response DTO containing both the newly generated key (shown only once) and the key information.</returns>
        /// <remarks>
        /// This method generates a secure random string for the key, computes its hash, and stores the hash 
        /// (not the actual key) in the database. The actual key is returned only once in the response and
        /// should be securely stored by the client as it cannot be retrieved again.
        /// </remarks>
        Task<CreateVirtualKeyResponseDto> GenerateVirtualKeyAsync(CreateVirtualKeyRequestDto request);

        /// <summary>
        /// Retrieves information about a specific virtual key by its ID.
        /// </summary>
        /// <param name="id">The ID of the virtual key to retrieve</param>
        /// <returns>A DTO containing the virtual key information, or null if no key with the specified ID exists</returns>
        /// <remarks>
        /// This method fetches the virtual key entity from the repository and maps it to a DTO that
        /// contains all the key information except the actual key hash.
        /// </remarks>
        Task<VirtualKeyDto?> GetVirtualKeyInfoAsync(int id);

        /// <summary>
        /// Retrieves a list of all virtual keys in the system.
        /// </summary>
        /// <returns>A list of DTOs containing information about all virtual keys</returns>
        /// <remarks>
        /// This method retrieves all virtual key entities from the repository and maps each one
        /// to a DTO.
        /// </remarks>
        Task<List<VirtualKeyDto>> ListVirtualKeysAsync();

        /// <summary>
        /// Updates an existing virtual key with new properties.
        /// </summary>
        /// <param name="id">The ID of the virtual key to update</param>
        /// <param name="request">The request DTO containing the properties to update</param>
        /// <returns>True if the update was successful, false if the key doesn't exist or the update failed</returns>
        /// <remarks>
        /// This method updates the properties of an existing virtual key. Only the properties that are
        /// provided in the request DTO will be updated; null properties will not change the existing values.
        /// </remarks>
        Task<bool> UpdateVirtualKeyAsync(int id, UpdateVirtualKeyRequestDto request);

        /// <summary>
        /// Deletes a virtual key by its ID.
        /// </summary>
        /// <param name="id">The ID of the virtual key to delete</param>
        /// <returns>True if the deletion was successful, false if the key doesn't exist or the deletion failed</returns>
        /// <remarks>
        /// This method permanently removes a virtual key from the database. Any requests using this key
        /// will fail after deletion.
        /// </remarks>
        Task<bool> DeleteVirtualKeyAsync(int id);

        /// <summary>
        /// Resets the current spend for a virtual key and potentially resets the budget start date.
        /// </summary>
        /// <param name="id">The ID of the virtual key to reset</param>
        /// <returns>True if the reset was successful, false if the key doesn't exist or the reset failed</returns>
        /// <remarks>
        /// This method resets the current spend amount to zero for a virtual key. Before resetting, it records
        /// the current spend in the spend history table for record-keeping.
        /// </remarks>
        Task<bool> ResetSpendAsync(int id);

        /// <summary>
        /// Validates a provided virtual key string against stored key hashes.
        /// Checks if the key exists, is enabled, has not expired, and has available budget.
        /// </summary>
        /// <param name="key">The virtual key string to validate</param>
        /// <param name="requestedModel">Optional. The model being requested, to check against allowed models</param>
        /// <returns>The VirtualKeyValidationInfoDto if validation succeeds, null if validation fails for any reason</returns>
        /// <remarks>
        /// This method performs comprehensive validation of a virtual key, including checking if the key exists,
        /// verifying it's enabled, checking expiration, ensuring sufficient budget, and validating model restrictions.
        /// </remarks>
        Task<VirtualKeyValidationInfoDto?> ValidateVirtualKeyAsync(string key, string? requestedModel = null);

        /// <summary>
        /// Updates the spend for a specific virtual key by adding the specified cost amount.
        /// </summary>
        /// <param name="keyId">The ID of the virtual key to update</param>
        /// <param name="cost">The cost amount to add to the current spend</param>
        /// <returns>True if the update was successful, false if the key doesn't exist or the update failed</returns>
        /// <remarks>
        /// This method adds the specified cost to the virtual key's current spend. This is typically called
        /// after a successful API request to update the key's usage.
        /// </remarks>
        Task<bool> UpdateSpendAsync(int keyId, decimal cost);

        /// <summary>
        /// Checks if the budget period for a key has expired based on its duration and start date,
        /// and resets the spend and start date if necessary.
        /// </summary>
        /// <param name="keyId">The ID of the virtual key to check</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests</param>
        /// <returns>True if a budget reset was performed, false if no reset was needed or possible</returns>
        /// <remarks>
        /// This method is typically called periodically to check if a key's budget period has expired.
        /// It supports different budget periods like monthly and daily, and will reset the budget if needed.
        /// </remarks>
        Task<bool> ResetBudgetIfExpiredAsync(int keyId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets detailed info about a virtual key for validation and budget checking.
        /// </summary>
        /// <param name="keyId">The ID of the virtual key to retrieve</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests</param>
        /// <returns>The full VirtualKeyValidationInfoDto if found, null otherwise</returns>
        /// <remarks>
        /// This method retrieves the full virtual key info for use in validation and budget checking.
        /// </remarks>
        Task<VirtualKeyValidationInfoDto?> GetVirtualKeyInfoForValidationAsync(int keyId, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Performs maintenance tasks for all virtual keys including:
        /// - Resetting expired budgets for monthly/daily keys
        /// - Disabling expired keys
        /// - Checking keys approaching budget limits
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>A task representing the asynchronous operation</returns>
        /// <remarks>
        /// This method is typically called periodically by a background service to
        /// ensure virtual keys are properly maintained.
        /// </remarks>
        Task PerformMaintenanceAsync(CancellationToken cancellationToken = default);
    }
}