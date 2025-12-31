using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using ConduitLLM.Admin.DTOs;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Options;
using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Admin.Services
{
    /// <summary>
    /// Service for tracking and reporting media cleanup status.
    /// Uses Redis for last run tracking and GlobalSettings for the runtime toggle.
    /// </summary>
    public class MediaCleanupStatusService : IMediaCleanupStatusService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConnectionMultiplexer? _redis;
        private readonly MediaLifecycleOptions _options;
        private readonly ILogger<MediaCleanupStatusService> _logger;

        // Redis keys
        private const string REDIS_KEY_LAST_RUN = "media:cleanup:last-run";
        private const string REDIS_KEY_LEADER = "media:cleanup:current-leader";

        /// <summary>
        /// GlobalSetting key for the runtime enabled toggle.
        /// </summary>
        public const string SETTING_KEY_ENABLED = "MediaCleanup.Enabled";

        /// <summary>
        /// GlobalSetting key for the simple retention override (days).
        /// When set, overrides policy-based retention for all media.
        /// </summary>
        public const string SETTING_KEY_SIMPLE_RETENTION = "MediaCleanup.SimpleRetentionDays";

        /// <summary>
        /// Minimum allowed retention days for simple override.
        /// </summary>
        public const int MIN_RETENTION_DAYS = 1;

        /// <summary>
        /// Maximum allowed retention days for simple override.
        /// </summary>
        public const int MAX_RETENTION_DAYS = 365;

        /// <summary>
        /// Initializes a new instance of the <see cref="MediaCleanupStatusService"/> class.
        /// </summary>
        public MediaCleanupStatusService(
            IServiceScopeFactory scopeFactory,
            IOptions<MediaLifecycleOptions> options,
            ILogger<MediaCleanupStatusService> logger,
            IConnectionMultiplexer? redis = null)
        {
            _scopeFactory = scopeFactory;
            _options = options.Value;
            _logger = logger;
            _redis = redis;
        }

        /// <inheritdoc />
        public async Task<MediaCleanupStatusDto> GetStatusAsync(CancellationToken cancellationToken = default)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<IConfigurationDbContext>();
            var budgetService = scope.ServiceProvider.GetRequiredService<IMediaDeletionBudgetService>();

            // Get budget info
            var monthlyDeleteCount = await budgetService.GetMonthlyDeleteCountAsync(cancellationToken);
            var remainingBudget = await budgetService.GetRemainingBudgetAsync(_options.MonthlyDeleteBudget, cancellationToken);

            // Get last run info from Redis
            var lastRunInfo = await GetLastRunInfoAsync();

            // Get default retention policy
            var defaultPolicy = await context.MediaRetentionPolicies
                .Where(p => p.IsDefault && p.IsActive)
                .Select(p => new RetentionPolicySummaryDto
                {
                    Name = p.Name,
                    PositiveBalanceRetentionDays = p.PositiveBalanceRetentionDays,
                    ZeroBalanceRetentionDays = p.ZeroBalanceRetentionDays,
                    NegativeBalanceRetentionDays = p.NegativeBalanceRetentionDays
                })
                .FirstOrDefaultAsync(cancellationToken);

            // Count active policies
            var activePoliciesCount = await context.MediaRetentionPolicies
                .CountAsync(p => p.IsActive, cancellationToken);

            // Check if enabled via runtime toggle
            var isEnabled = await IsEnabledAsync(cancellationToken);

            // Calculate next run time
            DateTime? nextScheduledRun = null;
            if (isEnabled && lastRunInfo?.LastRunTimeUtc != null)
            {
                nextScheduledRun = lastRunInfo.LastRunTimeUtc.Value.AddMinutes(_options.ScheduleIntervalMinutes);
                if (nextScheduledRun < DateTime.UtcNow)
                {
                    // If scheduled time has passed, it's likely running now or about to
                    nextScheduledRun = DateTime.UtcNow.AddMinutes(1);
                }
            }
            else if (isEnabled)
            {
                // First run would be ~30 seconds after startup
                nextScheduledRun = DateTime.UtcNow.AddMinutes(_options.ScheduleIntervalMinutes);
            }

            // Get current leader
            var currentLeader = await GetCurrentLeaderAsync();

            // Get simple retention override
            var simpleRetentionOverride = await GetSimpleRetentionOverrideAsync(cancellationToken);

            // Calculate budget percentage
            var budgetUsedPercent = _options.MonthlyDeleteBudget > 0
                ? (double)monthlyDeleteCount / _options.MonthlyDeleteBudget * 100
                : 0;

            return new MediaCleanupStatusDto
            {
                IsEnabled = isEnabled,
                IsDryRunMode = _options.DryRunMode,
                LastRunTimeUtc = lastRunInfo?.LastRunTimeUtc,
                LastRunStatus = lastRunInfo?.Status,
                LastRunFilesDeleted = lastRunInfo?.FilesDeleted ?? 0,
                LastRunBytesFreed = lastRunInfo?.BytesFreed ?? 0,
                LastRunDurationSeconds = lastRunInfo?.DurationSeconds,
                MonthlyDeleteCount = monthlyDeleteCount,
                MonthlyDeleteBudget = _options.MonthlyDeleteBudget,
                MonthlyDeleteBudgetRemaining = remainingBudget,
                MonthlyBudgetUsedPercent = Math.Round(budgetUsedPercent, 2),
                ScheduleIntervalMinutes = _options.ScheduleIntervalMinutes,
                MaxBatchSize = _options.MaxBatchSize,
                DefaultRetentionPolicy = defaultPolicy,
                ActiveRetentionPoliciesCount = activePoliciesCount,
                SimpleRetentionOverrideDays = simpleRetentionOverride,
                NextScheduledRunUtc = nextScheduledRun,
                CurrentLeaderInstanceId = currentLeader
            };
        }

        /// <inheritdoc />
        public async Task RecordRunCompletionAsync(
            int filesDeleted,
            long bytesFreed,
            double durationSeconds,
            string status,
            string leaderInstanceId,
            CancellationToken cancellationToken = default)
        {
            if (_redis == null)
            {
                _logger.LogDebug("Redis not available - last run info will not be persisted");
                return;
            }

            try
            {
                var db = _redis.GetDatabase();
                var runInfo = new LastRunInfo
                {
                    LastRunTimeUtc = DateTime.UtcNow,
                    FilesDeleted = filesDeleted,
                    BytesFreed = bytesFreed,
                    DurationSeconds = durationSeconds,
                    Status = status,
                    LeaderInstanceId = leaderInstanceId
                };

                var json = JsonSerializer.Serialize(runInfo);
                await db.StringSetAsync(REDIS_KEY_LAST_RUN, json, TimeSpan.FromDays(7));
                await db.StringSetAsync(REDIS_KEY_LEADER, leaderInstanceId, TimeSpan.FromMinutes(35));

                _logger.LogDebug(
                    "Recorded cleanup run completion: {FilesDeleted} files, {BytesFreed} bytes, {Duration:F2}s",
                    filesDeleted, bytesFreed, durationSeconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording cleanup run completion to Redis");
            }
        }

        /// <inheritdoc />
        public async Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default)
        {
            // First check the config option
            if (!_options.IsSchedulerEnabled)
            {
                return false;
            }

            // Then check the runtime toggle in GlobalSettings
            using var scope = _scopeFactory.CreateScope();
            var settingRepository = scope.ServiceProvider.GetRequiredService<IGlobalSettingRepository>();

            try
            {
                var setting = await settingRepository.GetByKeyAsync(SETTING_KEY_ENABLED);
                if (setting == null)
                {
                    // If no setting exists, default to the config value
                    return _options.IsSchedulerEnabled;
                }

                if (bool.TryParse(setting.Value, out var enabled))
                {
                    return enabled;
                }

                // Handle common string representations
                var normalized = setting.Value.Trim().ToLowerInvariant();
                return normalized == "1" || normalized == "yes" || normalized == "on" || normalized == "true";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking cleanup enabled state from GlobalSettings");
                // Default to config value on error
                return _options.IsSchedulerEnabled;
            }
        }

        /// <inheritdoc />
        public async Task SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
        {
            using var scope = _scopeFactory.CreateScope();
            var globalSettingService = scope.ServiceProvider.GetRequiredService<IAdminGlobalSettingService>();

            try
            {
                // Use the service layer which publishes GlobalSettingChanged events
                await globalSettingService.UpdateSettingByKeyAsync(new UpdateGlobalSettingByKeyDto
                {
                    Key = SETTING_KEY_ENABLED,
                    Value = enabled.ToString().ToLowerInvariant(),
                    Description = "Runtime toggle for the media cleanup service"
                });

                _logger.LogInformation("Media cleanup service enabled state changed to: {Enabled}", enabled);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting cleanup enabled state in GlobalSettings");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<int?> GetSimpleRetentionOverrideAsync(CancellationToken cancellationToken = default)
        {
            using var scope = _scopeFactory.CreateScope();
            var settingRepository = scope.ServiceProvider.GetRequiredService<IGlobalSettingRepository>();

            try
            {
                var setting = await settingRepository.GetByKeyAsync(SETTING_KEY_SIMPLE_RETENTION, cancellationToken);
                if (setting == null || string.IsNullOrWhiteSpace(setting.Value))
                {
                    return null;
                }

                if (int.TryParse(setting.Value, out var days) && days >= MIN_RETENTION_DAYS && days <= MAX_RETENTION_DAYS)
                {
                    return days;
                }

                _logger.LogWarning(
                    "Invalid simple retention override value '{Value}' - must be {Min}-{Max} days",
                    setting.Value, MIN_RETENTION_DAYS, MAX_RETENTION_DAYS);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading simple retention override from GlobalSettings");
                return null;
            }
        }

        /// <inheritdoc />
        public async Task SetSimpleRetentionOverrideAsync(int? days, CancellationToken cancellationToken = default)
        {
            using var scope = _scopeFactory.CreateScope();
            var globalSettingService = scope.ServiceProvider.GetRequiredService<IAdminGlobalSettingService>();

            try
            {
                if (days.HasValue)
                {
                    // Validate the value
                    if (days.Value < MIN_RETENTION_DAYS || days.Value > MAX_RETENTION_DAYS)
                    {
                        throw new ArgumentOutOfRangeException(
                            nameof(days),
                            $"Retention days must be between {MIN_RETENTION_DAYS} and {MAX_RETENTION_DAYS}");
                    }

                    // Use the service layer which publishes GlobalSettingChanged events
                    await globalSettingService.UpdateSettingByKeyAsync(new UpdateGlobalSettingByKeyDto
                    {
                        Key = SETTING_KEY_SIMPLE_RETENTION,
                        Value = days.Value.ToString(),
                        Description = "Simple retention override - all media deleted after this many days regardless of balance"
                    });

                    _logger.LogInformation("Simple retention override set to {Days} days", days.Value);
                }
                else
                {
                    // Clear the override by deleting the setting
                    await globalSettingService.DeleteSettingByKeyAsync(SETTING_KEY_SIMPLE_RETENTION);
                    _logger.LogInformation("Simple retention override cleared - using policy-based retention");
                }
            }
            catch (ArgumentOutOfRangeException)
            {
                throw; // Re-throw validation errors
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting simple retention override in GlobalSettings");
                throw;
            }
        }

        private async Task<LastRunInfo?> GetLastRunInfoAsync()
        {
            if (_redis == null)
            {
                return null;
            }

            try
            {
                var db = _redis.GetDatabase();
                var json = await db.StringGetAsync(REDIS_KEY_LAST_RUN);

                if (json.IsNullOrEmpty)
                {
                    return null;
                }

                return JsonSerializer.Deserialize<LastRunInfo>(json.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading last run info from Redis");
                return null;
            }
        }

        private async Task<string?> GetCurrentLeaderAsync()
        {
            if (_redis == null)
            {
                return null;
            }

            try
            {
                var db = _redis.GetDatabase();
                var leader = await db.StringGetAsync(REDIS_KEY_LEADER);
                return leader.IsNullOrEmpty ? null : leader.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading current leader from Redis");
                return null;
            }
        }

        /// <summary>
        /// Internal class for serializing last run info to Redis.
        /// </summary>
        private class LastRunInfo
        {
            public DateTime? LastRunTimeUtc { get; set; }
            public int FilesDeleted { get; set; }
            public long BytesFreed { get; set; }
            public double DurationSeconds { get; set; }
            public string? Status { get; set; }
            public string? LeaderInstanceId { get; set; }
        }
    }
}
