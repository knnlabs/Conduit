using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Events;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Redis-based implementation of provider error tracking
    /// </summary>
    public class ProviderErrorTrackingService : IProviderErrorTrackingService
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ProviderErrorTrackingService> _logger;
        private readonly IDatabase _db;

        public ProviderErrorTrackingService(
            IConnectionMultiplexer redis,
            IServiceScopeFactory scopeFactory,
            ILogger<ProviderErrorTrackingService> logger)
        {
            _redis = redis ?? throw new ArgumentNullException(nameof(redis));
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _db = _redis.GetDatabase();
        }

        public async Task TrackErrorAsync(ProviderErrorInfo error)
        {
            try
            {
                var keyPrefix = $"provider:errors:key:{error.KeyCredentialId}";
                
                if (error.IsFatal)
                {
                    await TrackFatalErrorAsync(keyPrefix, error);
                }
                else
                {
                    await TrackWarningAsync(keyPrefix, error);
                }
                
                // Update provider summary
                await UpdateProviderSummaryAsync(error.ProviderId, error.IsFatal);
                
                // Add to global feed
                await AddToGlobalFeedAsync(error);
                
                // Check if we should disable the key
                if (error.IsFatal && await ShouldDisableKeyAsync(error.KeyCredentialId, error.ErrorType))
                {
                    await DisableKeyAsync(error.KeyCredentialId, 
                        $"Auto-disabled due to {error.ErrorType}: {error.ErrorMessage}");
                }
                
                _logger.LogInformation(
                    "Tracked {ErrorType} error for key {KeyId}: {Message}",
                    error.ErrorType, error.KeyCredentialId, error.ErrorMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to track provider error for key {KeyId}", error.KeyCredentialId);
                // Don't throw - error tracking should not break the main flow
            }
        }

        private async Task TrackFatalErrorAsync(string keyPrefix, ProviderErrorInfo error)
        {
            var fatalKey = $"{keyPrefix}:fatal";
            
            var tasks = new List<Task>
            {
                _db.HashIncrementAsync(fatalKey, "count"),
                _db.HashSetAsync(fatalKey, new[]
                {
                    new HashEntry("error_type", error.ErrorType.ToString()),
                    new HashEntry("last_seen", error.OccurredAt.ToString("O")),
                    new HashEntry("last_error_message", error.ErrorMessage),
                    new HashEntry("last_status_code", error.HttpStatusCode ?? 0)
                })
            };
            
            // Set first_seen only if it doesn't exist
            tasks.Add(_db.HashSetAsync(fatalKey, "first_seen", 
                error.OccurredAt.ToString("O"), When.NotExists));
            
            await Task.WhenAll(tasks);
        }

        private async Task TrackWarningAsync(string keyPrefix, ProviderErrorInfo error)
        {
            var warningKey = $"{keyPrefix}:warnings";
            var warningData = JsonSerializer.Serialize(new
            {
                type = error.ErrorType.ToString(),
                message = error.ErrorMessage,
                timestamp = error.OccurredAt
            });
            
            await _db.SortedSetAddAsync(warningKey, 
                warningData, 
                new DateTimeOffset(error.OccurredAt).ToUnixTimeSeconds());
            
            // Trim old warnings (keep last 100)
            await _db.SortedSetRemoveRangeByRankAsync(warningKey, 0, -101);
            
            // Set TTL of 30 days
            await _db.KeyExpireAsync(warningKey, TimeSpan.FromDays(30));
        }

        private async Task UpdateProviderSummaryAsync(int providerId, bool isFatal)
        {
            var summaryKey = $"provider:errors:provider:{providerId}:summary";
            
            var tasks = new List<Task>
            {
                _db.HashIncrementAsync(summaryKey, "total_errors"),
                _db.HashSetAsync(summaryKey, "last_error", DateTime.UtcNow.ToString("O"))
            };
            
            if (isFatal)
            {
                tasks.Add(_db.HashIncrementAsync(summaryKey, "fatal_errors"));
            }
            else
            {
                tasks.Add(_db.HashIncrementAsync(summaryKey, "warnings"));
            }
            
            await Task.WhenAll(tasks);
        }

        private async Task AddToGlobalFeedAsync(ProviderErrorInfo error)
        {
            var feedKey = "provider:errors:recent";
            var feedEntry = JsonSerializer.Serialize(new
            {
                keyId = error.KeyCredentialId,
                providerId = error.ProviderId,
                type = error.ErrorType.ToString(),
                message = error.ErrorMessage,
                timestamp = error.OccurredAt
            });
            
            await _db.SortedSetAddAsync(feedKey, 
                feedEntry, 
                new DateTimeOffset(error.OccurredAt).ToUnixTimeSeconds());
            
            // Keep only last 1000 entries
            await _db.SortedSetRemoveRangeByRankAsync(feedKey, 0, -1001);
        }

        public async Task<bool> ShouldDisableKeyAsync(int keyId, ProviderErrorType errorType)
        {
            // Check if we have a disable policy for this error type
            if (!ErrorThresholdConfiguration.FatalErrorPolicies.TryGetValue(errorType, out var policy))
            {
                return false;
            }
            
            // Immediate disable for certain error types
            if (policy.DisableImmediately)
            {
                _logger.LogWarning("Key {KeyId} will be disabled immediately due to {ErrorType}", 
                    keyId, errorType);
                return true;
            }
            
            // Check occurrence count within time window
            var fatalKey = $"provider:errors:key:{keyId}:fatal";
            var errorTypeHash = await _db.HashGetAsync(fatalKey, "error_type");
            
            if (errorTypeHash.HasValue && errorTypeHash.ToString() == errorType.ToString())
            {
                var count = await _db.HashGetAsync(fatalKey, "count");
                var lastSeen = await _db.HashGetAsync(fatalKey, "last_seen");
                
                if (count.HasValue && lastSeen.HasValue)
                {
                    var lastSeenTime = DateTime.Parse(lastSeen.ToString());
                    var timeSinceLastError = DateTime.UtcNow - lastSeenTime;
                    
                    if (timeSinceLastError <= policy.TimeWindow && 
                        (int)count >= policy.RequiredOccurrences)
                    {
                        _logger.LogWarning(
                            "Key {KeyId} will be disabled: {Count} occurrences of {ErrorType} within {Window}",
                            keyId, (int)count, errorType, policy.TimeWindow);
                        return true;
                    }
                }
            }
            
            return false;
        }

        public async Task DisableKeyAsync(int keyId, string reason)
        {
            try
            {
                // Update database
                using var scope = _scopeFactory.CreateScope();
                var keyRepo = scope.ServiceProvider.GetRequiredService<IProviderKeyCredentialRepository>();
                var providerRepo = scope.ServiceProvider.GetRequiredService<IProviderRepository>();
                
                var key = await keyRepo.GetByIdAsync(keyId);
                if (key == null)
                {
                    _logger.LogWarning("Attempted to disable non-existent key {KeyId}", keyId);
                    return;
                }

                // Check if this is a primary key
                if (key.IsPrimary)
                {
                    // For primary keys, disable the provider instead
                    var provider = await providerRepo.GetByIdAsync(key.ProviderId);
                    if (provider != null && provider.IsEnabled)
                    {
                        provider.IsEnabled = false;
                        await providerRepo.UpdateAsync(provider);
                        
                        _logger.LogWarning(
                            "Disabled provider {ProviderId} ({ProviderName}) due to primary key failure: {Reason}",
                            provider.Id, provider.ProviderName, reason);
                        
                        // Update Redis to track provider disable
                        await _db.HashSetAsync($"provider:errors:provider:{provider.Id}:summary", 
                            "provider_disabled_at", DateTime.UtcNow.ToString("O"));
                        await _db.HashSetAsync($"provider:errors:provider:{provider.Id}:summary",
                            "provider_disable_reason", reason);
                        
                        // Publish event for UI update (could create a ProviderDisabledEvent)
                        var publishEndpoint = scope.ServiceProvider.GetService<MassTransit.IPublishEndpoint>();
                        if (publishEndpoint != null)
                        {
                            // Still publish key disabled event so UI knows something happened
                            await publishEndpoint.Publish(new ProviderKeyDisabledEvent
                            {
                                KeyId = keyId,
                                ProviderId = key.ProviderId,
                                Reason = $"Provider disabled: {reason}",
                                DisabledAt = DateTime.UtcNow
                            });
                        }
                    }
                }
                else if (key.IsEnabled)
                {
                    // For secondary keys, disable the key normally
                    key.IsEnabled = false;
                    await keyRepo.UpdateAsync(key);
                    
                    _logger.LogWarning("Disabled secondary key {KeyId} for provider {ProviderId}: {Reason}",
                        keyId, key.ProviderId, reason);
                    
                    // Update Redis
                    await _db.HashSetAsync($"provider:errors:key:{keyId}:fatal", 
                        "disabled_at", DateTime.UtcNow.ToString("O"));
                    
                    // Add key to provider's disabled list
                    var summaryKey = $"provider:errors:provider:{key.ProviderId}:summary";
                    var disabledKeys = await _db.HashGetAsync(summaryKey, "disabled_keys");
                    var keyList = disabledKeys.HasValue 
                        ? JsonSerializer.Deserialize<List<int>>(disabledKeys.ToString()) ?? new List<int>()
                        : new List<int>();
                    
                    if (!keyList.Contains(keyId))
                    {
                        keyList.Add(keyId);
                        await _db.HashSetAsync(summaryKey, "disabled_keys", 
                            JsonSerializer.Serialize(keyList));
                    }
                    
                    // Check if all keys are now disabled - if so, disable the provider
                    var allKeys = await keyRepo.GetByProviderIdAsync(key.ProviderId);
                    if (allKeys.All(k => !k.IsEnabled))
                    {
                        var provider = await providerRepo.GetByIdAsync(key.ProviderId);
                        if (provider != null && provider.IsEnabled)
                        {
                            provider.IsEnabled = false;
                            await providerRepo.UpdateAsync(provider);
                            
                            _logger.LogWarning(
                                "Disabled provider {ProviderId} ({ProviderName}) - all keys are disabled",
                                provider.Id, provider.ProviderName);
                            
                            await _db.HashSetAsync($"provider:errors:provider:{provider.Id}:summary", 
                                "provider_disabled_at", DateTime.UtcNow.ToString("O"));
                            await _db.HashSetAsync($"provider:errors:provider:{provider.Id}:summary",
                                "provider_disable_reason", "All keys disabled");
                        }
                    }
                    
                    // Publish event for UI update
                    var publishEndpoint = scope.ServiceProvider.GetService<MassTransit.IPublishEndpoint>();
                    if (publishEndpoint != null)
                    {
                        await publishEndpoint.Publish(new ProviderKeyDisabledEvent
                        {
                            KeyId = keyId,
                            ProviderId = key.ProviderId,
                            Reason = reason,
                            DisabledAt = DateTime.UtcNow
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to disable key {KeyId}", keyId);
                throw;
            }
        }

        public async Task<IReadOnlyList<ProviderErrorInfo>> GetRecentErrorsAsync(
            int? providerId = null, 
            int? keyId = null,
            int limit = 100)
        {
            var feedKey = "provider:errors:recent";
            var entries = await _db.SortedSetRangeByScoreAsync(
                feedKey, 
                order: Order.Descending, 
                take: limit);
            
            var errors = new List<ProviderErrorInfo>();
            
            foreach (var entry in entries)
            {
                try
                {
                    var data = JsonDocument.Parse(entry.ToString());
                    var errorProviderId = data.RootElement.GetProperty("providerId").GetInt32();
                    var errorKeyId = data.RootElement.GetProperty("keyId").GetInt32();
                    
                    // Apply filters
                    if (providerId.HasValue && errorProviderId != providerId.Value)
                        continue;
                    if (keyId.HasValue && errorKeyId != keyId.Value)
                        continue;
                    
                    errors.Add(new ProviderErrorInfo
                    {
                        KeyCredentialId = errorKeyId,
                        ProviderId = errorProviderId,
                        ErrorType = Enum.Parse<ProviderErrorType>(
                            data.RootElement.GetProperty("type").GetString()!),
                        ErrorMessage = data.RootElement.GetProperty("message").GetString() ?? "",
                        OccurredAt = data.RootElement.GetProperty("timestamp").GetDateTime()
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse error feed entry");
                }
            }
            
            return errors;
        }

        public async Task<Dictionary<int, int>> GetErrorCountsByKeyAsync(int providerId, TimeSpan window)
        {
            var counts = new Dictionary<int, int>();
            
            using var scope = _scopeFactory.CreateScope();
            var keyRepo = scope.ServiceProvider.GetRequiredService<IProviderKeyCredentialRepository>();
            
            var keys = await keyRepo.GetByProviderIdAsync(providerId);
            var cutoff = DateTime.UtcNow - window;
            
            foreach (var key in keys)
            {
                var fatalKey = $"provider:errors:key:{key.Id}:fatal";
                var lastSeen = await _db.HashGetAsync(fatalKey, "last_seen");
                
                if (lastSeen.HasValue)
                {
                    var lastSeenTime = DateTime.Parse(lastSeen.ToString());
                    if (lastSeenTime >= cutoff)
                    {
                        var count = await _db.HashGetAsync(fatalKey, "count");
                        if (count.HasValue)
                        {
                            counts[key.Id] = (int)count;
                        }
                    }
                }
            }
            
            return counts;
        }

        public async Task ClearErrorsForKeyAsync(int keyId)
        {
            var keyPrefix = $"provider:errors:key:{keyId}";
            
            // Delete error keys
            await _db.KeyDeleteAsync(new RedisKey[]
            {
                $"{keyPrefix}:fatal",
                $"{keyPrefix}:warnings"
            });
            
            _logger.LogInformation("Cleared errors for key {KeyId}", keyId);
        }

        public async Task<KeyErrorDetails?> GetKeyErrorDetailsAsync(int keyId)
        {
            using var scope = _scopeFactory.CreateScope();
            var keyRepo = scope.ServiceProvider.GetRequiredService<IProviderKeyCredentialRepository>();
            
            var key = await keyRepo.GetByIdAsync(keyId);
            if (key == null)
                return null;
            
            var details = new KeyErrorDetails
            {
                KeyId = keyId,
                KeyName = key.KeyName ?? $"Key {keyId}",
                IsDisabled = !key.IsEnabled
            };
            
            // Get fatal error info
            var fatalKey = $"provider:errors:key:{keyId}:fatal";
            var fatalData = await _db.HashGetAllAsync(fatalKey);
            
            if (fatalData.Length > 0)
            {
                var fatalDict = fatalData.ToDictionary(x => x.Name.ToString(), x => x.Value.ToString());
                
                details.FatalError = new FatalErrorInfo
                {
                    ErrorType = Enum.Parse<ProviderErrorType>(fatalDict.GetValueOrDefault("error_type", "Unknown")),
                    Count = int.Parse(fatalDict.GetValueOrDefault("count", "0")),
                    FirstSeen = DateTime.Parse(fatalDict.GetValueOrDefault("first_seen", DateTime.UtcNow.ToString("O"))),
                    LastSeen = DateTime.Parse(fatalDict.GetValueOrDefault("last_seen", DateTime.UtcNow.ToString("O"))),
                    LastErrorMessage = fatalDict.GetValueOrDefault("last_error_message", ""),
                    LastStatusCode = int.TryParse(fatalDict.GetValueOrDefault("last_status_code"), out var code) ? code : null
                };
                
                if (fatalDict.TryGetValue("disabled_at", out var disabledAt))
                {
                    details.DisabledAt = DateTime.Parse(disabledAt);
                }
            }
            
            // Get recent warnings
            var warningKey = $"provider:errors:key:{keyId}:warnings";
            var warnings = await _db.SortedSetRangeByScoreAsync(
                warningKey, 
                order: Order.Descending, 
                take: 10);
            
            foreach (var warning in warnings)
            {
                try
                {
                    var data = JsonDocument.Parse(warning.ToString());
                    details.RecentWarnings.Add(new WarningInfo
                    {
                        Type = Enum.Parse<ProviderErrorType>(data.RootElement.GetProperty("type").GetString()!),
                        Message = data.RootElement.GetProperty("message").GetString() ?? "",
                        Timestamp = data.RootElement.GetProperty("timestamp").GetDateTime()
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse warning data");
                }
            }
            
            return details;
        }

        public async Task<ProviderErrorSummary?> GetProviderSummaryAsync(int providerId)
        {
            var summaryKey = $"provider:errors:provider:{providerId}:summary";
            var summaryData = await _db.HashGetAllAsync(summaryKey);
            
            if (summaryData.Length == 0)
                return null;
            
            var summaryDict = summaryData.ToDictionary(x => x.Name.ToString(), x => x.Value.ToString());
            
            return new ProviderErrorSummary
            {
                ProviderId = providerId,
                TotalErrors = int.Parse(summaryDict.GetValueOrDefault("total_errors", "0")),
                FatalErrors = int.Parse(summaryDict.GetValueOrDefault("fatal_errors", "0")),
                Warnings = int.Parse(summaryDict.GetValueOrDefault("warnings", "0")),
                DisabledKeyIds = summaryDict.TryGetValue("disabled_keys", out var keys)
                    ? JsonSerializer.Deserialize<List<int>>(keys) ?? new List<int>()
                    : new List<int>(),
                LastError = summaryDict.TryGetValue("last_error", out var lastError)
                    ? DateTime.Parse(lastError)
                    : null
            };
        }

        public async Task<ErrorStatistics> GetErrorStatisticsAsync(TimeSpan window)
        {
            var stats = new ErrorStatistics();
            var cutoff = DateTime.UtcNow - window;
            
            // Get recent errors from feed
            var feedKey = "provider:errors:recent";
            var entries = await _db.SortedSetRangeByScoreAsync(
                feedKey,
                new DateTimeOffset(cutoff).ToUnixTimeSeconds(),
                new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds());
            
            foreach (var entry in entries)
            {
                try
                {
                    var data = JsonDocument.Parse(entry.ToString());
                    var errorType = data.RootElement.GetProperty("type").GetString()!;
                    
                    stats.TotalErrors++;
                    
                    // Count by type
                    if (!stats.ErrorsByType.ContainsKey(errorType))
                        stats.ErrorsByType[errorType] = 0;
                    stats.ErrorsByType[errorType]++;
                    
                    // Check if fatal
                    var errorTypeEnum = Enum.Parse<ProviderErrorType>(errorType);
                    if ((int)errorTypeEnum <= 9)
                        stats.FatalErrors++;
                    else
                        stats.Warnings++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse statistics entry");
                }
            }
            
            // Count disabled keys
            using (var scope = _scopeFactory.CreateScope())
            {
                var keyRepo = scope.ServiceProvider.GetRequiredService<IProviderKeyCredentialRepository>();
                var keys = await keyRepo.GetAllAsync();
                stats.DisabledKeys = keys.Count(k => !k.IsEnabled);
            }
            
            return stats;
        }
    }
}