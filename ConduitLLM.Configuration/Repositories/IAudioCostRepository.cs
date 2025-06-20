using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Configuration.Repositories
{
    /// <summary>
    /// Repository interface for audio cost configurations.
    /// </summary>
    public interface IAudioCostRepository
    {
        /// <summary>
        /// Gets all audio cost configurations.
        /// </summary>
        Task<List<AudioCost>> GetAllAsync();

        /// <summary>
        /// Gets an audio cost configuration by ID.
        /// </summary>
        Task<AudioCost?> GetByIdAsync(int id);

        /// <summary>
        /// Gets audio costs by provider.
        /// </summary>
        Task<List<AudioCost>> GetByProviderAsync(string provider);

        /// <summary>
        /// Gets the current cost for a specific provider, operation, and model.
        /// </summary>
        Task<AudioCost?> GetCurrentCostAsync(string provider, string operationType, string? model = null);

        /// <summary>
        /// Gets costs effective at a specific date.
        /// </summary>
        Task<List<AudioCost>> GetEffectiveAtDateAsync(DateTime date);

        /// <summary>
        /// Creates a new audio cost configuration.
        /// </summary>
        Task<AudioCost> CreateAsync(AudioCost cost);

        /// <summary>
        /// Updates an existing audio cost configuration.
        /// </summary>
        Task<AudioCost> UpdateAsync(AudioCost cost);

        /// <summary>
        /// Deletes an audio cost configuration.
        /// </summary>
        Task<bool> DeleteAsync(int id);

        /// <summary>
        /// Deactivates all costs for a provider and operation type.
        /// </summary>
        Task DeactivatePreviousCostsAsync(string provider, string operationType, string? model = null);

        /// <summary>
        /// Gets cost history for a provider and operation.
        /// </summary>
        Task<List<AudioCost>> GetCostHistoryAsync(string provider, string operationType, string? model = null);
    }
}
