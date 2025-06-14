using System.Collections.Generic;
using System.Threading.Tasks;

using ConduitLLM.Configuration.DTOs.Audio;

namespace ConduitLLM.Admin.Interfaces
{
    /// <summary>
    /// Service interface for managing audio provider configurations.
    /// </summary>
    public interface IAdminAudioProviderService
    {
        /// <summary>
        /// Gets all audio provider configurations.
        /// </summary>
        Task<List<AudioProviderConfigDto>> GetAllAsync();

        /// <summary>
        /// Gets an audio provider configuration by ID.
        /// </summary>
        Task<AudioProviderConfigDto?> GetByIdAsync(int id);

        /// <summary>
        /// Gets audio provider configurations by provider name.
        /// </summary>
        Task<List<AudioProviderConfigDto>> GetByProviderAsync(string providerName);

        /// <summary>
        /// Gets enabled audio provider configurations for a specific operation.
        /// </summary>
        Task<List<AudioProviderConfigDto>> GetEnabledForOperationAsync(string operationType);

        /// <summary>
        /// Creates a new audio provider configuration.
        /// </summary>
        Task<AudioProviderConfigDto> CreateAsync(CreateAudioProviderConfigDto dto);

        /// <summary>
        /// Updates an existing audio provider configuration.
        /// </summary>
        Task<AudioProviderConfigDto?> UpdateAsync(int id, UpdateAudioProviderConfigDto dto);

        /// <summary>
        /// Deletes an audio provider configuration.
        /// </summary>
        Task<bool> DeleteAsync(int id);

        /// <summary>
        /// Tests audio provider connectivity.
        /// </summary>
        Task<AudioProviderTestResult> TestProviderAsync(int id, string operationType);
    }

    /// <summary>
    /// Result of audio provider connectivity test.
    /// </summary>
    public class AudioProviderTestResult
    {
        /// <summary>
        /// Whether the test was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Test message or error description.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Response time in milliseconds.
        /// </summary>
        public int? ResponseTimeMs { get; set; }

        /// <summary>
        /// Provider capabilities detected.
        /// </summary>
        public Dictionary<string, bool>? Capabilities { get; set; }
    }
}
