using ConduitLLM.Configuration.DTOs.Cache;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Core.Events;
using MassTransit;
using System.Text.Json;

namespace ConduitLLM.Admin.Services
{
    /// <summary>
    /// Service for managing LLM response caching toggle across all instances.
    /// Simple database-backed toggle with event publishing for runtime updates.
    /// </summary>
    public class LLMCacheManagementService : ILLMCacheManagementService
    {
        private readonly IGlobalSettingRepository _globalSettingRepository;
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly ILogger<LLMCacheManagementService> _logger;

        private const string LLM_CACHE_SETTING_KEY = "LLM.Caching.Enabled";

        public LLMCacheManagementService(
            IGlobalSettingRepository globalSettingRepository,
            IPublishEndpoint publishEndpoint,
            ILogger<LLMCacheManagementService> logger)
        {
            _globalSettingRepository = globalSettingRepository ?? throw new ArgumentNullException(nameof(globalSettingRepository));
            _publishEndpoint = publishEndpoint ?? throw new ArgumentNullException(nameof(publishEndpoint));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets the current LLM caching status from GlobalSetting table
        /// </summary>
        public async Task<LLMCacheControlDto> GetLLMCacheStatusAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var setting = await _globalSettingRepository.GetByKeyAsync(LLM_CACHE_SETTING_KEY, cancellationToken);

                if (setting == null)
                {
                    _logger.LogDebug("No LLM cache setting found in database, returning default state (disabled)");
                    return GetDefaultStatus();
                }

                try
                {
                    var metadata = JsonSerializer.Deserialize<LLMCacheMetadata>(setting.Value);
                    if (metadata == null)
                    {
                        _logger.LogWarning("Failed to deserialize LLM cache metadata, value was null");
                        return GetDefaultStatus();
                    }

                    return new LLMCacheControlDto
                    {
                        Enabled = metadata.Enabled,
                        LastChangedAt = metadata.LastChangedAt,
                        LastChangedBy = metadata.LastChangedBy,
                        LastChangeReason = metadata.LastChangeReason,
                        ActiveInstances = null
                    };
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Failed to deserialize LLM cache metadata from GlobalSetting, returning default state");
                    return GetDefaultStatus();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving LLM cache status from database");
                return GetDefaultStatus();
            }
        }

        /// <summary>
        /// Toggles LLM caching for all instances
        /// </summary>
        public async Task<LLMCacheControlDto> ToggleLLMCacheAsync(
            bool enabled,
            string changedBy,
            string? reason = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogWarning(
                    "Toggling LLM cache to {Enabled} by {ChangedBy}. Reason: {Reason}",
                    enabled, changedBy, reason ?? "Not specified");

                var toggleTime = DateTime.UtcNow;

                var metadata = new LLMCacheMetadata
                {
                    Enabled = enabled,
                    LastChangedAt = toggleTime,
                    LastChangedBy = changedBy,
                    LastChangeReason = reason
                };

                var metadataJson = JsonSerializer.Serialize(metadata);
                await _globalSettingRepository.UpsertAsync(
                    LLM_CACHE_SETTING_KEY,
                    metadataJson,
                    "LLM response caching toggle state with audit trail",
                    cancellationToken);

                _logger.LogInformation(
                    "Persisted LLM cache state to GlobalSetting: Enabled={Enabled}",
                    enabled);

                await _publishEndpoint.Publish(new LLMCacheToggleEvent
                {
                    Enabled = enabled,
                    ToggledBy = changedBy,
                    ToggledAt = toggleTime,
                    Reason = reason,
                    ApplyImmediately = true
                }, cancellationToken);

                _logger.LogInformation(
                    "Published LLMCacheToggleEvent: Enabled={Enabled}, ChangedBy={ChangedBy}",
                    enabled, changedBy);

                return new LLMCacheControlDto
                {
                    Enabled = enabled,
                    LastChangedAt = toggleTime,
                    LastChangedBy = changedBy,
                    LastChangeReason = reason,
                    ActiveInstances = null
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to toggle LLM cache");
                throw;
            }
        }

        private LLMCacheControlDto GetDefaultStatus()
        {
            return new LLMCacheControlDto
            {
                Enabled = false,
                LastChangedAt = null,
                LastChangedBy = null,
                LastChangeReason = null,
                ActiveInstances = null
            };
        }
    }

    /// <summary>
    /// Interface for LLM cache management operations
    /// </summary>
    public interface ILLMCacheManagementService
    {
        /// <summary>
        /// Gets the current LLM caching status across all instances.
        /// </summary>
        Task<LLMCacheControlDto> GetLLMCacheStatusAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Toggles LLM caching on or off for all instances via event bus.
        /// </summary>
        Task<LLMCacheControlDto> ToggleLLMCacheAsync(bool enabled, string changedBy, string? reason = null, CancellationToken cancellationToken = default);
    }
}
