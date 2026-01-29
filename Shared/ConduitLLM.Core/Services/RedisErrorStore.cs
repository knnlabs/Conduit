using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Constants;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Redis implementation of error store
    /// </summary>
    public class RedisErrorStore : IRedisErrorStore
    {
        private readonly IDatabase _db;
        private readonly ILogger<RedisErrorStore> _logger;

        public RedisErrorStore(
            IConnectionMultiplexer redis,
            ILogger<RedisErrorStore> logger)
        {
            _db = redis?.GetDatabase() ?? throw new ArgumentNullException(nameof(redis));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task TrackFatalErrorAsync(int keyId, ProviderErrorInfo error)
        {
            var fatalKey = CacheKeys.ProviderError.FatalByKey(keyId);
            
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

        public async Task TrackWarningAsync(int keyId, ProviderErrorInfo error)
        {
            var warningKey = CacheKeys.ProviderError.WarningsByKey(keyId);
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

        public async Task UpdateProviderSummaryAsync(int providerId, bool isFatal)
        {
            var summaryKey = CacheKeys.ProviderError.ProviderSummary(providerId);
            
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

        public async Task AddToGlobalFeedAsync(ProviderErrorInfo error)
        {
            var feedKey = CacheKeys.ProviderError.RecentFeed;
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

        public async Task<FatalErrorData?> GetFatalErrorDataAsync(int keyId)
        {
            var fatalKey = CacheKeys.ProviderError.FatalByKey(keyId);
            var data = await _db.HashGetAllAsync(fatalKey);
            
            if (data.Length == 0)
                return null;
            
            var dict = data.ToDictionary(x => x.Name.ToString(), x => x.Value.ToString());
            
            return new FatalErrorData
            {
                ErrorType = dict.GetValueOrDefault("error_type"),
                Count = int.TryParse(dict.GetValueOrDefault("count"), out var count) ? count : 0,
                FirstSeen = dict.TryGetValue("first_seen", out var firstSeen) 
                    ? DateTime.Parse(firstSeen) : null,
                LastSeen = dict.TryGetValue("last_seen", out var lastSeen) 
                    ? DateTime.Parse(lastSeen) : null,
                LastErrorMessage = dict.GetValueOrDefault("last_error_message"),
                LastStatusCode = int.TryParse(dict.GetValueOrDefault("last_status_code"), out var code) 
                    ? code : null,
                DisabledAt = dict.TryGetValue("disabled_at", out var disabledAt) 
                    ? DateTime.Parse(disabledAt) : null
            };
        }

        public async Task MarkKeyDisabledAsync(int keyId, DateTime disabledAt)
        {
            var fatalKey = CacheKeys.ProviderError.FatalByKey(keyId);
            await _db.HashSetAsync(fatalKey, "disabled_at", disabledAt.ToString("O"));
        }

        public async Task MarkProviderDisabledAsync(int providerId, DateTime disabledAt, string reason)
        {
            var summaryKey = CacheKeys.ProviderError.ProviderSummary(providerId);
            await Task.WhenAll(
                _db.HashSetAsync(summaryKey, "provider_disabled_at", disabledAt.ToString("O")),
                _db.HashSetAsync(summaryKey, "provider_disable_reason", reason)
            );
        }

        public async Task AddDisabledKeyToProviderAsync(int providerId, int keyId)
        {
            var summaryKey = CacheKeys.ProviderError.ProviderSummary(providerId);
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
        }

        public async Task<IReadOnlyList<ErrorFeedEntry>> GetRecentErrorsAsync(int limit = 100)
        {
            var feedKey = CacheKeys.ProviderError.RecentFeed;
            var entries = await _db.SortedSetRangeByScoreAsync(
                feedKey, 
                order: Order.Descending, 
                take: limit);
            
            var errors = new List<ErrorFeedEntry>();
            
            foreach (var entry in entries)
            {
                try
                {
                    var data = JsonDocument.Parse(entry.ToString());
                    errors.Add(new ErrorFeedEntry
                    {
                        KeyId = data.RootElement.GetProperty("keyId").GetInt32(),
                        ProviderId = data.RootElement.GetProperty("providerId").GetInt32(),
                        ErrorType = data.RootElement.GetProperty("type").GetString() ?? "",
                        Message = data.RootElement.GetProperty("message").GetString() ?? "",
                        Timestamp = data.RootElement.GetProperty("timestamp").GetDateTime()
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse error feed entry");
                }
            }
            
            return errors;
        }

        public async Task<Dictionary<int, ErrorCountInfo>> GetErrorCountsByKeysAsync(
            int providerId, 
            IEnumerable<int> keyIds, 
            TimeSpan window)
        {
            var counts = new Dictionary<int, ErrorCountInfo>();
            var cutoff = DateTime.UtcNow - window;
            
            foreach (var keyId in keyIds)
            {
                var fatalKey = CacheKeys.ProviderError.FatalByKey(keyId);
                var lastSeenValue = await _db.HashGetAsync(fatalKey, "last_seen");
                
                if (lastSeenValue.HasValue)
                {
                    var lastSeenTime = DateTime.Parse(lastSeenValue.ToString());
                    if (lastSeenTime >= cutoff)
                    {
                        var countValue = await _db.HashGetAsync(fatalKey, "count");
                        if (countValue.HasValue)
                        {
                            counts[keyId] = new ErrorCountInfo
                            {
                                Count = (int)countValue,
                                LastSeen = lastSeenTime
                            };
                        }
                    }
                }
            }
            
            return counts;
        }

        public async Task ClearErrorsForKeyAsync(int keyId)
        {
            // Delete error keys
            await _db.KeyDeleteAsync(new RedisKey[]
            {
                CacheKeys.ProviderError.FatalByKey(keyId),
                CacheKeys.ProviderError.WarningsByKey(keyId)
            });
            
            _logger.LogInformation("Cleared errors for key {KeyId}", keyId);
        }

        public async Task<KeyErrorData?> GetKeyErrorDataAsync(int keyId)
        {
            var result = new KeyErrorData();
            
            // Get fatal error data
            result.FatalError = await GetFatalErrorDataAsync(keyId);
            
            // Get recent warnings
            var warningKey = CacheKeys.ProviderError.WarningsByKey(keyId);
            var warnings = await _db.SortedSetRangeByScoreAsync(
                warningKey, 
                order: Order.Descending, 
                take: 10);
            
            foreach (var warning in warnings)
            {
                try
                {
                    var data = JsonDocument.Parse(warning.ToString());
                    result.RecentWarnings.Add(new WarningData
                    {
                        Type = data.RootElement.GetProperty("type").GetString() ?? "",
                        Message = data.RootElement.GetProperty("message").GetString() ?? "",
                        Timestamp = data.RootElement.GetProperty("timestamp").GetDateTime()
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse warning data");
                }
            }
            
            return result;
        }

        public async Task<ProviderSummaryData?> GetProviderSummaryAsync(int providerId)
        {
            var summaryKey = CacheKeys.ProviderError.ProviderSummary(providerId);
            var summaryData = await _db.HashGetAllAsync(summaryKey);
            
            if (summaryData.Length == 0)
                return null;
            
            var dict = summaryData.ToDictionary(x => x.Name.ToString(), x => x.Value.ToString());
            
            return new ProviderSummaryData
            {
                TotalErrors = int.Parse(dict.GetValueOrDefault("total_errors", "0")),
                FatalErrors = int.Parse(dict.GetValueOrDefault("fatal_errors", "0")),
                Warnings = int.Parse(dict.GetValueOrDefault("warnings", "0")),
                DisabledKeyIds = dict.TryGetValue("disabled_keys", out var keys)
                    ? JsonSerializer.Deserialize<List<int>>(keys) ?? new List<int>()
                    : new List<int>(),
                LastError = dict.TryGetValue("last_error", out var lastError)
                    ? DateTime.Parse(lastError) : null,
                ProviderDisabledAt = dict.TryGetValue("provider_disabled_at", out var disabledAt)
                    ? DateTime.Parse(disabledAt) : null,
                ProviderDisableReason = dict.GetValueOrDefault("provider_disable_reason")
            };
        }

        public async Task<ErrorStatsData> GetErrorStatisticsAsync(TimeSpan window)
        {
            var stats = new ErrorStatsData();
            var cutoff = DateTime.UtcNow - window;
            
            // Get recent errors from feed
            var feedKey = CacheKeys.ProviderError.RecentFeed;
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
            
            return stats;
        }
    }
}