using ConduitLLM.Configuration.DTOs.Audio;

namespace ConduitLLM.Admin.Interfaces
{
    /// <summary>
    /// Service interface for managing audio cost configurations.
    /// </summary>
    public interface IAdminAudioCostService
    {
        /// <summary>
        /// Gets all audio cost configurations.
        /// </summary>
        Task<List<AudioCostDto>> GetAllAsync();

        /// <summary>
        /// Gets an audio cost configuration by ID.
        /// </summary>
        Task<AudioCostDto?> GetByIdAsync(int id);

        /// <summary>
        /// Gets audio costs by provider.
        /// </summary>
        Task<List<AudioCostDto>> GetByProviderAsync(int providerId);

        /// <summary>
        /// Gets the current cost for a specific provider and operation.
        /// </summary>
        Task<AudioCostDto?> GetCurrentCostAsync(int providerId, string operationType, string? model = null);

        /// <summary>
        /// Gets cost history for a provider and operation.
        /// </summary>
        Task<List<AudioCostDto>> GetCostHistoryAsync(int providerId, string operationType, string? model = null);

        /// <summary>
        /// Creates a new audio cost configuration.
        /// </summary>
        Task<AudioCostDto> CreateAsync(CreateAudioCostDto dto);

        /// <summary>
        /// Updates an existing audio cost configuration.
        /// </summary>
        Task<AudioCostDto?> UpdateAsync(int id, UpdateAudioCostDto dto);

        /// <summary>
        /// Deletes an audio cost configuration.
        /// </summary>
        Task<bool> DeleteAsync(int id);

        /// <summary>
        /// Imports bulk audio costs from a CSV or JSON file.
        /// </summary>
        Task<BulkImportResult> ImportCostsAsync(string data, string format);

        /// <summary>
        /// Exports audio costs to CSV or JSON format.
        /// </summary>
        Task<string> ExportCostsAsync(string format, int? providerId = null);
    }

    /// <summary>
    /// Result of bulk cost import operation.
    /// </summary>
    public class BulkImportResult
    {
        /// <summary>
        /// Number of costs successfully imported.
        /// </summary>
        public int SuccessCount { get; set; }

        /// <summary>
        /// Number of costs that failed to import.
        /// </summary>
        public int FailureCount { get; set; }

        /// <summary>
        /// Error messages for failed imports.
        /// </summary>
        public List<string> Errors { get; set; } = new();
    }
}
